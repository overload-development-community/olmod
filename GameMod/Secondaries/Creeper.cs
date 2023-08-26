using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Creeper : SecondaryWeapon
    {
        public Creeper()
        {
            displayName = "CREEPER";
            displayNamePlural = "CREEPERS";
            Tag2A = "XS";
            Tag2B = "ST";
            icon_idx = (int)AtlasIndex0.MISSILE_CREEPER1;
            projprefab = ProjPrefabExt.missile_creeper;
            ammo = 48;
            ammoUp = 64;
            ammoSuper = 40;
            AmmoLevelCap = WeaponUnlock.LEVEL_2A;
            MineHoming = true;
            MoveSync = true;
            ExplodeSync = true;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            if (GameplayManager.IsMultiplayerActive)
            {
                float angle = ((!ps.m_alternating_missile_fire) ? 2f : (-2f));
                localRotation = AngleSpreadX(localRotation, angle, c_up);
            }
            else
            {
                float angle2 = ((!ps.m_alternating_missile_fire) ? UnityEngine.Random.Range(0.5f, 2.5f) : UnityEngine.Random.Range(-2.5f, -0.5f));
                localRotation = AngleRandomize(localRotation, 6f, c_up, c_right);
                localRotation = AngleSpreadX(localRotation, angle2, c_up);
            }
            if (ps.m_alternating_missile_fire)
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right2.position, localRotation, 0f, level);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left2.position, localRotation, 0f, level);
            }
            ps.m_alternating_missile_fire = !ps.m_alternating_missile_fire;
            ps.m_refire_missile_time += 0.12f;
            player.PlayCameraShake(CameraShakeType.FIRE_CREEPER, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

            temp_pos.x = pos.x - 62f;
            temp_pos.y = pos.y;

            //UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            UIManager.DrawQuadUIRotated(temp_pos, 10f, 10f, 0f, c2, m_alpha, 35);
            temp_pos.x = pos.x + 62f;
            //UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            UIManager.DrawQuadUIRotated(temp_pos, 10f, 10f, 0f, c2, m_alpha, 35);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                temp_pos.x = pos.x + ((!GameManager.m_player_ship.m_alternating_missile_fire) ? (-62f) : 62f);
                //UIManager.DrawQuadUIRotated(temp_pos, 6f, 6f, (float)System.Math.PI / 4f, c, m_alpha, 4);
                UIManager.DrawQuadUIRotated(temp_pos, 6.8f, 6.8f, 0f, c, m_alpha, 4);
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            Vector3 vector;
            if (GameplayManager.IsMultiplayerActive)
            {
                m_vel_inherit = 0f;
                vector = Vector3.right * 10f;
                proj.m_acceleration += 0.1f;
                proj.m_homing_strength *= 1f;
                m_init_speed *= 1.2f;
            }
            else
            {
                vector = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(15f, 25f);
                proj.m_acceleration += UnityEngine.Random.Range(0f, 0.2f);
                proj.m_homing_strength *= UnityEngine.Random.Range(0.9f, 1.1f);
            }
            proj.c_rigidbody.AddTorque(vector * proj.c_rigidbody.mass);
            proj.m_homing_max_dist = 15f;
            proj.c_rigidbody.drag = ((proj.m_team != ProjTeam.ENEMY) ? 0.8f : 0.6f);
            proj.m_homing_acquire_speed = 20f;
            if (m_upgrade >= WeaponUnlock.LEVEL_1)
            {
                m_lifetime *= 1.4f;
                if (m_upgrade == WeaponUnlock.LEVEL_2A)
                {
                    m_damage *= 3f;
                }
                else if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    m_init_speed *= 1.1f;
                    proj.m_homing_acquire_speed = 50f;
                    proj.m_homing_strength *= 3f;
                    proj.c_rigidbody.drag = 0.85f;
                    proj.m_homing_max_dist = 20f;
                }
            }

            return false;
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if (proj.ShouldPlayDamageEffect(layer))
            {
                proj.Explode(damaged_something: true);
            }
        }

        public override RigidbodyInterpolation Interpolation(Projectile proj)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                return RigidbodyInterpolation.Interpolate;
            }
            else
            {
                return RigidbodyInterpolation.None;
            }
        }
    }
}
