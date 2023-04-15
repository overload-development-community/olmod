using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Cyclone : Weapon
    {
        FieldInfo flak_fire_count_Field = AccessTools.Field(typeof(PlayerShip), "flak_fire_count");

        public Cyclone(Ship s)
        {
            ship = s;

            displayName = "CYCLONE";
            UsesEnergy = true;
        }

        public override void Fire(Player player, float refire_multiplier)
        {
            float flak_fire_count = (float)flak_fire_count_Field.GetValue(player.c_player_ship);
            Vector3 c_right = (Vector3)c_right_Field.GetValue(player.c_player_ship);
            Vector3 c_up = (Vector3)c_up_Field.GetValue(player.c_player_ship);

            float num3 = 1f - Mathf.Min((float)flak_fire_count * 0.05f, (player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.25f);
            player.c_player_ship.FiringPitchModifier = ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? (0.6f - num3) : (0.75f - num3)) * 0.25f;
            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ProjPrefab type = ProjPrefab.proj_vortex;
            player.PlayCameraShake(CameraShakeType.FIRE_CYCLONE, 1f, 1f);
            float fire_angle = player.c_player_ship.m_fire_angle;
            Quaternion localRotation2 = player.c_player_ship.c_transform.localRotation;
            float angle = ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 2.25f : 1.5f);
            Quaternion localRotation;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2A)
            {
                angle = 2.75f;
                localRotation = AngleRandomize(localRotation2, 0.5f, c_up, c_right);
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_center.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true);
            }
            if (player.c_player_ship.IsCockpitVisible)
            {
                ParticleManager.psm[2].StartParticle(8, player.c_player_ship.m_muzzle_center.position, localRotation2, player.c_player_ship.c_transform);
            }
            Vector3 vector6 = c_right * 0.1f;
            Vector3 vector7 = Quaternion.AngleAxis(fire_angle, player.c_player_ship.c_forward) * vector6;
            localRotation = AngleSpreadX(localRotation2, angle, c_up);
            localRotation = AngleSpreadZ(localRotation, fire_angle, player.c_player_ship.c_forward);
            localRotation = AngleRandomize(localRotation, 0.25f, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_center.position + vector7, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true);
            vector7 = Quaternion.AngleAxis(fire_angle + 120f, player.c_player_ship.c_forward) * vector6;
            localRotation = AngleSpreadX(localRotation2, angle, c_up);
            localRotation = AngleSpreadZ(localRotation, fire_angle + 120f, player.c_player_ship.c_forward);
            localRotation = AngleRandomize(localRotation, 0.25f, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_center.position + vector7, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true);
            vector7 = Quaternion.AngleAxis(fire_angle + 240f, player.c_player_ship.c_forward) * vector6;
            localRotation = AngleSpreadX(localRotation2, angle, c_up);
            localRotation = AngleSpreadZ(localRotation, fire_angle + 240f, player.c_player_ship.c_forward);
            localRotation = AngleRandomize(localRotation, 0.25f, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_center.position + vector7, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type]);
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.4f : ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.3f : 0.3333f));
            }
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                player.c_player_ship.m_fire_angle = (player.c_player_ship.m_fire_angle + 350f) % 360f;
            }
            else
            {
                player.c_player_ship.m_fire_angle = (player.c_player_ship.m_fire_angle + 345f) % 360f;
            }
            player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.2f : 0.16f) * num3 * refire_multiplier;
            flak_fire_count++;
            if (!GameplayManager.IsMultiplayer)
            {
                float num4 = num3 / 1f * RUtility.FIXED_FT_INVERTED;
                player.c_player_ship.c_rigidbody.AddForce(player.c_player_ship.c_forward * (UnityEngine.Random.Range(-40f, -50f) * player.c_player_ship.c_rigidbody.mass * num4));
                player.c_player_ship.c_rigidbody.AddTorque(c_right * (UnityEngine.Random.Range(-150f, -100f) * num4));
            }
        }
    }
}