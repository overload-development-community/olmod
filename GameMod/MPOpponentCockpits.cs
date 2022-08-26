using System;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class MPOpponentCockpits {
        public static void SetOpponentCockpitVisibility(Player p, bool enabled) {
            if (p != null && p.c_player_ship != null && !p.isLocalPlayer && !p.m_spectator) {
                //Debug.LogFormat("Setting cockpit visibility for player ship {0} to {1}",p.m_mp_name, enabled);
                MeshRenderer[] componentsInChildren = p.c_player_ship.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                foreach (MeshRenderer meshRenderer in componentsInChildren)
                {
                    if (meshRenderer.enabled != enabled) {
                        if (string.CompareOrdinal(meshRenderer.name, 0, "cp_", 0, 3) == 0) {
                            meshRenderer.enabled = enabled;
                            meshRenderer.shadowCastingMode = 0;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Overload.PlayerShip), "SetCockpitVisibility")]
        class MPOpponentCockpits_Disable1 {
            static void Postfix(PlayerShip __instance) {
                SetOpponentCockpitVisibility(__instance.c_player, false);
            }
        }

        [HarmonyPatch(typeof(Overload.Player), "RestorePlayerShipDataAfterRespawn")]
        class MPOpponentCockpits_Disable2 {
            static void Postfix(Player __instance) {
                SetOpponentCockpitVisibility(__instance, false);
            }
        }
    }
}
