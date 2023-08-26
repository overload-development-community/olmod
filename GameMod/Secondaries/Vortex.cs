using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Vortex : SecondaryWeapon
    {
        public Vortex()
        {
            displayName = "VORTEX";
            //displayNamePlural = "VORTEXES";
            displayNamePlural = "VORTICES"; // :D :D :D
            Tag2A = "XG";
            Tag2B = "LP";
            icon_idx = (int)AtlasIndex0.MISSILE_VORTEX1;
            projprefab = ProjPrefabExt.missile_vortex;
            ammo = 12;
            ammoUp = 18;
            ammoSuper = 8;
            firingMode = FiringMode.SEMI_AUTO;
            bounceExp = FXWeaponExplosion.gun_sparks_ff2;
        }

        public override void Fire(float refire_multiplier)
        {
            //Vector3 c_right = ship.c_right;
            //Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center2.position, localRotation, 0f, level);
            ps.m_refire_missile_time += 0.5f;
            player.PlayCameraShake(CameraShakeType.FIRE_VORTEX, 1f, 1f);
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
                m_death_particle_override = FXWeaponExplosion.missile_explosion_vortex_mp;
                proj.m_death_sfx = SFXCue.exp_nova;
            }
            else
            {
                if (proj.m_team == ProjTeam.ENEMY)
                {
                    m_bounces = 2;
                    m_lifetime *= UnityEngine.Random.Range(1.2f, 1.4f);
                }
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    m_death_particle_override = proj.m_death_particle_damage;
                    proj.m_death_sfx = SFXCue.exp_vortex_long;
                }
                else
                {
                    m_death_particle_override = proj.m_death_particle_default;
                }
            }
            return false;
        }

        public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            FXWeaponExplosion explosion = FXWeaponExplosion.none;

            if (m_death_particle_override != 0)
            {
                explosion = m_death_particle_override;
            }
            else if (damaged_something && proj.m_death_particle_damage != 0)
            {
                explosion = proj.m_death_particle_damage;
            }
            else if (proj.m_death_particle_default != 0)
            {
                explosion = proj.m_death_particle_default;
            }
            if (explosion != FXWeaponExplosion.none)
            {
                ParticleManager.psm[3].StartParticle((int)explosion, proj.c_transform.localPosition, proj.c_transform.localRotation, null, proj);
            }
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if (proj.ShouldPlayDamageEffect(layer))
            {
                proj.Explode(damaged_something: true);
            }
            if (m_bounces < proj.m_bounce_max_count)
            {
                SFXCueManager.PlayRawSoundEffectPos(SoundEffect.imp_energy_bot3, proj.c_transform.localPosition, 0.6f, UnityEngine.Random.Range(0f, 0.1f));
            }
        }
    }
}
