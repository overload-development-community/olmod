using GameMod.Metadata;
using HarmonyLib;
using Overload;

namespace GameMod.Patches {
    /// <summary>
    /// Disables audio on the server by faking no audio channels are available.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(UnityAudio), "FindNextOpenAudioSlot")]
    public class UnityAudio_FindNextOpenAudioSlot {
        public static bool Prefix(ref int __result) {
            // tell the server we are sorry, but we don't have any audio channels left...
            if (GameplayManager.IsDedicatedServer()) {
                __result = -1;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Disables the playing of music on the server.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(UnityAudio), "PlayMusic")]
    public class ServerCleanup_NoPlayMusic {
        public static bool Prefix() {
            // suppress PlayMusic requests on the server...
            if (GameplayManager.IsDedicatedServer()) {
                return false;
            }
            return true;
        }
    }
}
