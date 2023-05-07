using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Impulse : PrimaryWeapon
    {
        public Impulse()
        {
            displayName = "IMPULSE";
            Tag2A = "Q";
            Tag2B = "RF";
            UsesEnergy = true;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ProjPrefab type = ProjPrefab.proj_impulse;

            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2A || (GameplayManager.IsMultiplayer && !MPClassic.matchEnabled)) //GameplayManager.IsMultiplayerActive)
            {
                Quaternion localRotation = player.c_player_ship.c_transform.localRotation;
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 0);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position + c_right * ship.QdiffRightX + c_up * ship.QdiffY + c_forward * ship.QdiffZ, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 1);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 2);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position + c_right * ship.QdiffLeftX + c_up * ship.QdiffY + c_forward * ship.QdiffZ, localRotation, 0f, WeaponUnlock.LEVEL_2A, false, 3);
                player.c_player_ship.m_refire_time += 0.28f * refire_multiplier;
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy(0.666667f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_IMPULSE, 1.3f, 1.2f);
            }
            else
            {
                Quaternion localRotation = player.c_player_ship.c_transform.localRotation;
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 2);
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.25f : 0.2f) * refire_multiplier;
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.33333f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_IMPULSE, 1f, 1f);
            }
        }

        /*
        public override void ServerFire(Player player, float refire_multiplier)
        {
            Client.GetClient().Send(MessageTypes.MsgSniperPacket, new SniperPacketMessage
            {
                m_player_id = player.netId,
                m_type = type,
                m_pos = pos,
                m_rot = rot,
                m_strength = strength,
                m_upgrade_lvl = upgrade_lvl,
                m_no_sound = no_sound,
                m_slot = slot,
                m_force_id = force_id
            });
        }
        */
    }
}