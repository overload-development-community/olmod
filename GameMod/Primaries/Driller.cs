using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Driller : PrimaryWeapon
    {
        public Driller()
        {
            displayName = "DRILLER";
            Tag2A = "DX";
            Tag2B = "M";
            icon_idx = (int)AtlasIndex0.WICON_DRILLER;
            UsesAmmo = true;
			projprefab = ProjPrefabExt.proj_driller;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            player.c_player_ship.FiringVolumeModifier = 0.75f;
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                Quaternion localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, 0.6f, c_up, c_right);
                // originally ProjectileManager.PlayerFire()
                MPSniperPackets.MaybePlayerFire(player, ProjPrefab.proj_driller_mini, player.c_player_ship.m_muzzle_center.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
                player.c_player_ship.m_refire_time += 0.11f;
                player.PlayCameraShake(CameraShakeType.FIRE_DRILLER, 0.7f, 0.7f);
                player.UseAmmo(1);
            }
            else
            {
                MPSniperPackets.MaybePlayerFire(rot: (!GameplayManager.IsMultiplayerActive)
                    ? AngleRandomize(player.c_player_ship.c_transform.localRotation, 0.1f, c_up, c_right)
                    : player.c_player_ship.c_transform.localRotation, player: player, type: (ProjPrefab)projprefab, pos: player.c_player_ship.m_muzzle_center.position, strength: 0f, upgrade_lvl: player.m_weapon_level[(int)player.m_weapon_type], no_sound: false, slot: 0);
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.26f : 0.22f);
                player.PlayCameraShake(CameraShakeType.FIRE_DRILLER, 1f, 1f);
                player.UseAmmo(2);
            }
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
			Vector2 vector = new Vector2();
			Vector2 temp_pos = new Vector2();

			float num2 = 20f;
			float num3 = 2.61799383f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
			num3 = 3.66519165f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
			if (GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
			{
				num3 = (float)System.Math.PI;
				vector.x = Mathf.Sin(num3) * num2;
				vector.y = (0f - Mathf.Cos(num3)) * num2;
				temp_pos.x = pos.x + vector.x;
				temp_pos.y = pos.y + vector.y;
				UIManager.DrawSpriteUIRotated(temp_pos, 0.18f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
				temp_pos.y += 10f;
				UIManager.DrawSpriteUIRotated(temp_pos, 0.18f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 41);
			}
			else
			{
				num2 = 25f;
				num3 = (float)System.Math.PI;
				vector.x = Mathf.Sin(num3) * num2;
				vector.y = (0f - Mathf.Cos(num3)) * num2;
				temp_pos.x = pos.x + vector.x;
				temp_pos.y = pos.y + vector.y;
				UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 42);
			}
			num2 = 30f;
			num3 = (float)System.Math.PI / 2f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 38);
			num3 = 4.712389f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 38);
		}

		public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
		{
			bool bigParticles = false;

			if (m_upgrade == WeaponUnlock.LEVEL_2A)
			{
				bigParticles = true;
				proj.m_firing_sfx = SFXCue.weapon_driller_lvl2a;
				m_damage *= 1.4f;
				m_push_force *= 1.8f;
				m_push_torque *= 1.25f;
				proj.m_stun_multiplier += 0.2f;
				proj.m_trail_renderer = FXTrailRenderer.trail_renderer_driller2;
			}

			return bigParticles;
		}
	}
}