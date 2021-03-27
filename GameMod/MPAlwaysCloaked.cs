using Harmony;
using Overload;

namespace GameMod {
    public class MPAlwaysCloaked {
        public static bool Enabled { get; set; } = false;
    }

    /// <summary>
    /// Make ships cloaked on spawn when mod enabled.
    /// </summary>
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

    /// <summary>
    /// Disallow cloak from spawning when mod enabled.
    /// </summary>
    [HarmonyPatch(typeof(NetworkMatch), "RandomAllowedSuperSpawn")]
    class MPAlwaysCloaked_RandomAllowedSuperSpawn {
        private static void Prefix() {
            if (MPAlwaysCloaked.Enabled) {
                for (int index = 0; index < RobotManager.m_multiplayer_spawnable_supers.Count; index++) {
                    var super = RobotManager.m_multiplayer_spawnable_supers[index];
                    if (super.type == (int)SuperType.CLOAK) {
                        super.percent = 0;
                        RobotManager.m_multiplayer_spawnable_supers[index] = super;
                    }
                }
            }
        }
    }
}
