using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Reflex : PrimaryWeapon
    {
        public Reflex()
        {
            displayName = "REFLEX";
            Tag2A = "RF";
            Tag2B = "L";
            icon_idx = (int)AtlasIndex0.WICON_REFLEX;
            UsesEnergy = true;
            projprefab = ProjPrefabExt.proj_reflex;
            bounceFX = FXWeaponEffect.reflex_bounce;
        }

        public override void Fire(float refire_multiplier)
        {
            player.c_player_ship.FiringVolumeModifier = 0.75f;
            if (player.c_player_ship.m_alternating_fire)
            {
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, player.c_player_ship.c_transform.localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, player.c_player_ship.c_transform.localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            }
            player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.1f : 0.08f) * refire_multiplier;
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy(0.3f);
            }
            player.PlayCameraShake(CameraShakeType.FIRE_REFLEX, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            float num2 = 65f;
            float num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            float a = m_alpha * ((!GameManager.m_player_ship.m_alternating_fire) ? 1f : 0.6f);
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            temp_pos.x = pos.x + vector.x * 1.45f;
            temp_pos.y = pos.y + vector.y * 1.45f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            a = m_alpha * ((!GameManager.m_player_ship.m_alternating_fire) ? 0.6f : 1f);
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            temp_pos.x = pos.x + vector.x * 1.45f;
            temp_pos.y = pos.y + vector.y * 1.45f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                m_upgrade = WeaponUnlock.LEVEL_0;
            }
            if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                proj.m_firing_sfx = SFXCue.weapon_reflex_lvl2B;
                m_lifetime *= 1.4f;
                m_bounces = -1;
            }

            return false;
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if (proj.ShouldPlayDamageEffect(layer))
            {
                proj.Explode(damaged_something: true);
            }
            else if (m_bounce_allow)
            {
                m_damage *= 0.85f;
            }
        }
    }
}