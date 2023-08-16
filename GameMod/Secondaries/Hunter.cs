using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Hunter : SecondaryWeapon
    {
        public Hunter()
        {
            displayName = "HUNTER";
            displayNamePlural = "HUNTERS";
            Tag2A = "W";
            Tag2B = "XT";
            icon_idx = (int)AtlasIndex0.MISSILE_HUNTER1;
            projprefab = ProjPrefabExt.missile_hunter;
            ammo = 16;
            ammoUp = 20;
            ammoSuper = 12;
            AmmoLevelCap = WeaponUnlock.LEVEL_2B;
        }

        public override void Fire(float refire_multiplier)
        {
            //Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;
            Quaternion localRotation = ps.c_transform.localRotation;

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            if (level == WeaponUnlock.LEVEL_2A)
            {
                ProjectileManager.PlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center2.position, localRotation, 0f, level, no_sound: true);
            }
            Quaternion rot = AngleSpreadX(localRotation, 0.5f, c_up);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right2.position, rot, 0f, level, no_sound: true);
            rot = AngleSpreadX(localRotation, -0.5f, c_up);
            ProjectileManager.PlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left2.position, rot, 0f, level);
            ps.m_refire_missile_time += 0.35f;
            player.PlayCameraShake(CameraShakeType.FIRE_HUNTER, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

            temp_pos.x = pos.x - 62f;
            temp_pos.y = pos.y;
            UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 34);
            temp_pos.x = pos.x + 62f;
            UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 34);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                UIManager.DrawQuadUIRotated(temp_pos, 4.8f, 4.8f, (float)System.Math.PI / 4f, c, m_alpha, 4);
                temp_pos.x = pos.x - 62f;
                UIManager.DrawQuadUIRotated(temp_pos, 4.8f, 4.8f, (float)System.Math.PI / 4f, c, m_alpha, 4);
            }
            if (GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] == WeaponUnlock.LEVEL_2A)
            {
                temp_pos.x = pos.x;
                temp_pos.y = pos.y + 48f;
                UIManager.DrawQuadUIRotated(temp_pos, 8.4f, 8.4f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
                if (GameManager.m_local_player.CanFireMissileAmmo())
                {
                    UIManager.DrawQuadUIRotated(temp_pos, 4.8f, 4.8f, (float)System.Math.PI / 4f, c, m_alpha, 4);
                }
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (m_upgrade >= WeaponUnlock.LEVEL_1)
            {
                m_damage *= 1.6f;
                if (m_upgrade == WeaponUnlock.LEVEL_2B)
                {
                    proj.m_homing_strength *= 1.4f;
                    m_init_speed *= 1.2f;
                }
            }
            if (!MPWeapons.IsOwnedByPlayer(proj))
            {
                proj.c_transform.localScale = Vector3.one * 1.5f;
            }
            else if (GameplayManager.IsMultiplayerActive)
            {
                proj.m_homing_strength *= 0.9f;
            }

            return false;
        }
    }
}
