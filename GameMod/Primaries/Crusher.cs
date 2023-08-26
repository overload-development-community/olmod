using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Crusher : PrimaryWeapon
    {
        public Crusher()
        {
            displayName = "CRUSHER";
            Tag2A = "DX";
            Tag2B = "AF";
            icon_idx = (int)AtlasIndex0.WICON_CRUSHER;
            UsesAmmo = true;
            projprefab = ProjPrefabExt.proj_shotgun;
            firingMode = FiringMode.SEMI_AUTO;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            Vector3 position = player.c_player_ship.m_muzzle_left.position;
            Vector3 position2 = player.c_player_ship.m_muzzle_right.position;
            float num2 = 0.15f;
            Quaternion localRotation2 = player.c_player_ship.c_transform.localRotation;
            Vector2 offset = default(Vector2);
            Vector3 pos = default(Vector3);
            float angle;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                angle = 0.5f;
                for (int i = 0; i < 7; i++)
                {
                    if (player.c_player_ship.m_alternating_fire)
                    {
                        offset.x = UnityEngine.Random.Range(0f - num2, num2);
                        offset.y = UnityEngine.Random.Range(0f - num2, num2);
                        pos.x = position.x + offset.x * c_right.x + offset.y * c_up.x;
                        pos.y = position.y + offset.x * c_right.y + offset.y * c_up.y;
                        pos.z = position.z + offset.x * c_right.z + offset.y * c_up.z;
                        Quaternion localRotation = AngleSpreadY(localRotation2, 2f * offset.x / num2, c_right);
                        localRotation = AngleSpreadX(localRotation, 2f * offset.y / num2, c_up);
                        localRotation = AngleRandomize(localRotation, angle, c_up, c_right);
                        // originally ProjectileManager.PlayerFire() 
                        MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, pos, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], i < 6, 0);
                    }
                    else
                    {
                        offset.x = UnityEngine.Random.Range(0f - num2, num2);
                        offset.y = UnityEngine.Random.Range(0f - num2, num2);
                        pos.x = position2.x + offset.x * c_right.x + offset.y * c_up.x;
                        pos.y = position2.y + offset.x * c_right.y + offset.y * c_up.y;
                        pos.z = position2.z + offset.x * c_right.z + offset.y * c_up.z;
                        Quaternion localRotation = AngleSpreadY(localRotation2, 2f * offset.x / num2, c_right);
                        localRotation = AngleSpreadX(localRotation, 2f * offset.y / num2, c_up);
                        localRotation = AngleRandomize(localRotation, angle, c_up, c_right);
                        MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, pos, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], i < 6, 1);
                    }
                }
                if (player.c_player_ship.IsCockpitVisible)
                {
                    if (player.c_player_ship.m_alternating_fire)
                    {
                        ParticleManager.psm[2].StartParticle(6, position2, localRotation2, player.c_player_ship.c_transform);
                    }
                    else
                    {
                        ParticleManager.psm[2].StartParticle(6, position, localRotation2, player.c_player_ship.c_transform);
                    }
                }
                player.c_player_ship.m_refire_time += 0.2f;
                player.UseAmmo(3);
                if (!GameplayManager.IsMultiplayer)
                {
                    player.c_player_ship.c_rigidbody.AddForce(c_forward * (UnityEngine.Random.Range(-100f, -150f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                    player.c_player_ship.c_rigidbody.AddTorque(c_right * (UnityEngine.Random.Range(-300f, -200f) * RUtility.FIXED_FT_INVERTED));
                }
                player.PlayCameraShake(CameraShakeType.FIRE_CRUSHER, 1f, 0.8f);
                return;
            }
            angle = ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.75f : 0.5f);
            for (int j = 0; j < 7; j++)
            {
                offset.x = UnityEngine.Random.Range(0f - num2, num2);
                offset.y = UnityEngine.Random.Range(0f - num2, num2);
                pos.x = position2.x + offset.x * c_right.x + offset.y * c_up.x;
                pos.y = position2.y + offset.x * c_right.y + offset.y * c_up.y;
                pos.z = position2.z + offset.x * c_right.z + offset.y * c_up.z;
                Quaternion localRotation = AngleSpreadY(localRotation2, 2.25f * offset.x / num2, c_right);
                localRotation = AngleSpreadX(localRotation, 2.25f * offset.y / num2, c_up);
                localRotation = AngleRandomize(localRotation, angle, c_up, c_right);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, pos, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
                offset.x = UnityEngine.Random.Range(0f - num2, num2);
                offset.y = UnityEngine.Random.Range(0f - num2, num2);
                pos.x = position.x + offset.x * c_right.x + offset.y * c_up.x;
                pos.y = position.y + offset.x * c_right.y + offset.y * c_up.y;
                pos.z = position.z + offset.x * c_right.z + offset.y * c_up.z;
                localRotation = AngleSpreadY(localRotation2, 2.25f * offset.x / num2, c_right);
                localRotation = AngleSpreadX(localRotation, 2.25f * offset.y / num2, c_up);
                localRotation = AngleRandomize(localRotation, angle, c_up, c_right);
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, pos, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], j < 6, 1);
            }
            if (player.c_player_ship.IsCockpitVisible)
            {
                ParticleManager.psm[2].StartParticle(6, position2, localRotation2, player.c_player_ship.c_transform);
                ParticleManager.psm[2].StartParticle(6, position, localRotation2, player.c_player_ship.c_transform);
            }
            if (player.m_overdrive)
            {
                if (GameplayManager.IsMultiplayerActive)
                {
                    player.c_player_ship.m_refire_time += 0.55f;
                }
                else
                {
                    player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.5f : 0.35f);
                }
            }
            else if (GameplayManager.IsMultiplayerActive)
            {
                player.c_player_ship.m_refire_time += 0.45f;
            }
            else
            {
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.5f : 0.3f);
            }
            player.UseAmmo(6);
            if (!GameplayManager.IsMultiplayer)
            {
                player.c_player_ship.c_rigidbody.AddForce(c_forward * (UnityEngine.Random.Range(-150f, -200f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                player.c_player_ship.c_rigidbody.AddTorque(c_right * (UnityEngine.Random.Range(-500f, -400f) * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_CRUSHER, 2f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            float a = m_alpha * ((!(GameManager.m_player_ship.m_refire_time <= 0f)) ? 0.2f : 1f);
            float num2 = 85f;
            float num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 39);
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 39);
            num3 = (float)System.Math.PI / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 39);
            num3 = 5.23598766f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 39);
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            bool bigParticles = false;

            if (m_upgrade == WeaponUnlock.LEVEL_2A)
            {
                proj.m_trail_renderer = FXTrailRenderer.trail_renderer_crusher4;
                bigParticles = true;
                proj.m_firing_sfx = SFXCue.weapon_shotgun_lvl2a;
                m_damage *= 1.3f;
                m_push_torque *= 1.1f;
                m_push_force *= 1.1f;
            }
            else
            {
                if (MPWeapons.IsOwnedByPlayer(proj))
                {
                    //proj.m_trail_renderer = (FXTrailRenderer)(6 + Projectile.CrusherNextTrailIndex);
                    //Projectile.CrusherNextTrailIndex = (Projectile.CrusherNextTrailIndex + 1) % 3;
                    if (!GameplayManager.IsMultiplayer)
                    {
                        proj.m_trail_renderer = FXTrailRenderer.trail_renderer_crusher + Projectile.CrusherNextTrailIndex;
                        Projectile.CrusherNextTrailIndex = (Projectile.CrusherNextTrailIndex + 1) % 3;
                    }
                }
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    proj.m_firing_sfx = SFXCue.weapon_shotgun_lvl2b;
                }
            }

            return bigParticles;
        }

        public override RigidbodyInterpolation Interpolation(Projectile proj)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                return RigidbodyInterpolation.None;
            }
            else
            {
                return RigidbodyInterpolation.Interpolate;
            }
        }
    }
}
