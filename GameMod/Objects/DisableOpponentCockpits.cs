using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    [Mod(Mods.DisableOpponentCockpits)]
    public static class DisableOpponentCockpits {
        public static void SetOpponentCockpitVisibility(Player p, bool enabled) {
            if (p != null && p.c_player_ship != null && !p.isLocalPlayer && !p.m_spectator) {
                MeshRenderer[] componentsInChildren = p.c_player_ship.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                foreach (MeshRenderer meshRenderer in componentsInChildren) {
                    if (meshRenderer.enabled != enabled) {
                        if (string.CompareOrdinal(meshRenderer.name, 0, "cp_", 0, 3) == 0) {
                            meshRenderer.enabled = enabled;
                            meshRenderer.shadowCastingMode = 0;
                        }
                    }
                }
            }
        }
    }
}
