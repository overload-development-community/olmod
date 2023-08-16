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
            icon_idx = (int)AtlasIndex0.WICON_LANCER;
            UsesEnergy = true;
            projprefab = ProjPrefabExt.proj_beam;
            firingMode = FiringMode.SEMI_AUTO;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            Quaternion localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, (player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0f : 0.2f, c_up, c_right);
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                if (player.c_player_ship.m_alternating_fire)
                {
                    MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
                }
                else
                {
                    MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
                }
                //player.c_player_ship.m_alternating_fire = !player.c_player_ship.m_alternating_fire; // This is done in 3 different places, in a row. Seems like overkill.
                player.c_player_ship.m_refire_time += 0.133333f * refire_multiplier;
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy(1f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 1f, 1f);
                return;
            }
            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
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

        // REFERENCES TO M_WEAPON_LEVEL need to be un-hardcoded -- done
        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            float num2 = 35f;
            float num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            num2 = 70f;
            num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            float a = m_alpha * ((!(GameManager.m_player_ship.m_refire_time <= 0f)) ? 0.2f : 1f);
            float a2 = a * ((GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] != WeaponUnlock.LEVEL_2B || !GameManager.m_player_ship.m_alternating_fire) ? 1f : 0.15f);
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a2, 39);
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            a2 = a * ((GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] != WeaponUnlock.LEVEL_2B || GameManager.m_player_ship.m_alternating_fire) ? 1f : 0.15f);
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a2, 39);
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                m_init_speed *= 0.4f;
            }
            if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                proj.m_firing_sfx = SFXCue.weapon_lancer_lvl2b;
            }

            return false;
        }
    }
}