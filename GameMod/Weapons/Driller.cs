using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Driller : PrimaryWeapon
    {
        public Driller()
        {
            displayName = "DRILLER";
            Tag2A = "DX";
            Tag2B = "M";
            UsesAmmo = true;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                ProjPrefab type = ProjPrefab.proj_driller_mini;
                Quaternion localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, 0.6f, c_up, c_right);
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_center.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
                player.c_player_ship.m_refire_time += 0.11f;
                player.PlayCameraShake(CameraShakeType.FIRE_DRILLER, 0.7f, 0.7f);
                player.UseAmmo(1);
            }
            else
            {
                ProjPrefab type = ProjPrefab.proj_driller;
                MPSniperPackets.MaybePlayerFire(rot: (!GameplayManager.IsMultiplayerActive)
                    ? AngleRandomize(player.c_player_ship.c_transform.localRotation, 0.1f, c_up, c_right)
                    : player.c_player_ship.c_transform.localRotation, player: player, type: type, pos: player.c_player_ship.m_muzzle_center.position, strength: 0f, upgrade_lvl: player.m_weapon_level[(int)player.m_weapon_type], no_sound: false, slot: 0);
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.26f : 0.22f);
                player.PlayCameraShake(CameraShakeType.FIRE_DRILLER, 1f, 1f);
                player.UseAmmo(2);
            }
        }
    }
}