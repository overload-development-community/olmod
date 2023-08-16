using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class MissilePod : SecondaryWeapon
    {
        public MissilePod()
        {
            displayName = "MISSILE POD";
            displayNamePlural = "MISSILE PODS";
            Tag2A = "XS";
            Tag2B = "ST";
            icon_idx = (int)AtlasIndex0.MISSILE_POD1;
            projprefab = ProjPrefabExt.missile_pod;
            ammo = 100;
            ammoUp = 120;
            ammoSuper = 80;
            AmmoLevelCap = WeaponUnlock.LEVEL_2A;
            firingMode = FiringMode.STREAM;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            if (!GameplayManager.IsMultiplayerActive)
            {
                float angle3 = ((player.m_missile_level[(int)player.m_missile_type] != WeaponUnlock.LEVEL_0) ? 3.5f : 2.5f);
                localRotation = AngleRandomize(localRotation, angle3, c_up, c_right);
            }
            if (ps.m_alternating_missile_fire)
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right2.position, localRotation, 0f, level);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left2.position, localRotation, 0f, level);
            }
            player.PlayCameraShake(CameraShakeType.FIRE_MISSILE_POD, 1f, 1f);
            ps.m_refire_missile_time += 0.11f;
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

            temp_pos.x = pos.x - 62f;
            temp_pos.y = pos.y;
            UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            temp_pos.x = pos.x + 62f;
            UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                temp_pos.x = pos.x + ((!GameManager.m_player_ship.m_alternating_missile_fire) ? (-62f) : 62f);
                UIManager.DrawQuadUIRotated(temp_pos, 4.8f, 4.8f, (float)System.Math.PI / 4f, c, m_alpha, 4);
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (m_upgrade <= WeaponUnlock.LEVEL_0)
            {
                proj.m_homing_strength = 0f;
            }
            if (m_upgrade == WeaponUnlock.LEVEL_2A)
            {
                m_death_particle_override = proj.m_death_particle_damage;
                m_damage *= 1.1f;
            }
            else
            {
                m_death_particle_override = proj.m_death_particle_default;
            }
            if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                proj.m_homing_strength *= 2f;
            }

            return false;
        }
    }
}
