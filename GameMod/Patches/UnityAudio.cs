using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches {
    /// <summary>
    /// Adds a filter to the audio source if necessary.
    /// </summary>
    [Mod(Mods.SoundOcclusion)]
    [HarmonyPatch(typeof(UnityAudio), "CreateAudioSourceAndObject")]
    public static class UnityAudio_CreateAudioSourceAndObject {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(ref GameObject[] ___m_a_object, int i) {
            //GameManager.m_audio.m_debug_sounds = true;

            if (___m_a_object != null) {
                MPSoundExt.m_a_object[i] = ___m_a_object[i];
                MPSoundExt.m_a_source[i] = ___m_a_object[i].GetComponent<AudioSource>();

                if (Menus.mms_audio_occlusion_strength != 0) {
                    SoundOcclusion.AddFilter(i);
                }
            }
        }
    }

    /// <summary>
    /// Disables audio on the server by faking no audio channels are available.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(UnityAudio), "FindNextOpenAudioSlot")]
    public static class UnityAudio_FindNextOpenAudioSlot {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(ref int __result) {
            // tell the server we are sorry, but we don't have any audio channels left...
            __result = -1;
            return false;
        }
    }

    /// <summary>
    /// Sets an int to track state for use with Occlusion in the Cue play commands since they can fire off several layers
    /// simultaneously. Previously this was resulting in several Linecasts from the same position. Should make things more
    /// efficient by allowing the PlaySound method to only do the check once for each cue.
    /// Needs to patch PlayCue2D, PlayCuePos, and PlayThunderboltFire, and all of it manually. See the next method.
    /// </summary>
    /// <remarks>
    /// This whole nonsense is because SFXCueManager is a static class with a static constructor, and that static constructor
    /// calls a function which ultimately calls UnityEngine.Resources.Load, attempting to load a resource from the Overload
    /// game files before they are available, causing an access violation that hard crashes olmod.  Instead, we have to patch
    /// after the static constructor is first loaded, which appears to be this method.
    /// </remarks>
    [Mod(Mods.SoundOcclusion)]
    [HarmonyPatch(typeof(UnityAudio), "InitAudio")]
    public static class UnityAudio_InitAudio {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void PatchPrefix() {
            SoundOcclusion.CueState = 1;
        }

        public static void PatchPostfix() {
            SoundOcclusion.CueState = 0;
        }

        public static void Postfix() {
            var harmony = new Harmony("olmod.postpatcher");

            var orig1 = typeof(SFXCueManager).GetMethod("PlayCue2D");
            var orig2 = typeof(SFXCueManager).GetMethod("PlayCuePos");
            var orig3 = typeof(SFXCueManager).GetMethod("PlayThunderboltFire");

            var prefix = typeof(UnityAudio_InitAudio).GetMethod("PatchPrefix");
            var postfix = typeof(UnityAudio_InitAudio).GetMethod("PatchPostfix");

            harmony.Patch(orig1, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
            harmony.Patch(orig2, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
            harmony.Patch(orig3, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
        }
    }

    /// <summary>
    /// Adds custom music support.
    /// </summary>
    [Mod(Mods.CustomMusic)]
    [HarmonyPatch(typeof(UnityAudio), "PlayCurrentTrack")]
    public static class UnityAudio_PlayCurrentTrack {
        private static AudioClip LoadLevelAudioClip(LevelInfo level, string name) {
            if (!level.IsAddOn || level.FilePath == null)
                return null;
            string tmpFilename = null;
            string clipFilename = null;
            string ext = null;
            AudioClip clip = null;
            if (level.ZipPath != null) {
                byte[] data = Mission.LoadAddonData(level.ZipPath, Path.Combine(Path.GetDirectoryName(level.FilePath), name), ref ext,
                    new[] { ".wav", ".ogg" });
                if (data != null) {
                    tmpFilename = Path.GetTempFileName() + ext;
                    File.WriteAllBytes(tmpFilename, data);
                    clipFilename = tmpFilename;
                }
            } else {
                clipFilename = Mission.FindAddonFile(Path.Combine(Path.GetDirectoryName(level.FilePath), name), ref ext,
                    new[] { ".wav", ".ogg" });
            }
            if (clipFilename != null) {
                WWW www = new WWW("file:///" + clipFilename);
                while (!www.isDone) {
                }
                if (string.IsNullOrEmpty(www.error))
                    clip = www.GetAudioClip(true, false);
                if (tmpFilename != null)
                    File.Delete(tmpFilename);
            }
            return clip;
        }

        public static void Postfix(string ___m_current_track, AudioSource ___m_music_source, float ___m_volume_music) {
            if (___m_music_source.clip == null && ___m_current_track != null) // couldn't load built in, try custom
            {
                Debug.Log("Trying loading custom music " + ___m_current_track);
                ___m_music_source.clip =
                    GameplayManager.Level.Mission.Type == MissionType.ADD_ON ?
                        GameplayManager.Level.Mission.LoadAudioClip(___m_current_track) :
                        LoadLevelAudioClip(GameplayManager.Level, ___m_current_track);
                if (___m_volume_music > 0f)
                    ___m_music_source.Play();
            }
        }
    }

    /// <summary>
    /// Disables the playing of music on the server.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(UnityAudio), "PlayMusic")]
    public static class ServerCleanup_NoPlayMusic {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix() {
            // suppress PlayMusic requests on the server...
            return false;
        }
    }

    /// <summary>
    /// Performs the sound occlusion when the sound is played.
    /// </summary>
    [Mod(Mods.SoundOcclusion)]
    [HarmonyPatch(typeof(UnityAudio), "PlaySound")]
    public static class UnityAudio_PlaySound {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(AudioSource), "Play")) {
                    yield return new CodeInstruction(OpCodes.Nop);
                } else {
                    yield return code;
                }
            }
        }

        public static void Postfix(int __result, Vector3 pos3d) {
            if (__result == -1) // if -1 then we ran out of audio slots, skip the whole thing
                return;

            // If pos3d == Vector3.zero, then it's almost without a doubt a 2D cue on the local client. It's beyond infeasible that sound could accidentally come from *exactly* this point.
            // If it ever does, well then you get 1 glitched cue and you should have bought a lottery ticket.
            if (Menus.mms_audio_occlusion_strength != 0) {
                if (pos3d == Vector3.zero) {
                    SoundOcclusion.Occluded = false;
                } else {
                    if (!MPObserver.Enabled || MPObserver.ObservedPlayer != null) {
                        // if we're mid-cue there's multiple sounds being played from the same location - use the previous Linecast calculations for efficiency
                        if (SoundOcclusion.CueState < 2) {
                            Vector3 shipPos = GameManager.m_player_ship.transform.localPosition;

                            if (Physics.Linecast(pos3d, shipPos, out RaycastHit ray1, 67256320)) {
                                // we don't have line-of-sight
                                // This is the "Tier 3" approach, taking both distance to target and thickness of obstruction into account
                                Physics.Linecast(shipPos, pos3d, out RaycastHit ray2, 67256320);

                                float maxdist = SoundOcclusion.MAXDISTS[Menus.mms_audio_occlusion_strength];
                                float cutoff = SoundOcclusion.CUTOFFS[Menus.mms_audio_occlusion_strength];
                                float lowfreq = SoundOcclusion.LOWFREQS[Menus.mms_audio_occlusion_strength];
                                float boost = SoundOcclusion.BOOSTS[Menus.mms_audio_occlusion_strength];

                                float p2pDist = Vector3.Distance(pos3d, shipPos); // point to point distance

                                float thick = Mathf.Clamp(p2pDist - ray1.distance - ray2.distance, 1f, maxdist); // how thick the obstruction is, clamped
                                p2pDist = Mathf.Clamp(p2pDist, SoundOcclusion.MINDIST, maxdist); // clamp the p2pDist value
                                float factor = (maxdist - (0.6f * thick + 0.4f * p2pDist)) / (maxdist);

                                SoundOcclusion.CutoffFreq = lowfreq + (cutoff * factor * factor); // exponential curve
                                SoundOcclusion.BoostAmount = boost * (0.85f - factor);
                                SoundOcclusion.Occluded = true;
                            } else {
                                SoundOcclusion.Occluded = false;
                            }
                        }
                        if (SoundOcclusion.CueState == 1) // we're in a multicue, pause calculations
                        {
                            SoundOcclusion.CueState = 2;
                        }
                    }
                }
                if (SoundOcclusion.Occluded) {
                    MPSoundExt.m_a_filter[__result].cutoffFrequency = SoundOcclusion.CutoffFreq;
                    MPSoundExt.m_a_source[__result].volume = MPSoundExt.m_a_source[__result].volume + SoundOcclusion.BoostAmount; // slight boost to volume as range increases to counter the HF rolloff
                    MPSoundExt.m_a_filter[__result].enabled = true;
                } else {
                    // restore the normal filter
                    MPSoundExt.m_a_filter[__result].cutoffFrequency = 22000f;
                    MPSoundExt.m_a_filter[__result].enabled = false;
                }
            }
            MPSoundExt.m_a_source[__result].Play();  // Nop'd out in the transpiler
        }
    }
}
