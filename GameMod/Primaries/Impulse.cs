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
            icon_idx = (int)AtlasIndex0.WICON_IMPULSE;
            UsesEnergy = true;
            projprefab = ProjPrefabExt.proj_impulse;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_weapon_level[(int)player.m_weapon_type];

            player.c_player_ship.FiringVolumeModifier = 0.75f;

            if ( level == WeaponUnlock.LEVEL_2A || (GameplayManager.IsMultiplayer && !MPClassic.matchEnabled)) //GameplayManager.IsMultiplayerActive)
            {
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right.position, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 0);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right.position + c_right * ship.QdiffRightX + c_up * ship.QdiffY + c_forward * ship.QdiffZ, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 1);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left.position, localRotation, 0f, WeaponUnlock.LEVEL_2A, true, 2);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left.position + c_right * ship.QdiffLeftX + c_up * ship.QdiffY + c_forward * ship.QdiffZ, localRotation, 0f, WeaponUnlock.LEVEL_2A, false, 3);
                player.c_player_ship.m_refire_time += 0.28f * refire_multiplier;
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy(0.666667f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_IMPULSE, 1.3f, 1.2f);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right.position, localRotation, 0f, level, true, 0);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left.position, localRotation, 0f, level, false, 2);
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.25f : 0.2f) * refire_multiplier;
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.33333f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_IMPULSE, 1f, 1f);
            }
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            float num2 = 45f;
            float num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            // CHECK HERE -- is pos assigned a value, or is it nulled out for some reason?
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 40);
            if (GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] == WeaponUnlock.LEVEL_2A)
            {
                vector.x *= 1.778f;
                vector.y *= 1.778f;
                temp_pos.x = pos.x + vector.x + 3f;
                temp_pos.y = pos.y + vector.y - 5f;
                UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
                temp_pos.x -= 6f;
                temp_pos.y += 10f;
                UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
            }
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 40);
            if (GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] == WeaponUnlock.LEVEL_2A)
            {
                vector.x *= 1.778f;
                vector.y *= 1.778f;
                temp_pos.x = pos.x + vector.x + 3f;
                temp_pos.y = pos.y + vector.y + 5f;
                UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
                temp_pos.x -= 6f;
                temp_pos.y -= 10f;
                UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
            }
        }


        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            bool bigParticles = false;

            if (proj.m_team == ProjTeam.ENEMY)
            {
                proj.m_trail_particle = FXWeaponEffect.trail_impulse2;
            }
            if (m_upgrade == WeaponUnlock.LEVEL_1 || m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                if (MPWeapons.IsOwnedByPlayer(proj))
                {
                    proj.m_trail_particle = FXWeaponEffect.trail_impulse_strong;
                }
                bigParticles = true;
                m_damage *= 1.2f;
                m_push_force *= 1.2f;
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    m_init_speed *= 1.4f;
                }
            }
            else if (m_upgrade == WeaponUnlock.LEVEL_2A)
            {
                proj.m_firing_sfx = SFXCue.weapon_impulse_lvl2a;
            }
            else
            {
                proj.m_firing_sfx = SFXCue.weapon_impulse_lvl0;
            }

            return bigParticles;
        }
    }
}