using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Flak : PrimaryWeapon
    {
        public Flak()
        {
            displayName = "FLAK";
            Tag2A = "DX";
            Tag2B = "VK";
            icon_idx = (int)AtlasIndex0.WICON_FLAK;
            UsesAmmo = true;
            projprefab = ProjPrefabExt.proj_flak_cannon;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            Quaternion localRotation;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                if (ship.flak_fire_count == 0)
                {
                    GameManager.m_audio.PlayCue2D(337, 0.7f, 0.5f, 0f, true);
                    GameManager.m_audio.PlayCue2D(338, 0.7f, 0.5f, 0f, true);
                    ship.flak_fire_count++;
                    player.c_player_ship.m_refire_time = 0.15f;
                    return;
                }
                float angle = 5f + (float)Mathf.Min(7, ship.flak_fire_count) * 0.2f;
                localRotation = AngleRandomize(player.c_player_ship.c_transform_rotation, angle, c_up, c_right);
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
                localRotation = AngleRandomize(player.c_player_ship.c_transform_rotation, angle, c_up, c_right);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
                player.c_player_ship.m_refire_time += 0.08f + (float)Mathf.Max(0, 4 - ship.flak_fire_count) * 0.01f;
                if (!GameplayManager.IsMultiplayer)
                {
                    player.c_player_ship.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(20f, 30f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                    player.c_player_ship.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(2f, 3f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                }
                player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.9f, 1.1f);
                ship.flak_fire_count++;
                player.UseAmmo(1);
                //player.UseAmmo(2);
                return;
            }
            float angle2 = ((!GameplayManager.IsMultiplayerActive) ? 8f : 6f);
            if (player.c_player_ship.m_alternating_fire)
            {
                localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, angle2, c_up, c_right);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.095f : 0.075f);
                return;
            }
            localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, angle2, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            player.c_player_ship.m_refire_time += 0.03f;
            if (!GameplayManager.IsMultiplayer)
            {
                player.c_player_ship.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(10f, 20f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                player.c_player_ship.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(1f, 2f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.6f, 1f);
            player.UseAmmo(1);
            //player.UseAmmo(2);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();
            Color color;

            float w;
            float num2;
            if (GameManager.m_player_ship.FlakInRange)
            {
                color = UIManager.m_col_hi7;
                w = 0.4f;
                num2 = 103f;
            }
            else
            {
                color = UIManager.m_col_ui3;
                w = 0.3f;
                num2 = 105f;
            }
            float num3 = (float)System.Math.PI / 4f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = (float)System.Math.PI * 3f / 4f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = 3.926991f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = 5.49778748f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = 4.712389f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = (float)System.Math.PI / 2f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            bool bigParticles = false; ;
            
            if (MPWeapons.IsOwnedByPlayer(proj))
            {
                proj.m_trail_particle = FXWeaponEffect.none;
                proj.m_trail_renderer = FXTrailRenderer.none;
                if (GameplayManager.IsMultiplayer)
                {
                    m_init_speed *= 1.15f;
                    proj.m_firing_sfx = SFXCue.weapon_flak_lvl2b;
                }
            }
            if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                m_lifetime *= 1.5f;
                m_damage *= 1.2f;
                proj.m_firing_sfx = SFXCue.weapon_flak_lvl2b;
            }
            else if (m_upgrade == WeaponUnlock.LEVEL_2A)
            {
                bigParticles = true;
                proj.m_firing_sfx = SFXCue.weapon_flak_lvl2a;
                m_damage *= 1.6f;
                m_push_torque *= 1.2f;
                proj.m_stun_multiplier *= 1.2f;
            }

            return bigParticles;
        }
    }
}