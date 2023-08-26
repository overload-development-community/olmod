using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Devastator : SecondaryWeapon
    {
        static float[] bomblet_angles = new float[6] { 40f, 90f, 140f, 220f, 270f, 320f };

        public Devastator()
        {
            displayName = "DEVASTATOR";
            displayNamePlural = "DEVASTATORS";
            Tag2A = "PT";
            Tag2B = "XS";
            icon_idx = (int)AtlasIndex0.MISSILE_DEVASTATOR1;
            projprefab = ProjPrefabExt.missile_devastator;
            subproj = ProjPrefabExt.missile_devastator_mini;
            ammo = 4;
            ammoUp = 8;
            ammoSuper = 2;
            firingMode = FiringMode.DETONATOR;
            WarnSelect = true;
            ExplodeSync = true;
        }

        public override void Fire(float refire_multiplier)
        {
            //Vector3 c_right = ship.c_right;
            //Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center2.position, localRotation, 0f, level);
            ps.m_refire_missile_time += (level < WeaponUnlock.LEVEL_1) ? 1f : 20f;
            player.PlayCameraShake(CameraShakeType.FIRE_DEVASTATOR, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            //Color c2 = UIManager.m_col_ui2;
            Color c2 = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_hi6, UnityEngine.Random.value * UIElement.FLICKER);

            temp_pos.x = pos.x;
            temp_pos.y = pos.y + 48f;
            UIManager.DrawQuadUIRotated(temp_pos, 10.8f, 10.8f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                UIManager.DrawQuadUIRotated(temp_pos, 6f, 6f, (float)System.Math.PI / 4f, c, m_alpha, 4);
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (proj.m_type == (ProjPrefab)projprefab)
            {
                proj.c_collider.isTrigger = false;
                if (GameplayManager.IsMultiplayerActive)
                {
                    m_init_speed *= 0.85f;
                }
                m_death_particle_override = proj.m_death_particle_default;
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    m_damage *= 2f;
                    m_death_particle_override = proj.m_death_particle_damage;
                }
                else if (m_upgrade == WeaponUnlock.LEVEL_2A)
                {
                    proj.c_collider.isTrigger = true;
                    m_init_speed *= 1.4f;
                    m_damage = 300f;
                }
            }
            else // subproj
            {
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    m_lifetime += UnityEngine.Random.Range(0f, 0.1f);
                    if (proj.m_team == ProjTeam.ENEMY)
                    {
                        proj.m_firing_sfx = SFXCue.mssile_pod;
                        proj.m_muzzle_flash_particle = FXWeaponEffect.muzzle_flash_missile_pod;
                        m_init_speed *= 2.5f;
                        m_lifetime *= 1.5f;
                    }
                    else
                    {
                        m_init_speed *= 1.2f;
                        m_damage *= 2f;
                    }
                }
                else if (m_upgrade == WeaponUnlock.LEVEL_2A && proj.m_team == ProjTeam.ENEMY)
                {
                    m_init_speed *= 1.1f;
                    m_lifetime *= 1.5f;
                }
            }

            return false;
        }

        public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            base.Explode(proj, damaged_something, m_death_particle_override, strength, m_upgrade);

            if (proj.m_type == (ProjPrefab)projprefab)
            {
                int num = ((m_upgrade != WeaponUnlock.LEVEL_2B) ? 3 : 4);
                Quaternion localRotation = proj.c_transform.localRotation;
                Vector3 localPosition = proj.c_transform.localPosition;
                for (int i = 0; i < num; i++)
                {
                    float num2 = (float)i / (float)num * 180f;
                    for (int j = 0; j < 4; j++)
                    {
                        float angle_x = bomblet_angles[j] + UnityEngine.Random.Range(-15f, 15f);
                        Quaternion rot = RUtility.AngleSpreadXZ(localRotation, angle_x, num2 + UnityEngine.Random.Range(-15f, 15f));
                        if (Server.IsActive() && (bool)proj.m_owner_player)
                        {
                            ProjectileManager.PlayerFire(proj.m_owner_player, (ProjPrefab)subproj, localPosition, rot, 0f, m_upgrade);
                        }
                    }
                }
                if (proj.m_owner_player != null)
                {
                    proj.m_owner_player.c_player_ship.m_refire_missile_time = 0.3f;
                    proj.m_owner_player.MaybeSwitchToNextMissile();
                }
            }
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if ((int)proj.m_type == (int)projprefab)
            {
                Debug.Log("CCF exploding Dev main projectile");
                base.ProcessCollision(proj, collider, collision_normal, layer, ref m_bounce_allow, ref m_bounces, ref m_cur_target, ref m_cur_target_player, ref m_cur_target_robot, ref m_damage, ref m_lifetime, ref m_target_timer, m_trail_effect_pe);
            }
            else // subproj
            {
                if (proj.ShouldPlayDamageEffect(layer))
                {
                    proj.Explode(damaged_something: true);
                }
            }
        }
    }
}
