using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Cyclone : PrimaryWeapon
    {
        public static int CycloneSpinupStartingStep = 0;

        public Cyclone()
        {
            displayName = "CYCLONE";
            Tag2A = "X4";
            Tag2B = "F";
            icon_idx = (int)AtlasIndex0.WICON_CYCLONE;
            UsesEnergy = true;
            projprefab = ProjPrefabExt.proj_vortex;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;           

            if (ship.flak_fire_count == 0)
            {
                ship.flak_fire_count = CycloneSpinupStartingStep;
            }
            
            float num3 = 1f - Mathf.Min((float)ship.flak_fire_count * 0.05f, (player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.25f);
            player.c_player_ship.FiringPitchModifier = ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? (0.6f - num3) : (0.75f - num3)) * 0.25f;
            player.c_player_ship.FiringVolumeModifier = 0.75f;
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
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_center.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true);
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
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_center.position + vector7, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true);

            vector7 = Quaternion.AngleAxis(fire_angle + 120f, player.c_player_ship.c_forward) * vector6;
            localRotation = AngleSpreadX(localRotation2, angle, c_up);
            localRotation = AngleSpreadZ(localRotation, fire_angle + 120f, player.c_player_ship.c_forward);
            localRotation = AngleRandomize(localRotation, 0.25f, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_center.position + vector7, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true);

            vector7 = Quaternion.AngleAxis(fire_angle + 240f, player.c_player_ship.c_forward) * vector6;
            localRotation = AngleSpreadX(localRotation2, angle, c_up);
            localRotation = AngleSpreadZ(localRotation, fire_angle + 240f, player.c_player_ship.c_forward);
            localRotation = AngleRandomize(localRotation, 0.25f, c_up, c_right);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_center.position + vector7, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type]);

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
            ship.flak_fire_count++;
            if (!GameplayManager.IsMultiplayer)
            {
                float num4 = num3 / 1f * RUtility.FIXED_FT_INVERTED;
                player.c_player_ship.c_rigidbody.AddForce(player.c_player_ship.c_forward * (UnityEngine.Random.Range(-40f, -50f) * player.c_player_ship.c_rigidbody.mass * num4));
                player.c_player_ship.c_rigidbody.AddTorque(c_right * (UnityEngine.Random.Range(-150f, -100f) * num4));
            }
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            float num2 = 30f;
            float num3 = GameManager.m_player_ship.m_fire_angle * (-(float)System.Math.PI / 180f) + (float)System.Math.PI;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
            num3 += (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
            num3 += (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
            num2 = 50f;
            num3 = 0f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 38);
            num3 += (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 38);
            num3 += (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 38);
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            bool bigParticles = false;

            if (m_upgrade >= WeaponUnlock.LEVEL_1)
            {
                m_lifetime = 5f;
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    proj.m_firing_sfx = SFXCue.weapon_cyclone_lvl2B;
                    m_init_speed *= 1.3f;
                }
                else if (m_upgrade == WeaponUnlock.LEVEL_2A)
                {
                    bigParticles = true;
                    proj.m_firing_sfx = SFXCue.weapon_cyclone_lvl2A;
                }
            }
            else if (proj.m_team == ProjTeam.ENEMY)
            {
                m_lifetime = 0.5f;
            }

            return bigParticles;
        }
    }
}