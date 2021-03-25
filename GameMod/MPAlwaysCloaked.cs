using Harmony;
using Overload;

namespace GameMod {
    public class MPAlwaysCloaked {
        public static bool Enabled { get; set; } = false;
    }

    [HarmonyPatch(typeof(Player), "Update")]
    class MPAlwaysCloaked_Update {
        private static void Postfix(Player __instance) {
            if (GameplayManager.IsMultiplayer && NetworkManager.IsServer() && NetworkMatch.m_match_state == MatchState.PLAYING && MPAlwaysCloaked.Enabled && __instance != null && !__instance.Networkm_cloaked) {
                __instance.Networkm_cloaked = true;
                __instance.CallRpcActivateCloak(float.MaxValue);
                __instance.CallTargetAddHUDMessage(__instance.connectionToClient, Loc.LSN("EVERYONE IS ALWAYS CLOAKED!"), -1, true);
            }
        }
    }
}
