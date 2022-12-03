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

namespace GameMod
{
    class MPAudioTaunts
    {
        // DEBUG
        [HarmonyPatch(typeof(GameManager), "Start")]
        internal class DebugCommandsPatch
        {
            private static void Postfix(GameManager __instance)
            {
                uConsole.RegisterCommand("q", "lets you test shiny soundeffects", new uConsole.DebugCommand(CmdPlaySoundEffect));
                uConsole.RegisterCommand("b1", "", new uConsole.DebugCommand(testBool1));
                uConsole.RegisterCommand("b2", "", new uConsole.DebugCommand(testBool2));
            }


            private static void CmdPlaySoundEffect()
            {
                int effect_index = uConsole.GetInt();
                GameManager.m_audio.PlayCue2D(effect_index, 0.5f, 0.07f, 0f, false);
            }
            private static void testBool1()
            {
                uConsole.Log((GameplayManager.m_gameplay_state == GameplayState.PLAYING).ToString());

            }
            private static void testBool2()
            {
                uConsole.Log((GameplayManager.m_game_type == GameType.MULTIPLAYER).ToString());
            }
        }










        /* TODO:
         * - Handle clients leaving
         * - make a button the completly deactivates all of this code and stops receiving network messages
         * - add a visual indicator when somebody is playing a taunt and display the audio spectrum
         * - write a handler that dynamically schedules downloads/uploads and adjusts the packet size based on the resend rate
         * - replace the AudioTaunt.received_packet list with a dynamically sized bool array 
         * - remove all provider ids from the server match_taunts when the game ends or a client disconnects
         * - make joystick keys bindable
         * - fix that external taunts are selectable in the menu
         * - Add uploader connection id update when a client leaves
         */

        public const int AUDIO_TAUNT_SIZE_LIMIT = 131072;           // 128 kB, maximum allowed audio taunt file size in bytes
        public const int PACKET_PAYLOAD_SIZE = 800;                 // maximum payload size in bytes for packets that carry chunks of an audio taunt file
        public const int AMOUNT_OF_TAUNTS_PER_CLIENT = 6;
        public const float DEFAULT_TAUNT_COOLDOWN = 4.5f;           // defines the minimum interval between sending taunts for the client
        public static WaitForSecondsRealtime delay = new WaitForSecondsRealtime(0.008f);
        

        public class AudioTaunt
        {
            public string hash;
            public string name;
            public byte[] audio_taunt_data;         // temporary holder of incoming bytes till the full audio clip is assembled (or permanently on the server to buffer it incase other clients request this data)
            public bool is_data_complete = false;   // indicates wether audio_taunt_data has been fully populated 
            public int timestamp = 0;               // holds the time (Time.frameCount) of when this object was last used in a major action like a file transfer 
            // Client
            public bool ready_to_play;              // indicates wether the audio clip is in a playable state
            public bool requested_taunt;            // holds wether the client has requested the audiotaunt byte data for this.hash from the server
            public AudioClip audioclip;             // contains the playable taunt
            // Server
            public List<int> providing_clients;     // holds the connection ids of clients that use the taunt and can provide this taunt 
            public List<int> requesting_clients;    // A list of all connection ids that have requested this audioclip
            public List<int> received_packets;
        }


        public static class AClient
        {
            public static bool active = true;
            public static bool initialized = false;
            public static string loaded_local_taunts = "";              // temporary holder (necessary till the execution of LoadLocalAudioTauntsFromPilotPrefs()) of the hashes of the local taunts, gets populated from the pilots config files

            public static string LocalAudioTauntDirectory = "";         // path towards the directory where the audiotaunts from the local installation are saved
            public static string ExternalAudioTauntDirectory = "";      // path towards the directory where the audiotaunts of other players get saved
            public static int[] keybinds = new int[6];                  // keybinds[i] holds the keycode that triggers the playing of local_taunts[i] 
            public static int selected_audio_slot = 0;                  // temporary variable that is used in the menu code to differentiate between the 6 local taunt slots
            public static int audio_taunt_volume = 25;                  // range: 0-100
            public static float remaining_cooldown = 0f;                // time in seconds till the client is allowed to send an audiotaunt

            public static List<AudioTaunt> taunts = new List<AudioTaunt>();                                     // a list of all locally loaded audio taunts
            public static Dictionary<string, AudioTaunt> match_taunts = new Dictionary<string, AudioTaunt>();// contains the audio taunts of the other players during a game
            public static AudioTaunt[] local_taunts = new AudioTaunt[6];// contains the audio taunts that this client has chosen, can not change during a game 

            public static AudioSource[] audioSources = new AudioSource[3];


            [HarmonyPatch(typeof(GameManager), "Awake")]
            class MPAudioTaunts_GameManager_Awake
            {
                static void Postfix()
                {
                    if (String.IsNullOrEmpty(LocalAudioTauntDirectory))
                    {
                        LocalAudioTauntDirectory = Path.Combine(Application.streamingAssetsPath, "AudioTaunts");
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
                        ImportAudioTaunts(LocalAudioTauntDirectory, new List<string>());
                        ImportAudioTaunts(ExternalAudioTauntDirectory, new List<string>());
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
                            keybinds[i] = -1;
                        }
                        LoadLocalAudioTauntsFromPilotPrefs();
                    }
                    else
                    {

                    }
                    initialized = true;
                }


                // from https://github.com/derhass/olmod/commit/fa897b3384dfd6f228d4c95a385af6b7f37d99b5
                // Fix patching GameManager.Awake
                // When "GameManager.Awake" gets patched, two things happen:
                // 1. GameManager.Version gets set to 0.0.0.0 becuae it internally uses
                //    GetExecutingAssembly() to query the version, but the patched version
                //     lives in another assembly
                // 2. The "anitcheat" injection detector is triggered. But that thing
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


            // Imports either the in 'files_to_load' specified taunts or all taunts from that directory
            // (under the condition that they have yet to be imported and are valid formats and that their size is not beyond 128 kB) 
            public static void ImportAudioTaunts(string path_to_directory, List<String> files_to_load)
            {
                Debug.Log("Attempting to import AudioTaunts from: " + path_to_directory);
                bool load_all_files = files_to_load == null | files_to_load.Count == 0;
                Debug.Log(" load all files: " + load_all_files);
                var fileInfo = new DirectoryInfo(path_to_directory).GetFiles();
                foreach (FileInfo file in fileInfo)
                {
                    if ((files_to_load.Contains(file.Name) | load_all_files) && taunts.Find(t => t.name.Equals(file.Name)) == null && file.Extension.Equals(".ogg") && file.Length <= AUDIO_TAUNT_SIZE_LIMIT)
                    {

                        AudioTaunt t = new AudioTaunt
                        {
                            hash = CalculateMD5ForFile(Path.Combine(path_to_directory, file.Name)),
                            name = file.Name,
                            audio_taunt_data = File.ReadAllBytes(Path.Combine(path_to_directory, file.Name)),
                            is_data_complete = true,
                            timestamp = Time.frameCount,
                            audioclip = LoadAsAudioClip(file.Name, file.Extension, path_to_directory),//Resources.Load<AudioClip>("AudioTaunts/" + file.Name)
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
                        Debug.Log("  Added " + file.Name + "  size: " + file.Length + " as an AudioTaunt");

                    }
                }
            }

            private static AudioClip LoadAsAudioClip(string filename, string ext, string path2)
            {
                //Debug.Log("  Attempting to load file as audio clip: " + filename);
                string path = Path.Combine(path2, filename);
                if (path != null)
                {
                    WWW www = new WWW("file:///" + path);
                    while (!www.isDone) { }
                    if (string.IsNullOrEmpty(www.error))
                    {
                        return www.GetAudioClip(true, false);
                    }
                    else Debug.Log("Error in 'LoadAsAudioClip': " + www.error + " :" + filename + ":" + ext + ":" + path2);
                }
                return null;
            }

            // used to calculate a hash for each audio taunt file to avoid filename collisions
            private static string CalculateMD5ForFile(string filename)
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
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
                        AudioTaunt at = taunts.Find(t => t.hash.Equals(hash));
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

            // the clip_id should only be added when the audioclip should be shared
            public static void PlayAudioTauntFromAudioclip(AudioClip audioClip, string clip_id = null)
            {
                if (audio_taunt_volume == 0 | audioClip == null | !active)
                    return;

                Debug.Log("Playing Audiotaunt");

                int index = -1;
                for (int i = 0; i < audioSources.Length; i++) {
                    if (audioSources[i] == null)
                        audioSources[i] = new GameObject().AddComponent<AudioSource>();
                    
                    if (!audioSources[i].isPlaying) index = i;
                }

                if (index == -1) {
                    Debug.Log("Couldnt play Audio taunt. All audio sources are occupied!");
                    return;
                }

                audioSources[index].clip = audioClip;
                audioSources[index].volume = audio_taunt_volume / 100f;
                audioSources[index].timeSamples = 0;
                audioSources[index].bypassReverbZones = true;
                audioSources[index].reverbZoneMix = 0f;
                audioSources[index].PlayScheduled(AudioSettings.dspTime);
                audioSources[index].SetScheduledEndTime(AudioSettings.dspTime + 4);

                if (GameplayManager.IsMultiplayer && clip_id != null && Client.GetClient() != null && (GameplayManager.m_gameplay_state == GameplayState.PLAYING | MenuManager.m_menu_state == MenuState.MP_LOBBY)) {
                    Debug.Log("Client -> Server: Share the activation of "+clip_id);
                    Client.GetClient().Send(MessageTypes.MsgPlayAudioTaunt,
                                        new PlayAudioTaunt {
                                            hash = clip_id
                                        });
                }
            }

            public static string ChainTogetherHashesOfLocalTaunts()
            {
                string result = "";
                for (int i = 0; i < AMOUNT_OF_TAUNTS_PER_CLIENT; i++)
                {
                    result += local_taunts[i].hash;
                    if (i < AMOUNT_OF_TAUNTS_PER_CLIENT - 1)
                        result += "/";
                }
                return result;
            }

            // Todo: buffer the result
            public static float[] calculateFrequencyBand()
            {
                float[] freqBand = new float[8];
                for (int z = 0; z < 3; z++)
                {

                    if (audioSources[z] != null && audioSources[z].isPlaying)
                    {
                        float[] samples = new float[512];

                        audioSources[z].GetSpectrumData(samples, 0, FFTWindow.Rectangular);

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

            /*
            [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
            internal class MPAudioTaunts_PlayerShip_UpdateReadImmediateControls {
                static void Prefix() {
                    if (!GameplayManager.IsDedicatedServer() && AServer.server_supports_audiotaunts) {
                        if (remaining_cooldown > 0f)
                            remaining_cooldown -= Time.deltaTime;

                        for (int i = 0; i < 6; i++) {
                            if (remaining_cooldown <= 0f && keybinds[i] > 0 && Input.GetKeyDown((KeyCode)keybinds[i])) {
                                remaining_cooldown = DEFAULT_TAUNT_COOLDOWN;
                                Debug.Log("-----Found Keyinput. playing audiotaunt and sending its hash to the server to distribute");
                                PlayAudioTauntFromAudioclip(local_taunts[i].audioclip, local_taunts[i].hash);
                            }
                        }
                    }
                }
            }*/

            // Send the file names of your audio taunts to the server when entering a game // Client OnAcceptedToLobby OnMatchStart
            [HarmonyPatch(typeof(Client), "OnAcceptedToLobby")]
            class MPAudioTaunts_Client_OnAcceptedToLobby
            {
                static void Postfix(){
                    if (GameplayManager.IsDedicatedServer() | Client.GetClient() == null | !active) //| !server_supports_audiotaunts
                    {
                        Debug.Log("-----Bailed Early OnAcceptedToLobby");
                        return;
                    }


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
                    Debug.Log("-----Sent Information about my audio taunts to the server\n"+fileHashes);
                    Client.GetClient().Send(MessageTypes.MsgShareAudioTauntIdentifiers,
                        new ShareAudioTauntIdentifiers
                        {
                            hashes = fileHashes
                        });
                }
            }



            
            // Todo: move the key check here
            [HarmonyPatch(typeof(GameManager), "Update")]
            class MPAudioTaunts_GameManager_Update
            {
                public static void Postfix()
                {
                    if (!GameplayManager.IsDedicatedServer() && active)
                    {
                        if (!MPAudioTaunts_Client_RegisterHandlers.isUploading && MPAudioTaunts_Client_RegisterHandlers.queued_uploads.Count > 0)
                        {
                            Debug.Log("-----Processing uploading the requested audio taunts to the server");
                            GameManager.m_gm.StartCoroutine(MPAudioTaunts_Client_RegisterHandlers.UploadAudioTauntToServer(MPAudioTaunts_Client_RegisterHandlers.queued_uploads[0]));
                            MPAudioTaunts_Client_RegisterHandlers.queued_uploads.Remove(MPAudioTaunts_Client_RegisterHandlers.queued_uploads[0]);
                        }

                        if (AServer.server_supports_audiotaunts)
                        {
                            if (remaining_cooldown > 0f)
                                remaining_cooldown -= Time.deltaTime;

                            for (int i = 0; i < 6; i++)
                            {
                                if (remaining_cooldown <= 0f && keybinds[i] > 0 && Input.GetKeyDown((KeyCode)keybinds[i]))
                                {
                                    remaining_cooldown = DEFAULT_TAUNT_COOLDOWN;
                                    Debug.Log("-----Found Keyinput. playing audiotaunt and sending its hash to the server to distribute");
                                    PlayAudioTauntFromAudioclip(local_taunts[i].audioclip, local_taunts[i].hash);
                                }
                            }
                        }
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
                    
                    ImportAudioTaunts(ExternalAudioTauntDirectory, file_hashes);
;

                    foreach (string hash in file_hashes)
                    {

                        Debug.Log("  Hashes received: " + hash);
                        // if the taunt has been downloaded before then load it locally and add it to the game taunts
                        if (!string.IsNullOrEmpty(hash) & !match_taunts.ContainsKey(hash))
                        {

                                AudioTaunt taunt = taunts.Find(t => t.hash.Equals(hash));
                                if (taunt != null)
                                {
                                    Debug.Log("[AudioTaunts]  Found Audiotaunt in the local data: " + hash);
                                    match_taunts.Add(hash, taunt);
                                }
                                else
                                {
                                    Debug.Log("[AudioTaunts]  Requesting Audiotaunt from Server: " + hash);
                                    if (Client.GetClient() == null)
                                    {
                                        Debug.Log("[AudioTaunts]  MPAudioTaunts_Client_RegisterHandlers: no client?");
                                        return;
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

                                    Client.GetClient().Send(MessageTypes.MsgRequestAudioTaunt,
                                        new RequestAudioTaunt
                                        {
                                            hash = hash
                                        });

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
                    if(index != -1)
                    {
                        if(local_taunts[index].is_data_complete)
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
                    Debug.Log("[AudioTaunts] Started uploading AudioTaunt to server");
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

                        Client.GetClient().connection.Send(MessageTypes.MsgAudioTauntPacket, packet);
                        Debug.Log("[AudioTaunts]    upload: " + (position + index) + " / " + data.audio_taunt_data.Length + "  for " + data.hash);
                        yield return delay;
                        position += PACKET_PAYLOAD_SIZE;
                        _packet_id++;
                    }

                    isUploading = false;
                }







                
                private static void OnAudioTauntPacket(NetworkMessage rawMsg)
                {
                    if (!active)
                        return;

                    Debug.Log("[AudioTaunts]        Received a Fragment of an Audiotaunt");
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
                            Debug.Log("     Added the data of the fragment");

                            // if this completes the data then check wether there are unanswered requests
                            if (match_taunts[msg.hash].received_packets.Count * PACKET_PAYLOAD_SIZE >= msg.filesize)
                            {
                                Debug.Log("     This Fragment completed the audio taunt data");
                                match_taunts[msg.hash].is_data_complete = true;



                                // write the byte data to a file
                                string path = Path.Combine(ExternalAudioTauntDirectory,msg.hash+".ogg");

                                File.WriteAllBytes(path, match_taunts[msg.hash].audio_taunt_data);

                                match_taunts[msg.hash].audioclip = LoadAsAudioClip(msg.hash, ".ogg", ExternalAudioTauntDirectory);
                                match_taunts[msg.hash].ready_to_play = true;


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






                 private static void OnPlayAudioTaunt(NetworkMessage rawMsg)
                 {
                     if (!active) return;

                     var msg = rawMsg.ReadMessage<PlayAudioTaunt>();

                     Debug.Log("[AudioTaunts] playing external audiotaunt: "+msg.hash);

                     if (match_taunts.ContainsKey(msg.hash) && match_taunts[msg.hash].ready_to_play)
                        PlayAudioTauntFromAudioclip(match_taunts[msg.hash].audioclip);
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















        // Responsible for sharing the required audiotaunts across clients and their activation
        public static class AServer
        {
            public static bool active = true;
            public static bool server_supports_audiotaunts = false;

            public static Dictionary<string,AudioTaunt> match_taunts = new Dictionary<string, AudioTaunt>();


            [HarmonyPatch(typeof(Client), "OnAcceptedToLobby")]
            class MPAudioTaunts_Client_OnAcceptedToLobby
            {
                static void Postfix()
                {
                    if (GameplayManager.IsDedicatedServer())
                        server_supports_audiotaunts = false;

                }
            }


            // resets match specific informations
            [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
            class MPAudioTaunts_NetworkMatch_InitBeforeEachMatch
            {
                static void Postfix()
                {
                    if (!GameplayManager.IsDedicatedServer())
                    {
                        Debug.Log("-----Bailed out of NetworkMatch.InitBeforeEachMatch");
                        return;
                    }


                    Debug.Log("-----Reset Audio taunt specific settings on the server");
                    match_taunts = new Dictionary<string, AudioTaunt>();
                }
            }

            [HarmonyPatch(typeof(Server), "RegisterHandlers")]
            class MPAudioTaunts_Server_RegisterHandlers
            {
                private static void OnShareAudioTauntIdentifiers(NetworkMessage rawMsg)
                {
                    // Read the message
                    Debug.Log("[Audiotaunt] : Received AudioTauntIdentifiers from Client");
                    var msg = rawMsg.ReadMessage<ShareAudioTauntIdentifiers>();
                    
                    // Split the string to retrieve the individual hashes
                    string[] file_hashes = msg.hashes.Split('/');
                   

                    // Update match_taunts
                    int index = 0;
                    foreach (string hash in file_hashes){

                        if (!string.IsNullOrEmpty(hash) & index < AMOUNT_OF_TAUNTS_PER_CLIENT){
                            // this taunt already exists, add the sender as a possible provider
                            if(match_taunts.ContainsKey(hash)){
                                match_taunts[hash].providing_clients.Add(rawMsg.conn.connectionId);
                                Debug.Log("  match_taunts:" + hash + "exists, added: " + rawMsg.conn.connectionId + " as a provider");
                            }
                            // this taunt doesnt exist, add it
                            else{
                                AudioTaunt taunt = new AudioTaunt {
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
                                match_taunts.Add(hash,taunt);
                                Debug.Log("  Added new context to match_taunts: " + hash);
                            } 
                        }
                        index++;
                    }

                    
                    string hashes = "";
                    foreach (var t in match_taunts)
                        hashes += t.Value.hash + "/";

                    //Send the updated list of hashes to all connected clients that support audiotaunts
                    foreach (NetworkConnection networkConnection in NetworkServer.connections) {
                        if (MPTweaks.ClientHasTweak(networkConnection.connectionId, "audiotaunts"))
                            networkConnection.Send(MessageTypes.MsgShareAudioTauntIdentifiers, new ShareAudioTauntIdentifiers { hashes = hashes });
                    }

                }

                
                private static void OnAudioTauntRequest(NetworkMessage rawMsg)
                {
                    Debug.Log("[AudioTaunts] Received audio taunt file request");
                    var msg = rawMsg.ReadMessage<RequestAudioTaunt>();

                    if(match_taunts.ContainsKey(msg.hash))
                    {
                        // if its byte data is populated send that to the client that requested it
                        if(match_taunts[msg.hash].is_data_complete)
                        {
                            // start upload
                            GameManager.m_gm.StartCoroutine(UploadAudioTauntToClient(msg.hash, match_taunts[msg.hash].audio_taunt_data, rawMsg.conn.connectionId));
                        }

                        // if it is not then request that data from the client the shared this audiotaunt
                        // and add Client A to the request list
                        else
                        {
                            foreach (NetworkConnection networkConnection in NetworkServer.connections)
                            {
                                if (networkConnection.connectionId == match_taunts[msg.hash].providing_clients[0])
                                {
                                    networkConnection.Send(MessageTypes.MsgRequestAudioTaunt, new RequestAudioTaunt { hash = msg.hash });
                                    Debug.Log(" [SERVER]: Requested AudioTaunt data from: "+ networkConnection.connectionId+" for: "+rawMsg.conn.connectionId);
                                }     
                            }
                            match_taunts[msg.hash].requesting_clients.Add(rawMsg.conn.connectionId);
                        }
                    }
                    else
                    {
                        Debug.Log("  [SERVER]: A client requested an audiotaunt that has no context on the server");
                    }
                }

                public static IEnumerator UploadAudioTauntToClient(string hash, byte[] data, int connectionId)
                {
                    Debug.Log("[AudioTaunts] Started uploading AudioTaunt to client");
                    if(data.Length < PACKET_PAYLOAD_SIZE)
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
                            Debug.Log("[AudioTaunts]    packet: "+packet_id+" upload: " + (position + index) + " / " + data.Length + "  for " + hash);
                            position += PACKET_PAYLOAD_SIZE;
                            packet_id++;
                            yield return delay;
                        }
                    }
                    Debug.Log("[AudioTaunts] successfully transmitted taunt to client" + hash);
                }



                private static void OnAudioTauntPacket(NetworkMessage rawMsg)
                {
                    if (!active)
                        return;

                    Debug.Log("[AudioTaunts]        Received a Fragment of an Audiotaunt");
                    var msg = rawMsg.ReadMessage<AudioTauntPacket>();

                    if(match_taunts.ContainsKey(msg.hash))
                    {
                        // initialise the array with the correct size
                        if(match_taunts[msg.hash].timestamp == -1)
                        {
                            match_taunts[msg.hash].timestamp = Time.frameCount;
                            match_taunts[msg.hash].audio_taunt_data = new byte[msg.filesize];
                        }

                        if(!match_taunts[msg.hash].received_packets.Contains(msg.packet_id))
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
                                Debug.Log("     This Fragment completed the audio taunt data");
                                match_taunts[msg.hash].is_data_complete = true;
                                foreach( int connectionId in match_taunts[msg.hash].requesting_clients)
                                {
                                    Debug.Log("         Uploading Requested Taunt to: "+connectionId);
                                    GameManager.m_gm.StartCoroutine(UploadAudioTauntToClient(msg.hash, match_taunts[msg.hash].audio_taunt_data, connectionId));
                                }
                            }
                        }
                        else
                        {
                            Debug.Log("     This Fragment is a duplicate. packet_id: "+msg.packet_id);
                        }

                    }
                    else
                    {
                        Debug.Log(" There is no context for this audiotaunt, ignoring the fragment, connectionID:"+rawMsg.conn.connectionId);
                    }
                }


                private static void OnPlayAudioTaunt(NetworkMessage rawMsg)
                {
                    Debug.Log("     RECEIVED AUDIO TAUNT BEING PLAYED");
                    var msg = rawMsg.ReadMessage<PlayAudioTaunt>();
                    if (match_taunts.ContainsKey(msg.hash))
                    {
                        // distribute it to all other clients
                        PlayAudioTaunt packet = new PlayAudioTaunt
                        {
                            hash = msg.hash
                        };
             
                        foreach (NetworkConnection networkConnection in NetworkServer.connections)
                        {
                            if (rawMsg.conn.connectionId != networkConnection.connectionId & MPTweaks.ClientHasTweak(networkConnection.connectionId, "audiotaunts"))
                                networkConnection.Send(MessageTypes.MsgPlayAudioTaunt, packet);
                        }
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

        // Todo:
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



        // Todo: test this
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

                data = Convert.FromBase64String(msg);
            }
        }
    }
}
