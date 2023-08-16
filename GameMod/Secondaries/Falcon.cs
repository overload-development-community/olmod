using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Falcon : SecondaryWeapon
    {
        public Falcon()
        {
            displayName = "FALCON";
            displayNamePlural = "FALCONS";
            Tag2A = "MX";
            Tag2B = "T";
            icon_idx = (int)AtlasIndex0.MISSILE_FALCON1;
            projprefab = ProjPrefabExt.missile_falcon;
            ammo = 20;
            ammoUp = 24;
            ammoSuper = 16;
            AmmoLevelCap = WeaponUnlock.LEVEL_0;
        }

        public override void Fire(float refire_multiplier)
        {
            //Vector3 c_right = ship.c_right;
            //Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            if (ps.m_alternating_missile_fire)
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right2.position, localRotation, 0f, level);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left2.position, localRotation, 0f, level);
            }
            ps.m_alternating_missile_fire = !ps.m_alternating_missile_fire;
            ps.m_refire_missile_time += ((level != WeaponUnlock.LEVEL_2A) ? 0.3f : 0.22f);
            player.PlayCameraShake(CameraShakeType.FIRE_FALCON, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

            temp_pos.x = pos.x - 62f;
            temp_pos.y = pos.y;

            //UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            UIManager.DrawQuadUIRotated(temp_pos, 10f, 10f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            temp_pos.x = pos.x + 62f;
            //UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            UIManager.DrawQuadUIRotated(temp_pos, 10f, 10f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                temp_pos.x = pos.x + ((!GameManager.m_player_ship.m_alternating_missile_fire) ? (-62f) : 62f);
                //UIManager.DrawQuadUIRotated(temp_pos, 6f, 6f, (float)System.Math.PI / 4f, c, m_alpha, 4);
                UIManager.DrawQuadUIRotated(temp_pos, 7f, 7f, (float)System.Math.PI / 4f, c, m_alpha, 4);
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (m_upgrade >= WeaponUnlock.LEVEL_1)
            {
                m_death_particle_override = proj.m_death_particle_default;
                if (m_upgrade == WeaponUnlock.LEVEL_2A)
                {
                    m_init_speed *= 1.65f;
                    m_damage *= 1.5f;
                }
                else if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    proj.m_homing_strength = 0.3f;
                }
            }
            else
            {
                m_death_particle_override = proj.m_death_particle_damage;
            }

            return false;
        }
    }
}
