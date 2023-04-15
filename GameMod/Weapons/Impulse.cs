using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Impulse : Weapon
    {
        public Impulse(Ship s)
        {
            ship = s;

            displayName = "IMPULSE";
            UsesEnergy = true;
        }

        public override void Fire(Player player, float refire_multiplier)
        {
            Vector3 c_right = (Vector3)c_right_Field.GetValue(player.c_player_ship);
            Vector3 c_up = (Vector3)c_up_Field.GetValue(player.c_player_ship);
            Vector3 vector = player.c_player_ship.c_forward;
            Vector3 vector2 = c_right;
            Vector3 vector3 = c_up;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ProjPrefab type = ProjPrefab.proj_impulse;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2A || (GameplayManager.IsMultiplayer && !MPClassic.matchEnabled)) //GameplayManager.IsMultiplayerActive)
            {
                Quaternion localRotation = player.c_player_ship.c_transform.localRotation;
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 0);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position + vector2 * ship.QdiffRightX + vector3 * ship.QdiffY + vector * ship.QdiffZ, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 1);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 2);
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position + vector2 * ship.QdiffLeftX + vector3 * ship.QdiffY + vector * ship.QdiffZ, localRotation, 0f, WeaponUnlock.LEVEL_2A, false, 3);
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