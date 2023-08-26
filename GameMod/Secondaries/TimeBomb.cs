using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class TimeBomb : SecondaryWeapon
    {
        public TimeBomb()
        {
            displayName = "TIME BOMB";
            displayNamePlural = "TIME BOMBS";
            Tag2A = "LT";
            Tag2B = "XS";
            icon_idx = (int)AtlasIndex0.MISSILE_TIMEBOMB1;
            projprefab = ProjPrefabExt.missile_timebomb;
            ammo = 6;
            ammoUp = 10;
            ammoSuper = 3;
            MineHoming = true;
            MoveSync = true;
            ExplodeSync = true;
            firingMode = FiringMode.SEMI_AUTO;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            Vector3 c_forward = ship.c_forward;
            //Quaternion localRotation = ps.c_transform.localRotation;
            Quaternion rot = AngleRandomize(ps.c_transform.localRotation, 0.1f, c_up, c_right);

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center2.position, rot, 0f, level);
            ps.m_refire_missile_time += 1f;
            if (!GameplayManager.IsMultiplayer)
            {
                ps.c_rigidbody.AddForce(c_forward * (UnityEngine.Random.Range(-200f, -250f) * ps.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                ps.c_rigidbody.AddTorque(c_right * (UnityEngine.Random.Range(-1500f, -1000f) * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_TIMEBOMB, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

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
            if (GameplayManager.IsMultiplayerActive)
            {
                m_vel_inherit = 0f;
                m_death_particle_override = proj.m_death_particle_damage;
                m_damage *= 2f;
                proj.m_homing_strength = 10f;
                proj.m_homing_min_dot = -1f;
                m_lifetime += 0.5f;
                m_init_speed *= 1.5f;
                proj.m_homing_max_dist = 25f;
            }
            else if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                m_death_particle_override = proj.m_death_particle_damage;
                m_init_speed *= 1.4f;
                m_damage *= 2f;
            }
            else
            {
                m_death_particle_override = proj.m_death_particle_default;
            }

            return false;
        }

        public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            base.Explode(proj, damaged_something, m_death_particle_override, strength, m_upgrade);

            float num8 = ((m_upgrade != WeaponUnlock.LEVEL_2A) ? 5f : 7.5f);
            if (NetworkManager.IsServer())
            {
                float num9 = ((!GameplayManager.IsMultiplayer) ? Mathf.Min(num8 * 2f, GameplayManager.SLOW_MO_TIMER + num8) : 0.25f);
                Server.SendSlowMoTimerToAllClients(Mathf.RoundToInt(num9 * 1000000f));
            }
            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.wep_missile_timebomb_on, 1f, 0f, 0f, reverb: true);
            if (m_upgrade >= WeaponUnlock.LEVEL_1)
            {
                RobotManager.StunAll(proj.c_transform.position, (m_upgrade != WeaponUnlock.LEVEL_2A) ? 2.5f : 3f, (m_upgrade != WeaponUnlock.LEVEL_2A) ? 1.5f : 2f, 1f);
            }
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if (proj.ShouldPlayDamageEffect(layer))
            {
                proj.Explode(damaged_something: true);
            }
            else
            {
                Vector3 vector = Vector3.Cross(collision_normal, proj.c_transform.forward);
                if (proj.c_rigidbody != null)
                {
                    proj.c_rigidbody.AddTorque(proj.c_rigidbody.mass * 5f * vector, ForceMode.Impulse);
                    proj.c_rigidbody.AddForce(proj.c_rigidbody.mass * collision_normal, ForceMode.Impulse);
                }
                m_target_timer = 0.3f;
                m_cur_target = null;
                m_cur_target_robot = null;
                m_cur_target_player = null;
                if (m_trail_effect_pe != null)
                {
                    m_trail_effect_pe.DelayedDestroy(proj.m_trail_post_lifetime, detach: true);
                }
            }
        }
    }
}
