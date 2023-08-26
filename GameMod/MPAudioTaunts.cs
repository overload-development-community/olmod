using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Security.Cryptography;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.Events;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using UnityEngine.Audio;
using Debug = UnityEngine.Debug;

namespace GameMod
{
    class MPAudioTaunts
    {

        /* 
         * - write a handler that dynamically schedules downloads/uploads and adjusts the packet size based on the resend rate
         * - replace the AudioTaunt.received_packet list with a dynamically sized bool array 
         */

        public const int AUDIO_TAUNT_SIZE_LIMIT = 131072;           // 128 kB, maximum allowed audio taunt file size in bytes
        public const int PACKET_PAYLOAD_SIZE = 800;                 // maximum payload size in bytes for packets that carry chunks of an audio taunt file
        public const int AMOUNT_OF_TAUNTS_PER_CLIENT = 6;           // the amount of taunts a client is allowed to bring into a match
        public const float DEFAULT_TAUNT_COOLDOWN = 4f;             // defines the minimum interval between sending taunts for the client
        public const float TAUNT_PLAYTIME = 3f;                     // defines the time in seconds that taunts are allowed to play till they get cutoff on the client
        public const float DEFAULT_SPECTRUM_UPDATE_COOLDOWN = 0.07f;
        public static WaitForSecondsRealtime delay = new WaitForSecondsRealtime(0.016f);    // interval between sending packets


        public class AudioTaunt
        {
            public string hash;                     // a MD5 hash over the file data
            public string name;                     // the original filename
            public byte[] audio_taunt_data;         // temporary holder of incoming bytes till the full audio clip is assembled (or permanently on the server to buffer it incase other clients request this data)
            public bool is_data_complete = false;   // indicates wether audio_taunt_data has been fully populated 
            public int timestamp = 0;               // holds the time (Time.frameCount) of when this object was last used in a major action like a file transfer 

            // Client
            public bool is_external_taunt;          // indicates wether this is a taunt that got added by this client (local) or wether it got downloaded in order to allow its activation by other clients during a match (external). external taunts will not show up as selectable taunts in the menu
            public bool ready_to_play;              // indicates wether the audio clip is in a playable state
            public bool requested_taunt;            // holds wether the client has requested the audiotaunt byte data for this.hash from the server
            public AudioClip audioclip;             // contains the playable taunt

            // Server
            public List<int> providing_clients;     // holds the connection ids of clients that use the taunt and can provide this taunt 
            public List<int> requesting_clients;    // A list of all connection ids that have requested this audioclip
            public List<int> received_packets;
        }

        public class AudioSourceContainer
        {
            public string player_name = "";         // the player that initiated the clip that is playing on this source. 
            public float[] frequency_band = { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f };
            public AudioSource source = new GameObject().AddComponent<AudioSource>();

            public void UpdateFrequencyBand()
            {
                if (source == null)
                {
                   Debug.Log("AudioSourceContainer.UpdateFrequencyBand: Had to reinstantiate the audio source!");
                    source = new GameObject().AddComponent<AudioSource>();
                }


                if (source.isPlaying)
                {
                    float[] samples = new float[512];
                    source.GetSpectrumData(samples, 0, FFTWindow.Rectangular);
                    int count = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        float average = 0;
                        int sampleCount = (int)Mathf.Pow(2, i) * 2;

                        if (i == 7) sampleCount += 2;

                        for (int j = 0; j < sampleCount; j++)
                        {
                            average += samples[count] * (count + 1);
                            count++;
                        }

                        average /= count;
                        frequency_band[i] = average * 10;
                        frequency_band[i] = Mathf.Min(1.6f, frequency_band[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < frequency_band.Length; i++)
                    {
                        frequency_band[i] = -1f;
                    }
                }
            }
        }

        public static class AClient
        {
            public static bool active = true;
            public static bool initialized = false;
            public static string loaded_local_taunts = "";              // temporary holder (necessary till the execution of LoadLocalAudioTauntsFromPilotPrefs()) of the hashes of the local taunts, gets populated from the pilots config files

            public static string LocalAudioTauntDirectory = "";         // path towards the directory where the audiotaunts from the local installation are saved
            public static string ExternalAudioTauntDirectory = "";      // path towards the directory where the audiotaunts of other players get saved
            public static int[] keybinds = new int[AMOUNT_OF_TAUNTS_PER_CLIENT];                  // keybinds[i] holds the keycode that triggers the playing of local_taunts[i] 
            public static int selected_audio_slot = 0;                  // temporary variable that is used in the menu code to differentiate between the 6 local taunt slots
            public static int audio_taunt_volume = 50;                  // range: 0-100
            public static float remaining_cooldown = 0f;                // time in seconds till the client is allowed to send an audiotaunt

            public static List<AudioTaunt> taunts = new List<AudioTaunt>();                                     // a list of all locally loaded audio taunts
            public static Dictionary<string, AudioTaunt> match_taunts = new Dictionary<string, AudioTaunt>();// contains the audio taunts of the other players during a game
            public static AudioTaunt[] local_taunts = new AudioTaunt[AMOUNT_OF_TAUNTS_PER_CLIENT];// contains the audio taunts that this client has chosen, can not change during a game 

            public static AudioSourceContainer[] asc = new AudioSourceContainer[3];
            public static float spectrum_update_cooldown = 0f;
            public static bool display_audio_spectrum = true;

            [HarmonyPatch(typeof(GameManager), "Awake")]
            class MPAudioTaunts_GameManager_Awake
            {
                static void Postfix()
                {
                    if (String.IsNullOrEmpty(LocalAudioTauntDirectory))
                    {
                        LocalAudioTauntDirectory = Path.Combine(Application.persistentDataPath, "AudioTaunts");
                        if (!Directory.Exists(LocalAudioTauntDirectory))
                        {
                            Debug.Log("Did not find a directory for local audiotaunts, creating one at: " + LocalAudioTauntDirectory);
                            Directory.CreateDirectory(LocalAudioTauntDirectory);
                        }

                        ExternalAudioTauntDirectory = Path.Combine(LocalAudioTauntDirectory, "external");
                        if (!Directory.Exists(ExternalAudioTauntDirectory))
                        {
                            Debug.Log("Did not find a directory for external audiotaunts, creating one at: " + ExternalAudioTauntDirectory);
                            Directory.CreateDirectory(ExternalAudioTauntDirectory);
                        }
                    }

                    if (!GameplayManager.IsDedicatedServer())
                    {
                        for (int i = 0; i < asc.Length; i++)
                        {
                            asc[i] = new AudioSourceContainer();
                        }

                        ImportAudioTaunts(LocalAudioTauntDirectory, new List<string>(), false);
                        ImportAudioTaunts(ExternalAudioTauntDirectory, new List<string>(), true);
                        for (int i = 0; i < 6; i++)
                        {
                            local_taunts[i] = new AudioTaunt
                            {
                                hash = "EMPTY",
                                name = "EMPTY",
                                audioclip = null,
                                ready_to_play = false,
                                requested_taunt = false
                            };
                        }
                        LoadLocalAudioTauntsFromPilotPrefs();
                    }

                    taunts.Sort((x, y) => x.name.CompareTo(y.name));
                    initialized = true;
                }



                // from https://github.com/derhass/olmod/commit/fa897b3384dfd6f228d4c95a385af6b7f37d99b5
                // Fix patching GameManager.Awake
                // When "GameManager.Awake" gets patched, two things happen:
                // 1. GameManager.Version gets set to 0.0.0.0 becuae it internally uses
                //    GetExecutingAssembly() to query the version, but the patched version
                //     lives in another assembly
                // 2. The "anticheat" injection detector is triggered. But that thing
                //    doesn't do anything useful anyway, so disable it
                // return the Assembly which contains Overload.GameManager
                public static Assembly GetOverloadAssembly()
                {
                    return Assembly.GetAssembly(typeof(Overload.GameManager));
                }

                // transpiler
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
                {
                    // This patches the next call of Server.IsDedicatedServer() call after
                    // a StartCoroutine was called to just pushing true onto the stack instead.
                    // We play safe here becuase other patches might add IsDedicatedServer() calls
                    // to that method, so we search specifically for the first one after
                    // StartCoroutine was called.
                    int state = 0;

                    foreach (var code in codes)
                    {
                        // patch GetExecutingAssembly to use GetOverloadAssembly instead
                        if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "GetExecutingAssembly")
                        {
                            var method = AccessTools.Method(typeof(MPAudioTaunts_GameManager_Awake), "GetOverloadAssembly");
                            yield return new CodeInstruction(OpCodes.Call, method);
                            continue;
                        }
                        if (state == 0 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "StartCoroutine")
                        {
                            state = 1;
                        }
                        else if (state == 1 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsDedicatedServer")
                        {
                            // this is the first IsDedicatedServer call after StartCoroutine
                            yield return new CodeInstruction(OpCodes.Ldc_I4_1); // push true on the stack instead
                            state = 2; // do not patch other invocations of StartCoroutine
                            continue;
                        }

                        yield return code;
                    }
                }
            }

            [HarmonyPatch(typeof(MenuManager), "ApplySpeakerMode")]
            internal class MPAudioTaunts_MenuManager_ApplySpeakerMode
            {
                private static AudioSpeakerMode speaker_mode;

                static void Prefix()
                {
                    speaker_mode = AudioSettings.speakerMode;
                }

                static void Postfix()
                {
                    if(speaker_mode != AudioSettings.speakerMode)
                        Reload();
                }
            }

            public static void Reload()
            {

                if (String.IsNullOrEmpty(LocalAudioTauntDirectory))
                {
                    LocalAudioTauntDirectory = Path.Combine(Application.persistentDataPath, "AudioTaunts");

                    if (!Directory.Exists(LocalAudioTauntDirectory))
                    {
                        Debug.Log("Did not find a directory for local audiotaunts, creating one at: " + LocalAudioTauntDirectory);
                        Directory.CreateDirectory(LocalAudioTauntDirectory);
                    }

                    ExternalAudioTauntDirectory = Path.Combine(LocalAudioTauntDirectory, "external");
                    if (!Directory.Exists(ExternalAudioTauntDirectory))
                    {
                        Debug.Log("Did not find a directory for external audiotaunts, creating one at: " + ExternalAudioTauntDirectory);
                        Directory.CreateDirectory(ExternalAudioTauntDirectory);
                    }
                }

                if (!GameplayManager.IsDedicatedServer())
                {
                    AClient.taunts = new List<AudioTaunt>();
                    for (int i = 0; i < AClient.asc.Length; i++)
                        AClient.asc[i] = new AudioSourceContainer();


                    AClient.ImportAudioTaunts(AClient.LocalAudioTauntDirectory, new List<string>(), false, true);
                    AClient.ImportAudioTaunts(AClient.ExternalAudioTauntDirectory, new List<string>(), true, true);
                    for (int i = 0; i < 6; i++)
                    {
                        AClient.local_taunts[i] = new AudioTaunt
                        {
                            hash = "EMPTY",
                            name = "EMPTY",
                            audioclip = null,
                            ready_to_play = false,
                            requested_taunt = false
                        };
                    }
                    AClient.LoadLocalAudioTauntsFromPilotPrefs();
                    taunts.Sort((x, y) => x.name.CompareTo(y.name));

                    Debug.Log("Reload complete. amount of taunts: " + AClient.taunts.Count);
                }
            }

            // Imports either the in 'files_to_load' (contains the filenames, not the hashes) specified taunts or all taunts from that directory
            // (under the condition that they have yet to be imported and are valid formats and that their size is not beyond 128 kB) 
            public static void ImportAudioTaunts(string path_to_directory, List<String> files_to_load, bool external_taunts = false, bool silent = false)
            {
                Debug.Log("Attempting to import AudioTaunts from: " + path_to_directory);

                // measure the time it takes to load the taunts
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                bool load_all_files = files_to_load == null | files_to_load.Count == 0;
                var fileInfo = new DirectoryInfo(path_to_directory).GetFiles();
                foreach (FileInfo file in fileInfo)
                {
                    if ((files_to_load.Contains(file.Name) | load_all_files)    // if this file or all files got requested
                        && taunts.Find(t => t.name.Equals(file.Name)) == null   // if it hasnt been loaded before
                        && file.Extension.Equals(".ogg")                        // we only accept .ogg for now
                        && file.Length <= AUDIO_TAUNT_SIZE_LIMIT)               // limit the taunt size
                    {

                        AudioTaunt t = new AudioTaunt
                        {
                            hash = CalculateMD5ForFile(Path.Combine(path_to_directory, file.Name)),
                            name = file.Name,
                            audio_taunt_data = File.ReadAllBytes(Path.Combine(path_to_directory, file.Name)),
                            is_data_complete = true,
                            timestamp = Time.frameCount,
                            is_external_taunt = external_taunts,
                            audioclip = LoadAsAudioClip(file.Name, path_to_directory),//Resources.Load<AudioClip>("AudioTaunts/" + file.Name)
                            ready_to_play = true,
                            requested_taunt = false
                        };
                        
                        if (t.name.StartsWith(t.hash))
                        {
                            t.name = t.name.Remove(0, t.hash.Length + 1);
                        }
                        else
                        {
                            File.Move(Path.Combine(path_to_directory, file.Name), Path.Combine(path_to_directory, t.hash + "-" + file.Name));
                        }

                        taunts.Add(t);
                        if(!silent)
                        {
                            Debug.Log(string.Format("  Added {0,-67}  size: {1,6} as an AudioTaunt",
                            file.Name,
                            file.Length
                            ));
                        }

                    }
                }
                stopwatch.Stop();
                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                Debug.Log("Loading the taunts took "+ (stopwatch.ElapsedMilliseconds/1000f).ToString("0.0000")+"s");
            }

            // finds the next client audiotaunt in the taunts list.
            // this is necessary because the taunts list also contains the taunts from other players
            // which would clutter the selection if we were to just select the next index
            public static int GetNextSelectableAudioTauntIndex(int start_index, int direction)
            {
                // nonsensical parameters
                if (start_index < 0 | start_index >= taunts.Count | direction > 1 | direction < -1)
                    return -1;

                // iterate through the taunts till you find another local taunt
                int tmp_index = start_index;
                int count = MPAudioTaunts.AClient.taunts.Count;
                while (MPAudioTaunts.AClient.taunts[tmp_index].is_external_taunt
                    | MPAudioTaunts.AClient.IsContainedInLocalTaunts(MPAudioTaunts.AClient.taunts[tmp_index].hash))
                {
                    tmp_index += direction;
                    // check wether the bounds got violated
                    if (tmp_index < 0)
                        tmp_index = MPAudioTaunts.AClient.taunts.Count - 1;
                    else if (tmp_index >= MPAudioTaunts.AClient.taunts.Count)
                        tmp_index = 0;

                    count--;
                    if (count <= 0) break;
                }

                if (count > 0)
                {
                    return tmp_index;
                }
                return -1;
            }

            public static int GetNextIndexThatStartsWithStringSequence(int start_index, int current_index,string start_sequence)
            {
                if (start_index < 0 | start_index >= taunts.Count | current_index < 0 | current_index >= taunts.Count)
                    return -1;

                if (taunts[current_index].name.ToUpper().StartsWith(start_sequence.ToUpper()))
                {
                    int next_index = GetNextSelectableAudioTauntIndex(current_index, 1);
                    if(taunts[next_index].name.ToUpper().StartsWith(start_sequence.ToUpper()))
                    return next_index;
                }

                for (int i = 0; i < taunts.Count; i++)
                    if(taunts[i].name.ToUpper().StartsWith(start_sequence.ToUpper()) & !taunts[i].is_external_taunt)
                        return i;

                return -1;
            }

            public static bool IsContainedInLocalTaunts(string hash)
            {
                if (String.IsNullOrEmpty(hash))
                    return false;

                bool isContained = false;
                for (int j = 0; j < local_taunts.Length; j++)
                {
                    if (local_taunts[j].hash.Equals(hash))
                    {
                        isContained = true;
                        break;
                    }
                }
                return isContained;
            }

            private static AudioClip LoadAsAudioClip(string filename, string directory_path)
            {
                //Debug.Log("  Attempting to load file as audio clip: " + filename);
                string path = Path.Combine(directory_path, filename);
                if (path != null)
                {
                    WWW www = new WWW("file:///" + path);
                    while (!www.isDone) { }
                    if (string.IsNullOrEmpty(www.error)){
                        return www.GetAudioClip(true, false);
                    }
                    else Debug.Log("Error in 'LoadAsAudioClip': " + www.error + " : " + filename + " : " + directory_path);
                }
                return null;
            }

            public static bool IsKeyCodeAlreadyUsed(int keycode)
            {
                for (int i = 0; i < keybinds.Length; i++)
                {
                    if (keybinds[i] == keycode)
                        return true;
                }
                return false;
            }

            // used to calculate a hash for each audio taunt file to avoid filename collisions
            public static string CalculateMD5ForFile(string path_to_file)
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(path_to_file))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }

            // Splits the hashes that get stored as a single string in loaded_local_taunts and finds the corresponding taunts
            // to populate the 6 audio taunt slots. '/' is used as the seperator of the slots
            public static void LoadLocalAudioTauntsFromPilotPrefs()
            {
                string[] file_hashes = loaded_local_taunts.Split('/');
                int index = 0;
                foreach (string hash in file_hashes)
                {
                    if (index < 6)
                    {
                        AudioTaunt at = taunts.Find(t => t.hash.Equals(hash) && !t.is_external_taunt);
                        if (at == null)
                        {
                            at = new AudioTaunt
                            {
                                hash = "EMPTY",
                                name = "EMPTY",
                                audioclip = null,
                                ready_to_play = false
                            };
                        }
                        local_taunts[index] = at;
                    }
                    index++;
                }
            }

            public static bool IsPlayerMuted(string player_name)
            {
                if (string.IsNullOrEmpty(player_name))
                    return false;

                player_name = player_name.ToUpper();
                foreach(PlayerLobbyData pld in NetworkMatch.m_players.Values)
                {
                    if (pld.m_name.ToUpper().Equals(player_name) & ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Contains(pld.m_player_id))
                        return true;
                }
                return false;
            }

            // the clip_id should only be added when the activation of this audioclip should be shared in a multiplayer match
            public static void PlayAudioTauntFromAudioclip(AudioClip audioClip, string player_name, string clip_id = null)
            {
                if (audio_taunt_volume == 0 | audioClip == null | !active | IsPlayerMuted(player_name))
                    return;

                int index = -1;
                for (int i = 0; i < asc.Length; i++)
                {
                    if (asc[i].source == null)
                        asc[i].source = new GameObject().AddComponent<AudioSource>();

                    if (!asc[i].source.isPlaying) index = i;
                }

                if (index == -1)
                {
                    Debug.Log("Couldnt play Audio taunt. All audio sources are occupied!");
                    return;
                }

                if (player_name != null)
                    asc[index].player_name = player_name;
                else
                    asc[index].player_name = "EMPTY";


                asc[index].source.enabled = true;
                asc[index].source.priority = 0;
                asc[index].source.clip = audioClip;
                asc[index].source.volume = audio_taunt_volume / 100f;
                asc[index].source.bypassReverbZones = true;
                asc[index].source.reverbZoneMix = 0.001f;
                asc[index].source.PlayScheduled(AudioSettings.dspTime);
                asc[index].source.SetScheduledEndTime(AudioSettings.dspTime + TAUNT_PLAYTIME);

                if (GameplayManager.IsMultiplayer
                    && clip_id != null
                    && Client.GetClient() != null
                    && (NetworkMatch.GetMatchState() == MatchState.PLAYING | NetworkMatch.GetMatchState() == MatchState.LOBBY))
                {
                    //Debug.Log("Client -> Server: Share the activation of " + clip_id);
                    Client.GetClient().SendByChannel(MessageTypes.MsgPlayAudioTaunt,
                                        new PlayAudioTaunt
                                        {
                                            hash = clip_id,
                                            sender_name = GameManager.m_local_player.m_mp_name
                                        },0);
                }
            }

            public static float[] calculateFrequencyBand()
            {
                float[] freqBand = new float[8];
                for (int z = 0; z < asc.Length; z++)
                {

                    if (asc[z].source != null && asc[z].source.isPlaying)
                    {
                        float[] samples = new float[512];

                        asc[z].source.GetSpectrumData(samples, 0, FFTWindow.Rectangular);

                        int count = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            float average = 0;
                            int sampleCount = (int)Mathf.Pow(2, i) * 2;

                            if (i == 7) sampleCount += 2;

                            for (int j = 0; j < sampleCount; j++)
                            {
                                average += samples[count] * (count + 1);
                                count++;
                            }

                            average /= count;
                            freqBand[i] = average * 10;
                            freqBand[i] = Mathf.Min(1.6f, freqBand[i]);
                        }



                        return freqBand;
                    }
                }
                return new float[8];
            }

            
            [HarmonyPatch(typeof(UIManager), "PushedDir")]
            class MPAudioTaunts_UIManager_PushedDir
            {
                static bool Prefix(ref bool __result)
                {
                    if (MenuManager.m_menu_state == MenuState.MP_OPTIONS & !UIManager.PushedSelect(100) & MenuManager.m_menu_micro_state == 3 & UIManager.m_menu_selection > 15 & UIManager.m_menu_selection < 22)
                    {
                        if (Input.inputString.ToUpper().Contains('A') | Input.inputString.ToUpper().Contains('D'))//Input.GetKeyDown((KeyCode)'A') | Input.GetKeyDown((KeyCode)'D'))
                        {
                            __result = false;
                            return false; 
                        }
                        else
                            return true;

                    }
                    return true;
                }
            }

            // Send the file names of your audio taunts to the server when entering a game // Client OnAcceptedToLobby OnMatchStart
            [HarmonyPatch(typeof(Client), "OnAcceptedToLobby")]
            class MPAudioTaunts_Client_OnAcceptedToLobby
            {
                static void Postfix()
                {
                    if (GameplayManager.IsDedicatedServer() | Client.GetClient() == null | !active)
                        return;


                    AServer.server_supports_audiotaunts = false;

                    string fileHashes = "";
                    for (int i = 0; i < local_taunts.Length; i++)
                    {
                        if (local_taunts[i].hash != null && !local_taunts[i].hash.Equals("EMPTY"))
                        {
                            fileHashes += local_taunts[i].hash;
                            if (i != local_taunts.Length - 1)
                                fileHashes += "/";
                        }
                    }

                    Client.GetClient().SendByChannel(MessageTypes.MsgShareAudioTauntIdentifiers,
                        new ShareAudioTauntIdentifiers
                        {
                            hashes = fileHashes
                        },0);
                }
            }

            


            
            [HarmonyPatch(typeof(UIElement), "DrawPlayerName")]
            class MPAudioTaunts_UIElement_DrawPlayerName
            {
                public static List<Vector2> curve1 = CalculatePointsOnASphericalCurve(new Vector2(12, -3), new Vector2(12, 3), new Vector2(3, 0), 5);
                public static List<Vector2> curve2 = CalculatePointsOnASphericalCurve(new Vector2(15, -5.5f), new Vector2(15, 5.5f), new Vector2(3, 0), 7);
                public static List<Vector2> curve3 = CalculatePointsOnASphericalCurve(new Vector2(18, -8), new Vector2(18, 8), new Vector2(3, 0), 9);

                static void Postfix(UIElement __instance, Vector2 pos, PlayerLobbyData pld, bool bg_bar, float highlight_width, float name_offset, float max_width = -1f)
                {
                    if (!active | string.IsNullOrEmpty(pld.m_player_id) | pld.m_name.ToUpper().Equals(GameManager.m_local_player.m_mp_name.ToUpper()))
                        return;

                    bool muted = ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Contains(pld.m_player_id);
                    DrawMuteIcon(__instance, pos + (Vector2.right * 20f) + Vector2.right * (-name_offset - 5f), pld, muted); // 20
                }

                public static void DrawMuteIcon(UIElement uie, Vector2 pos, PlayerLobbyData pld, bool muted)
                {
                    uie.TestMouseInRect(pos + new Vector2(5f, 0f), 23f, 15f, 20000 + pld.m_id, true);
                    bool highlighted = UIManager.m_menu_selection == 20000 + pld.m_id;

                    Color color = Color.green;
                    if (muted) color = Color.red;
                    if (highlighted) color = Color.white;
                    
                    if (!muted)
                    {
                        for (int i = 1; i < curve1.Count; i++)
                            UIManager.DrawQuadCenterLine(pos + curve1[i - 1], pos + curve1[i], 0.5f, 0f, color, 4);

                        for (int i = 1; i < curve2.Count; i++)
                            UIManager.DrawQuadCenterLine(pos + curve2[i - 1], pos + curve2[i], 0.5f, 0f, color, 4);
                        
                        for (int i = 1; i < curve3.Count; i++)
                            UIManager.DrawQuadCenterLine(pos + curve3[i - 1], pos + curve3[i], 0.5f, 0f, color, 4);
                    }
                    else
                    {
                        // 5.497790f = 315°, 0.785398f = 45°
                        UIManager.DrawSpriteUIRotated(pos + new Vector2(12.4f, 0f), 0.15f, 0.15f, 5.497790f, color, 0.5f, 41);
                        UIManager.DrawSpriteUIRotated(pos + new Vector2(12.4f, 0f), 0.15f, 0.15f, 0.785398f, color, 0.5f, 41);
                    }

                    // 81 = triangle, 131 = cross, 199 = block, 41 = shortest_line, [6,7,34] = border, 11 = clean block
                    UIManager.DrawSpriteUI(pos, 0.18f, 0.18f, color, 0.5f, 199);
                    pos.x += 3f;
                    UIManager.DrawSpriteUI(pos, 0.18f, 0.18f, color, 0.5f, 81);
                }

                public static List<Vector2> CalculatePointsOnASphericalCurve(Vector2 start, Vector2 end, Vector2 center, int amt_lines)
                {
                    List<Vector2> points = new List<Vector2>();
                    points.Add(start);

                    float radius = (float)Math.Sqrt(Math.Pow(start.x - center.x, 2) + Math.Pow(start.y - center.y, 2));
                    float angle1 = (float)Math.Atan2(start.y - center.y, start.x - center.x);
                    float angle2 = (float)Math.Atan2(end.y - center.y, end.x - center.x);
                    float angleDelta = (angle2 - angle1) / amt_lines;

                    for (int i = 0; i < amt_lines; i++)
                    {
                        float angle = angle1 + i * angleDelta;
                        float x = center.x + radius * (float)Math.Cos(angle);
                        float y = center.y + radius * (float)Math.Sin(angle);
                        Vector2 point = new Vector2(x, y);
                        points.Add(point);
                    }
                    points.Add(end);
                    return points;
                }
            }

            

            [HarmonyPatch(typeof(MenuManager), "MpPreMatchMenuUpdate")]
            class MPAudioTaunts_MenuManager_MpPreMatchMenuUpdate
            {
                static void Postfix()
                {
                    foreach(PlayerLobbyData p in NetworkMatch.m_players.Values)
                    {
                        if(UIManager.PushedSelect(100) && UIManager.m_menu_selection == 20000 + p.m_id)
                        {
                            if (string.IsNullOrEmpty(p.m_player_id))
                                continue;

                            if (ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Contains(p.m_player_id))
                                ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Remove(p.m_player_id);
                            else
                                ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Add(p.m_player_id);
                            MenuManager.PlaySelectSound(1f);
                        }
                    }
                }
            }



            [HarmonyPatch(typeof(GameManager), "Update")]
            class MPAudioTaunts_GameManager_Update
            {
                public static void Postfix()
                {
                    if (!GameplayManager.IsDedicatedServer() && active)
                    {
                        // Checks wether the slot for uploading a taunt to the server is free and if so queues the next potential upload
                        if (!MPAudioTaunts_Client_RegisterHandlers.isUploading && MPAudioTaunts_Client_RegisterHandlers.queued_uploads.Count > 0)
                        {
                            GameManager.m_gm.StartCoroutine(MPAudioTaunts_Client_RegisterHandlers.UploadAudioTauntToServer(MPAudioTaunts_Client_RegisterHandlers.queued_uploads[0]));
                            MPAudioTaunts_Client_RegisterHandlers.queued_uploads.Remove(MPAudioTaunts_Client_RegisterHandlers.queued_uploads[0]);
                        }

                        //uConsole.Log("Menustate: "+ MenuManager.m_menu_state.ToString());

                        // Checks for Keyinput on this client for triggering the playing of an audio taunt
                        if (!uConsole.IsOn() & AServer.server_supports_audiotaunts & !PlayerShip.m_typing_in_chat & (GameplayManager.IsMultiplayerActive | MenuManager.m_menu_state == MenuState.MP_PRE_MATCH_MENU))
                        {
                            if (remaining_cooldown > 0f)
                                remaining_cooldown -= Time.unscaledDeltaTime;

                            for (int i = 0; i < 6; i++)
                            {
                                if (remaining_cooldown <= 0f && keybinds[i] > 0 && (Input.GetKeyDown((KeyCode)keybinds[i]) && local_taunts[i].audioclip != null || Controls.JustPressed((CCInput)(61+i))))
                                {
                                    remaining_cooldown = DEFAULT_TAUNT_COOLDOWN;
                                    PlayAudioTauntFromAudioclip(local_taunts[i].audioclip, GameManager.m_local_player.m_mp_name, local_taunts[i].hash);
                                    break;
                                }
                            }
                        }

                        if (display_audio_spectrum)
                        {
                            spectrum_update_cooldown -= Time.unscaledDeltaTime;
                            if (spectrum_update_cooldown <= 0f)
                            {
                                for (int i = 0; i < asc.Length; i++)
                                {
                                    asc[i].UpdateFrequencyBand();
                                }
                                spectrum_update_cooldown = DEFAULT_SPECTRUM_UPDATE_COOLDOWN;
                            }
                        }

                    }
                }
            }


            public static void DrawVisualIndicator(UIElement __instance)
            {
                // Draws an indicator of who is currently playing an audiotaunt
                if ((NetworkMatch.GetMatchState() == MatchState.PLAYING | NetworkMatch.GetMatchState() == MatchState.LOBBY) & MenuManager.m_menu_state != MenuState.MP_OPTIONS)
                {
                    //uConsole.Log(UIManager.m_mouse_pos.x + " : " + UIManager.m_mouse_pos.y);

                    int position = -1;
                    for (int i = 0; i < asc.Length; i++)
                    {
                        if (asc[i].source == null)
                            asc[i].source = new GameObject().AddComponent<AudioSource>();

                        if (asc[i].source != null & asc[i].player_name != null && asc[i].source.isPlaying)
                        {
                            position++;


                            // draw an indicator for actively playing audio taunts
                            Vector2 pos = new Vector2(-600, -40);

                            // shift the element down depending on how many audio sources are playing at the same time
                            pos.y += position * 32;



                            // draw the player name
                            __instance.DrawStringSmall(asc[i].player_name + (display_audio_spectrum ? ":" : ""), pos, 0.32f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);

                            // draw the frequency band
                            if (display_audio_spectrum)
                            {
                                // check wether there is a non zero change to the default values
                                bool should_display = false;
                                for (int x = 0; x < 8; x++)
                                {
                                    if (asc[i].frequency_band[x] != -1)
                                        should_display = true;
                                }

                                if (should_display)
                                {
                                    pos.x += UIManager.GetStringWidth(asc[i].player_name + ":", 8f);
                                    pos.y += 3f;
                                    for (int x = 0; x < 8; x++)
                                    {
                                        // new Vector2(1.7f, 1f)
                                        UIManager.DrawBarVertical(pos, new Vector2(2f, 1.2f), asc[i].frequency_band[x] * 20f, Color.green * 0.7f, 199);//UIManager.DrawQuadBarVertical(pos, 6f, 1f, freqBand[i] * 50f, Color.yellow, 199); //(pos, new Vector2(pos.x, -freqBand[i] * 200f), 1f, 0f, Color.yellow, 4);
                                        pos.x += 5.5f;
                                    }
                                }
                            }




                        }
                    }
                }
            }

            [HarmonyPatch(typeof(UIElement), "DrawHUD")]
            class MPAudioTaunts_UIElement_DrawHUD
            {
                static void Postfix(UIElement __instance)
                {
                    DrawVisualIndicator(__instance);
                }

            }

            [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
            class MPAudioTaunts_UIElement_DrawMpPreMatchMenu
            {
                static void Postfix(UIElement __instance)
                {
                    DrawVisualIndicator(__instance);
                }
            }


            [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
            class MPDeathReview_UIElement_DrawMpMiniScoreboard
            {
                static void Postfix(UIElement __instance)
                {
                    if (!MPDeathReview.stickyDeathReview | !MPDeathReview.showDeathReviewDetails)
                    {
                        DrawVisualIndicator(__instance);
                    }
                }
            }


            [HarmonyPatch(typeof(Client), "RegisterHandlers")]
            class MPAudioTaunts_Client_RegisterHandlers
            {

                public static bool isUploading = false;
                public static List<AudioTaunt> queued_uploads = new List<AudioTaunt>();

                private static void OnShareAudioTauntIdentifiers(NetworkMessage rawMsg)
                {
                    if (!active) return;

                    Debug.Log("[AudioTaunts]  Received AudioTauntIdentifiers from Server");
                    var msg = rawMsg.ReadMessage<ShareAudioTauntIdentifiers>();
                    List<string> file_hashes = msg.hashes.Split('/').ToList();
                    foreach (string hash in file_hashes)
                        Debug.Log(hash);


                    ImportAudioTaunts(ExternalAudioTauntDirectory, file_hashes);
                    
                    foreach (string hash in file_hashes)
                    {

                        // if the taunt has been downloaded before then load it locally and add it to the game taunts
                        if (!string.IsNullOrEmpty(hash) & !match_taunts.ContainsKey(hash))
                        {

                            AudioTaunt taunt = taunts.Find(t => t.hash.Equals(hash));
                            if (taunt != null)
                            {
                                Debug.Log("     [AudioTaunts]  Found Audiotaunt in the local data: " + hash);
                                match_taunts.Add(hash, taunt);
                            }
                            else
                            {
                                Debug.Log("     [AudioTaunts]  Requesting Audiotaunt from Server: " + hash);
                                if (Client.GetClient() == null)
                                {
                                    Debug.Log("     [AudioTaunts]  MPAudioTaunts_Client_RegisterHandlers: no client?");
                                    continue;
                                }
                                // to prepare for an incoming answer to our request we create a context to collect the bytes from the server
                                match_taunts.Add(hash, new AudioTaunt
                                {
                                    hash = hash,
                                    name = "",
                                    audio_taunt_data = new byte[AUDIO_TAUNT_SIZE_LIMIT],
                                    is_data_complete = false,
                                    timestamp = -1,
                                    ready_to_play = false,
                                    requested_taunt = true
                                });

                                Client.GetClient().SendByChannel(MessageTypes.MsgRequestAudioTaunt,
                                    new RequestAudioTaunt
                                    {
                                        hash = hash
                                    },0);

                            }

                        }
                    }
                }

                private static void OnAudioTauntRequest(NetworkMessage rawMsg)
                {
                    if (!active) return;


                    var msg = rawMsg.ReadMessage<RequestAudioTaunt>();
                    Debug.Log("[AudioTaunts.Client]  Server requested audiotaunt: " + msg.hash);

                    // find the audiotaunt data that the server requested
                    int index = -1;
                    for (int i = 0; i < AMOUNT_OF_TAUNTS_PER_CLIENT; i++)
                    {
                        if (local_taunts[i].hash.Equals(msg.hash))
                            index = i;
                    }

                    // start the upload if we actually have the data
                    if (index != -1)
                    {
                        if (local_taunts[index].is_data_complete)
                        {
                            Debug.Log("[AudioTaunts]  starting the upload or putting it in the queue: " + msg.hash);
                            if (!isUploading)
                                GameManager.m_gm.StartCoroutine(UploadAudioTauntToServer(new AudioTaunt
                                {
                                    hash = msg.hash,
                                    audio_taunt_data = local_taunts[index].audio_taunt_data
                                }));
                            else
                                queued_uploads.Add(new AudioTaunt
                                {
                                    hash = msg.hash,
                                    audio_taunt_data = local_taunts[index].audio_taunt_data
                                });
                        }
                        else
                        {
                            Debug.Log(" Local taunt byte data is declared as incomplete!");
                        }
                    }
                    else
                    {
                        Debug.Log(" The requested audiotaunt isnt part of the selected local taunts");
                    }
                }

                public static IEnumerator UploadAudioTauntToServer(AudioTaunt data)
                {
                    isUploading = true;
                    Debug.Log("[AudioTaunts] Started uploading an AudioTaunt to server");
                    int _packet_id = 0;
                    int position = 0;
                    while (position < data.audio_taunt_data.Length)
                    {
                        // write a packet
                        int index = 0;
                        byte[] to_send = new byte[PACKET_PAYLOAD_SIZE];
                        while (index < PACKET_PAYLOAD_SIZE & ((position + index) < data.audio_taunt_data.Length))
                        {
                            to_send[index] = data.audio_taunt_data[position + index];
                            index++;
                        }

                        AudioTauntPacket packet = new AudioTauntPacket
                        {
                            filesize = data.audio_taunt_data.Length,
                            amount_of_bytes_sent = index,
                            hash = data.hash,
                            packet_id = _packet_id,
                            data = to_send,//Convert.ToBase64String(to_send)
                        };

                        Client.GetClient().connection.SendByChannel(MessageTypes.MsgAudioTauntPacket, packet,0);
                       
                        yield return delay;
                        position += PACKET_PAYLOAD_SIZE;
                        _packet_id++;
                    }
                    uConsole.Log("[AudioTaunts]    completed the upload for " + data.hash);
                    isUploading = false;
                }

                private static void OnAudioTauntPacket(NetworkMessage rawMsg)
                {
                    if (!active)
                        return;

                    //Debug.Log("[AudioTaunts]        Received a Fragment of an Audiotaunt");

                    try
                    {
                        var msg = rawMsg.ReadMessage<AudioTauntPacket>();
                        if (match_taunts.ContainsKey(msg.hash))
                        {
                            // initialise the array with the correct size
                            if (match_taunts[msg.hash].timestamp == -1)
                            {
                                match_taunts[msg.hash].timestamp = Time.frameCount;
                                match_taunts[msg.hash].audio_taunt_data = new byte[msg.filesize];
                            }

                            if (match_taunts[msg.hash].received_packets == null)
                                match_taunts[msg.hash].received_packets = new List<int>();

                            if (!match_taunts[msg.hash].received_packets.Contains(msg.packet_id))
                            {
                                // add data of the fragment to the context
                                int startindex = msg.packet_id * PACKET_PAYLOAD_SIZE;
                                for (int i = 0; i < msg.amount_of_bytes_sent; i++)
                                {
                                    match_taunts[msg.hash].audio_taunt_data[startindex + i] = msg.data[i];
                                }
                                match_taunts[msg.hash].received_packets.Add(msg.packet_id);
                                //Debug.Log("     Added the data of the fragment");

                                // if this completes the data then check wether there are unanswered requests
                                if (match_taunts[msg.hash].received_packets.Count * PACKET_PAYLOAD_SIZE >= msg.filesize)
                                {
                                    //DEBUG.MARKER Debug.Log("     This Fragment completed the audio taunt data");
                                    match_taunts[msg.hash].is_data_complete = true;



                                    // write the byte data to a file
                                    string path = Path.Combine(ExternalAudioTauntDirectory, msg.hash + ".ogg");

                                    File.WriteAllBytes(path, match_taunts[msg.hash].audio_taunt_data);

                                    match_taunts[msg.hash].audioclip = LoadAsAudioClip(msg.hash + ".ogg", ExternalAudioTauntDirectory);
                                    match_taunts[msg.hash].ready_to_play = true;


                                }
                            }
                            else
                            {
                                //DEBUG.MARKER Debug.Log("     This Fragment is a duplicate. packet_id: " + msg.packet_id);
                            }

                        }
                        else
                        {
                            //DEBUG.MARKER Debug.Log(" There is no context for this audiotaunt, ignoring the fragment, connectionID:" + rawMsg.conn.connectionId);
                        }
                    }
                    catch (FormatException fe)
                    {
                        Debug.Log(" (Marker.UnusualEvent): Received a fragment with a payload that doesnt format into a Base64 String!\n" + fe);
                        return;
                    }


                }

                private static void OnPlayAudioTaunt(NetworkMessage rawMsg)
                {
                    if (!active) return;

                    var msg = rawMsg.ReadMessage<PlayAudioTaunt>();

                    if (match_taunts.ContainsKey(msg.hash) && match_taunts[msg.hash].ready_to_play)
                    {
                        PlayAudioTauntFromAudioclip(match_taunts[msg.hash].audioclip, msg.sender_name);
                    }
                    // If someone tries to play a taunt that we dont even know existed before then we missed the ShareAudioTauntIdentifiers packet
                    else if (!match_taunts.ContainsKey(msg.hash))
                    {
                        Debug.Log("[AudioTaunts] Client was unaware of a taunts existence. Resending MsgShareAudioTauntIdentifiers!");
                        string fileHashes = "";
                        for (int i = 0; i < local_taunts.Length; i++)
                        {
                            if (local_taunts[i].hash != null && !local_taunts[i].hash.Equals("EMPTY"))
                            {
                                fileHashes += local_taunts[i].hash;
                                if (i != local_taunts.Length - 1)
                                    fileHashes += "/";
                            }
                        }

                        Client.GetClient().SendByChannel(MessageTypes.MsgShareAudioTauntIdentifiers,
                            new ShareAudioTauntIdentifiers
                            {
                                hashes = fileHashes
                            }, 0);
                    }
                }



                static void Postfix()
                {
                    if (GameplayManager.IsDedicatedServer())
                        return;

                    if (Client.GetClient() == null)
                    {
                        Debug.Log("Couldnt setup MessageHandlers for Audiotaunts on the Client");
                        return;
                    }

                    Client.GetClient().RegisterHandler(MessageTypes.MsgShareAudioTauntIdentifiers, OnShareAudioTauntIdentifiers);
                    Client.GetClient().RegisterHandler(MessageTypes.MsgRequestAudioTaunt, OnAudioTauntRequest);
                    Client.GetClient().RegisterHandler(MessageTypes.MsgAudioTauntPacket, OnAudioTauntPacket);
                    Client.GetClient().RegisterHandler(MessageTypes.MsgPlayAudioTaunt, OnPlayAudioTaunt);
                }
            }
        }















        // Responsible for sharing the required audiotaunts and their activation across clients
        public static class AServer
        {
            public static bool active = true;
            public static bool server_supports_audiotaunts = false;

            public static Dictionary<string, AudioTaunt> match_taunts = new Dictionary<string, AudioTaunt>();
            public static Dictionary<string, AudioTaunt> taunt_buffer = new Dictionary<string, AudioTaunt>(); // this holds taunts whose data has been uploaded 


            [HarmonyPatch(typeof(Server), "OnDisconnect")]
            class MPAudioTaunts_Server_OnDisconnect
            {
                static void Prefix(NetworkMessage msg)
                {
                    if (msg == null || msg.conn == null)
                    {
                        Debug.Log(" [Audiotaunts.Server] received a OnDisconnect event but the network message object didnt exist");
                        return;
                    }

                    // remove taunts where this client is the sole provider from
                    Dictionary<string, AudioTaunt> new_match_taunts = new Dictionary<string, AudioTaunt>();
                    foreach (var item in match_taunts)
                    {
                        bool sole_provider = true;
                        foreach (int provider in match_taunts[item.Key].providing_clients)
                        {
                            if (provider != msg.conn.connectionId)
                                sole_provider = false;
                        }

                        if (!sole_provider & !new_match_taunts.ContainsKey(item.Key))
                        {
                            new_match_taunts.Add(item.Key, item.Value);
                        }

                    }
                    match_taunts = new_match_taunts;
                }
            }

            // resets match specific informations
            [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
            class MPAudioTaunts_NetworkMatch_InitBeforeEachMatch
            {
                static void Postfix()
                {
                    if (!GameplayManager.IsDedicatedServer())
                        return;


                    string content = "\n";
                    foreach (var item in match_taunts)
                    {
                        content += "    " + item.Key + "\n";
                    }

                    string content2 = "\n";
                    foreach (var item in taunt_buffer)
                    {
                        content2 += "    " + item.Key + "\n";
                    }

                    match_taunts.Clear();
                    Debug.Log("[AudioTaunts] Initialisation:"
                        + "\n[match_taunt]: " + match_taunts.Count
                        + "\n" + content
                        + "\n[buffered taunts]: " + taunt_buffer.Count
                        + "\n" + content2
                        );
                }
            }

            [HarmonyPatch(typeof(Server), "RegisterHandlers")]
            class MPAudioTaunts_Server_RegisterHandlers
            {
                private static void OnShareAudioTauntIdentifiers(NetworkMessage rawMsg)
                {


                    try
                    {
                        // Read the message
                        Debug.Log("[Audiotaunt] : Received AudioTauntIdentifiers from Client");
                        var msg = rawMsg.ReadMessage<ShareAudioTauntIdentifiers>();


                        if (string.IsNullOrEmpty(msg.hashes))
                        {
                            Debug.Log(" (Marker:UnusualEvent) the OnShareAudioTauntIdentifiers message was empty!");
                            return;
                        }

                        // Split the string to retrieve the individual hashes
                        List<string> file_hashes = msg.hashes.Split('/').ToList();
                        Debug.Log(file_hashes.Count);
                        foreach (string hash in file_hashes)
                        {
                            Debug.Log(" hash:" + hash);
                        }
                        Debug.Log("");

                        // Update match_taunts
                        int index = 0;
                        foreach (string hash in file_hashes)
                        {
                            if (hash == null || hash.Length < 5)
                                continue;

                            if (!string.IsNullOrEmpty(hash))
                            {
                                if (hash.StartsWith(" "))
                                    continue;

                                try
                                {

                                    // this taunt already exists, add the sender as a possible provider
                                    if (match_taunts.ContainsKey(hash))
                                    {

                                        if (match_taunts[hash].providing_clients == null)
                                        {
                                            match_taunts[hash].providing_clients = new List<int>();
                                        }
                                        if(!match_taunts[hash].providing_clients.Contains(rawMsg.conn.connectionId))
                                        {
                                            match_taunts[hash].providing_clients.Add(rawMsg.conn.connectionId);
                                            Debug.Log("  match_taunts:" + hash + " exists, added: " + rawMsg.conn.connectionId + " as a provider");
                                        } 
                                    }
                                    // this taunt doesnt exist, add it
                                    else
                                    {
                                        AudioTaunt taunt = new AudioTaunt
                                        {
                                            hash = hash,
                                            name = "EMPTY",
                                            audio_taunt_data = new byte[AUDIO_TAUNT_SIZE_LIMIT],
                                            is_data_complete = false,
                                            timestamp = -1,
                                            ready_to_play = false,
                                            requested_taunt = false,
                                            audioclip = null,
                                            providing_clients = new List<int>(),
                                            requesting_clients = new List<int>(),
                                            received_packets = new List<int>()
                                        };
                                        taunt.providing_clients.Add(rawMsg.conn.connectionId);
                                        match_taunts.Add(hash, taunt);
                                        Debug.Log("  Added new context to match_taunts: " + hash);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.Log("Server.OnShareAudioTaunts encountered an exception"
                                        + "\n loop:" + index
                                        + "\n hash:" + hash
                                        + "\n Exception: " + ex
                                        );
                                }

                            }
                            index++;
                        }

                        try
                        {
                            string hashes = "";
                            int count = 0;
                            foreach (var t in match_taunts)
                            {
                                if (t.Value != null && t.Value.hash != null)
                                {
                                    hashes += t.Value.hash;
                                    count++;
                                    if (count < match_taunts.Count)
                                        hashes += "/";
                                }
                                else
                                {
                                    Debug.Log("Server.OnShareAudiotaunts: encountered an empty element in the match taunts");
                                }
                            }

                            //Send the updated list of hashes to all connected clients that support audiotaunts
                            foreach (NetworkConnection networkConnection in NetworkServer.connections)
                            {
                                if (networkConnection != null && MPTweaks.ClientHasTweak(networkConnection.connectionId, "audiotaunts"))
                                    networkConnection.Send(MessageTypes.MsgShareAudioTauntIdentifiers, new ShareAudioTauntIdentifiers { hashes = hashes });
                            }
                        }
                        catch(Exception ex)
                        {
                            Debug.Log(" (Marker:UnusualEvent[ 1 ]) in Server.OnShareAudioTauntIdentifiers: " + ex);
                        }



                       
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(" (Marker:UnusualEvent[ 2 ]) in Server.OnShareAudioTauntIdentifiers: "+ex);
                        return;
                    }


                }


                private static void OnAudioTauntRequest(NetworkMessage rawMsg)
                {
                    try
                    {
                        var msg = rawMsg.ReadMessage<RequestAudioTaunt>();
                        Debug.Log("[AudioTaunts] Received audio taunt file request");
                        string hash = msg.hash;

                        AudioTaunt taunt = null;
                        if (taunt_buffer.ContainsKey(hash))
                        {
                            taunt = taunt_buffer[hash];
                        }
                        else if (match_taunts.ContainsKey(hash))
                        {
                            taunt = match_taunts[hash];
                        }


                        if (taunt != null)
                        {
                            // if its byte data is populated send that to the client that requested it
                            if (taunt.is_data_complete)
                            {
                                // start upload
                                GameManager.m_gm.StartCoroutine(UploadAudioTauntToClient(hash, taunt.audio_taunt_data, rawMsg.conn.connectionId));
                            }

                            // if it is not then request that data from the client the shared this audiotaunt
                            // and add Client A to the request list
                            else
                            {
                                foreach (NetworkConnection networkConnection in NetworkServer.connections)
                                {
                                    if (networkConnection != null && taunt.providing_clients.Count > 0 && networkConnection.connectionId == taunt.providing_clients[0])
                                    {
                                        networkConnection.SendByChannel(MessageTypes.MsgRequestAudioTaunt, new RequestAudioTaunt { hash = hash }, 0);
                                        Debug.Log(" [SERVER]: Requested AudioTaunt data from: " + networkConnection.connectionId + " for: " + rawMsg.conn.connectionId + " hash: "+hash );
                                    }
                                }
                                if (taunt.requesting_clients == null)
                                    taunt.requesting_clients = new List<int>();

                                taunt.requesting_clients.Add(rawMsg.conn.connectionId);
                            }
                        }
                        else
                        {
                            Debug.Log("  [SERVER]: A client requested an audiotaunt that has no context on the server");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(" (Marker:UnusualEvent) encountered an exception when processing the network request: " + ex);
                    }
                }

                public static IEnumerator UploadAudioTauntToClient(string hash, byte[] data, int connectionId)
                {
                    Debug.Log("[AudioTaunts] Started uploading AudioTaunt to client");
                    bool ended_early = false;
                    if (data.Length < PACKET_PAYLOAD_SIZE)
                    {
                        AudioTauntPacket packet = new AudioTauntPacket
                        {
                            filesize = data.Length,
                            amount_of_bytes_sent = data.Length,
                            hash = hash,
                            packet_id = 0,
                            data = data,
                        };
                        NetworkServer.SendToClient(connectionId, MessageTypes.MsgAudioTauntPacket, packet);
                    }
                    else
                    {
                        int position = 0;
                        int packet_id = 0;
                        while (position < data.Length)
                        {
                            // stop the transmission if the game is over
                            if(NetworkMatch.m_match_state == MatchState.POSTGAME | NetworkMatch.m_match_state == MatchState.SCOREBOARD)
                            {
                                ended_early = true;
                                break;
                            }

                            int index = 0;
                            byte[] to_send = new byte[PACKET_PAYLOAD_SIZE];

                            while (index < PACKET_PAYLOAD_SIZE & ((position + index) < data.Length))
                            {
                                to_send[index] = data[position + index];
                                index++;
                            }
                            AudioTauntPacket packet = new AudioTauntPacket
                            {
                                filesize = data.Length,
                                amount_of_bytes_sent = index,
                                hash = hash,
                                packet_id = packet_id,
                                data = to_send,
                            };
                            NetworkServer.SendToClient(connectionId, MessageTypes.MsgAudioTauntPacket, packet);
                            position += PACKET_PAYLOAD_SIZE;
                            packet_id++;
                            yield return null;
                        }
                        Debug.Log("[AudioTaunts]   completed the upload to "+connectionId+" for " + hash);
                    }
                    if (!ended_early)
                        Debug.Log("[AudioTaunts] successfully transmitted taunt to client" + hash);
                    else
                        Debug.Log("[AudioTaunts] stopped the transmission of an audiotaunt due to reaching the end of the match "+hash);               
                }



                private static void OnAudioTauntPacket(NetworkMessage rawMsg)
                {
                    if (!active)
                        return;
                    try
                    {

                        //Debug.Log("[AudioTaunts]        Received a Fragment of an Audiotaunt");
                        var msg = rawMsg.ReadMessage<AudioTauntPacket>();

                        if (msg.data == null)
                            return;

                        if (match_taunts.ContainsKey(msg.hash))
                        {
                            // initialise the array with the correct size
                            if (match_taunts[msg.hash].timestamp == -1)
                            {
                                match_taunts[msg.hash].timestamp = Time.frameCount;
                                match_taunts[msg.hash].audio_taunt_data = new byte[msg.filesize];
                            }

                            if (!match_taunts[msg.hash].received_packets.Contains(msg.packet_id))
                            {
                                // add data of the fragment to the context
                                int startindex = msg.packet_id * PACKET_PAYLOAD_SIZE;
                                for (int i = 0; i < msg.amount_of_bytes_sent; i++)
                                {
                                    match_taunts[msg.hash].audio_taunt_data[startindex + i] = msg.data[i];
                                }
                                match_taunts[msg.hash].received_packets.Add(msg.packet_id);
                                //Debug.Log("     Added the data of the fragment");

                                // if this completes the data then check wether there are unanswered requests
                                if (match_taunts[msg.hash].received_packets.Count * PACKET_PAYLOAD_SIZE >= msg.filesize)
                                {
                                    //Debug.Log("     This Fragment completed the audio taunt data");
                                    match_taunts[msg.hash].is_data_complete = true;

                                    Debug.Log("Added a taunt to the taunt buffer: "+msg.hash);
                                    if(!taunt_buffer.ContainsKey(msg.hash))
                                    {
                                        taunt_buffer.Add(msg.hash, new AudioTaunt
                                        {
                                            hash = msg.hash,
                                            name = "",
                                            audio_taunt_data = match_taunts[msg.hash].audio_taunt_data,
                                            is_data_complete = true,
                                            timestamp = -1,
                                            ready_to_play = false,
                                            requested_taunt = false
                                        });
                                    }
                                    else
                                        Debug.Log(" BUG: The taunt was already added to the buffer! This should not happen: " + msg.hash);

                                    List<int> duplicate_free_requests = new List<int>();
                                    foreach (int connectionId in match_taunts[msg.hash].requesting_clients)
                                    {
                                        if(!duplicate_free_requests.Contains(connectionId))
                                        {
                                            duplicate_free_requests.Add(connectionId);
                                            Debug.Log("         Uploading Requested Taunt to: " + connectionId);
                                            GameManager.m_gm.StartCoroutine(UploadAudioTauntToClient(msg.hash, match_taunts[msg.hash].audio_taunt_data, connectionId));
                                        }
                                    }

                                }
                            }
                            else
                            {
                                Debug.Log("     This Fragment is a duplicate. packet_id: " + msg.packet_id);
                            }

                        }
                        else
                        {
                            Debug.Log(" There is no context for this audiotaunt, ignoring the fragment, connectionID:" + rawMsg.conn.connectionId);
                        }
                    }
                    catch(Exception ex)
                    {
                        Debug.Log(" (Marker:UnusualEvent) encountered an exception in Server.OnAudioTauntPacket: " + ex);
                    }
                }


                private static void OnPlayAudioTaunt(NetworkMessage rawMsg)
                {
                    try
                    {
                        var msg = rawMsg.ReadMessage<PlayAudioTaunt>();

                        if (string.IsNullOrEmpty(msg.hash) | string.IsNullOrEmpty(msg.sender_name))
                        {
                            Debug.Log(" (Marker:UnusualEvent) the Server.OnPlayAudioTaunt msg was incomplete! client with a testversion ?: " + MPTweaks.ClientInfos[rawMsg.conn.connectionId]);
                            return;
                        }

                        //Debug.Log("     msg.hash: " + msg.hash + "\n  msg.sender: " + msg.sender_name);

                        if (match_taunts != null && match_taunts.ContainsKey(msg.hash))
                        {
                            // distribute it to all other clients
                            PlayAudioTaunt packet = new PlayAudioTaunt
                            {
                                hash = msg.hash,
                                sender_name = msg.sender_name
                            };

                            foreach (NetworkConnection networkConnection in NetworkServer.connections)
                            {
                                if (networkConnection != null & rawMsg.conn.connectionId != networkConnection.connectionId & MPTweaks.ClientHasTweak(networkConnection.connectionId, "audiotaunts"))
                                    networkConnection.SendByChannel(MessageTypes.MsgPlayAudioTaunt, packet, 0);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // This is necessary because there are 7 test versions in circulation with different formatting for their network messages
                    }
                }

                static void Postfix()
                {
                    NetworkServer.RegisterHandler(MessageTypes.MsgShareAudioTauntIdentifiers, OnShareAudioTauntIdentifiers);
                    NetworkServer.RegisterHandler(MessageTypes.MsgRequestAudioTaunt, OnAudioTauntRequest);
                    NetworkServer.RegisterHandler(MessageTypes.MsgAudioTauntPacket, OnAudioTauntPacket);
                    NetworkServer.RegisterHandler(MessageTypes.MsgPlayAudioTaunt, OnPlayAudioTaunt);
                }
            }
        }

        public class ShareAudioTauntIdentifiers : MessageBase
        {
            public string hashes;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(hashes);
            }
            public override void Deserialize(NetworkReader reader)
            {
                reader.SeekZero();
                hashes = reader.ReadString();
            }
        }

        public class RequestAudioTaunt : MessageBase
        {
            public string hash;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(hash);
            }
            public override void Deserialize(NetworkReader reader)
            {
                reader.SeekZero();
                hash = reader.ReadString();
            }
        }

        public class PlayAudioTaunt : MessageBase
        {
            public string hash;
            public string sender_name = "";

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(hash + "/" + sender_name);
            }
            public override void Deserialize(NetworkReader reader)
            {
                try
                {
                    reader.SeekZero();
                    var msg = reader.ReadString().Split('/');
                    hash = msg[0];
                    sender_name = msg[1];
                }
                catch(Exception)
                {
                    // This is necessary because there are 7 test versions in circulation with different formatting for their network messages
                }
            }
        }

        public class Ack : MessageBase
        {
            public string identifier;
            public int packet_id;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(packet_id.ToString() + "/" + identifier);
            }
            public override void Deserialize(NetworkReader reader)
            {
                reader.SeekZero();
                var msg = reader.ReadString().Split('/');
                packet_id = int.Parse(msg[0]);
                identifier = msg[1];
            }
        }



        public class AudioTauntPacket : MessageBase
        {
            public int filesize;
            public int amount_of_bytes_sent;
            public string hash;
            public int packet_id;
            public byte[] data;

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(packet_id.ToString() + "/"
                    + filesize.ToString() + "/"
                    + amount_of_bytes_sent.ToString() + "/"
                    + hash + "/"
                    + Convert.ToBase64String(data)
                    );
            }
            public override void Deserialize(NetworkReader reader)
            {
                //reader.SeekZero();
                string msg = reader.ReadString();

                //Debug.Log("CONTENT OF PACKET:\n"+msg);

                int pos = msg.IndexOf("/");
                packet_id = int.Parse(msg.Substring(0, pos));
                msg = msg.Remove(0, pos + 1);

                pos = msg.IndexOf("/");
                filesize = int.Parse(msg.Substring(0, pos));
                msg = msg.Remove(0, pos + 1);

                pos = msg.IndexOf("/");
                amount_of_bytes_sent = int.Parse(msg.Substring(0, pos));
                msg = msg.Remove(0, pos + 1);

                pos = msg.IndexOf("/");
                hash = msg.Substring(0, pos);
                msg = msg.Remove(0, pos + 1);
                try
                {
                    data = Convert.FromBase64String(msg);
                }
                catch(Exception ex)
                {
                    Debug.Log($"[AudioTaunts] AudioTauntPacket.Deserialize: data couldn't format properly: {ex.ToString()}");
                }
                
            }
        }



    }
}
