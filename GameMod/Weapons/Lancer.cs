using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Lancer : PrimaryWeapon
    {
        public Lancer()
        {
            displayName = "LANCER";
            Tag2A = "C";
            Tag2B = "SF";
            UsesEnergy = true;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ProjPrefab type = ProjPrefab.proj_beam;
            Quaternion localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, (player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0f : 0.2f, c_up, c_right);
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                if (player.c_player_ship.m_alternating_fire)
                {
                    MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
                }
                else
                {
                    MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
                }
                player.c_player_ship.m_alternating_fire = !player.c_player_ship.m_alternating_fire;
                player.c_player_ship.m_refire_time += 0.133333f * refire_multiplier;
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy(1f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 1f, 1f);
                return;
            }
            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            if (GameplayManager.IsMultiplayerActive)
            {
                if (player.m_overdrive)
                {
                    player.c_player_ship.m_refire_time += 0.29f;
                }
                else
                {
                    player.c_player_ship.m_refire_time += 0.23f * refire_multiplier;
                }
            }
            else if (player.m_overdrive)
            {
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.28f : 0.2f);
            }
            else
            {
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.2f : 0.1f) * refire_multiplier;
            }
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                if (GameplayManager.IsMultiplayerActive)
                {
                    player.UseEnergy(1f);
                }
                else
                {
                    player.UseEnergy((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 2f : 1.5f);
                }
            }
            player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 1.3f, 1.5f);
        }
    }
}