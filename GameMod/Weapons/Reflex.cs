using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Reflex : Weapon
    {
        public Reflex(Ship s)
        {
            ship = s;

            displayName = "REFLEX";
            UsesEnergy = true;
        }

        public override void Fire(Player player, float refire_multiplier)
        {
            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ProjPrefab type = ProjPrefab.proj_reflex;
            if (player.c_player_ship.m_alternating_fire)
            {
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, player.c_player_ship.c_transform.localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, player.c_player_ship.c_transform.localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            }
            player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.1f : 0.08f) * refire_multiplier;
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy(0.3f);
            }
            player.PlayCameraShake(CameraShakeType.FIRE_REFLEX, 1f, 1f);
        }
    }
}