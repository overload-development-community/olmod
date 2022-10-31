using GameMod.Metadata;
using Overload;

namespace GameMod.Objects {
    /// <summary>
    /// Code to manage balancing for the thunderbolt.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    public class ThunderboltBalance {
        private const float m_tb_overchargedamage_multiplier = 4f; // 4.0dps self-damage instead of stock 1.0dps)
        public const float m_muzzle_adjust = 0.2f; // Projectile exit point offsets

        public static int m_charge_loop_index = -1;

        private static float GetThunderboltSelfDamageMultiplier() {
            return GameplayManager.IsMultiplayer ? m_tb_overchargedamage_multiplier : 1f;
        }

        public static void StopThunderboltSelfDamageLoop() {
            if (m_charge_loop_index != -1) {
                GameManager.m_audio.StopSound(m_charge_loop_index);
                m_charge_loop_index = -1;
            }
        }

        public static float GetSelfChargeDamage(float num, PlayerShip playerShip) {
            return GetThunderboltSelfDamageMultiplier() * num;
        }
    }
}
