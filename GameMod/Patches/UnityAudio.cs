using System.IO;
using GameMod.Metadata;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches {
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
}
