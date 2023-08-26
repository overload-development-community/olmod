using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;


namespace GameMod
{
    public class Thunderbolt : PrimaryWeapon
    {
		private float m_thunder_sound_timer = 0f;

		private static int m_charge_loop_index = -1;
        private static float m_tb_overchargedamage_multiplier = 4f; // 4.0dps self-damage instead of stock 1.0dps)

        public Thunderbolt()
        {
            displayName = "THUNDERBOLT";
            Tag2A = "MX";
            Tag2B = "RT";
            icon_idx = (int)AtlasIndex0.WICON_THUNDERBOLT;
            UsesEnergy = true;
			projprefab = ProjPrefabExt.proj_thunderbolt;
			firingMode = FiringMode.CHARGED;
			// ExplodeSync = true; // let's see what happens, shall we
        }

        public override void Fire(float refire_multiplier)
        {
            Quaternion localRotation = ps.c_transform.localRotation;
            ps.m_thunder_power = Mathf.Min((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 1f : 1.15f, ps.m_thunder_power);
            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_right.position, localRotation, ps.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_left.position, localRotation, ps.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
			if (ship.triTB)
            {
				MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center.position, localRotation, ps.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], true, 2);
				if (MPSniperPackets.AlwaysUseEnergy())
				{
					player.UseEnergy(1f + ps.m_thunder_power * 3f);
				}
			}
			
			if (GameplayManager.IsMultiplayerActive)
            {
                ps.m_refire_time += 0.5f * refire_multiplier;
            }
            else
            {
                ps.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.45f : 0.5f) * refire_multiplier;
            }
            if (!GameplayManager.IsMultiplayer)
            {
                ps.c_rigidbody.AddForce(ps.c_forward * (UnityEngine.Random.Range(-300f, -350f) * (0.5f + ps.m_thunder_power * 1.2f) * ps.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                Vector3 vector4 = RUtility.RandomUnitVector();
                ps.c_rigidbody.AddTorque((vector4 + UnityEngine.Random.onUnitSphere * 0.2f) * (UnityEngine.Random.Range(1000f, 1500f) * (0.5f + ps.m_thunder_power * 1.2f) * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_THUNDER, 1f + ps.m_thunder_power * 2f, 1f + ps.m_thunder_power);
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy(2f + ps.m_thunder_power * 3f);
            }
            ps.m_thunder_power = 0f;
			m_thunder_sound_timer = 0f;
            StopThunderboltSelfDamageLoop();
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();
            Color color;

			float num4 = Mathf.Min(1f, GameManager.m_player_ship.m_thunder_power);
			if (num4 > 0f)
			{
				color = Color.Lerp(UIManager.m_col_ub0, UIManager.m_col_hi6, num4);
				color.a = m_alpha;
				UIManager.DrawRingArc(pos, 0f, 20f, 2.5f, 0f, (int)(num4 * 32f), color, color, 4);
			}
			float num2 = 40f;
			color = Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_hi6, Mathf.Clamp01(num4 * 4f));
			float num3 = (float)System.Math.PI / 3f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			num3 = 5.23598766f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			color = Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_hi6, Mathf.Clamp01(num4 * 4f - 1f));
			num3 = 1.30899692f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			num3 = 4.97418833f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			color = Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_hi6, Mathf.Clamp01(num4 * 4f - 2f));
			num3 = (float)System.Math.PI / 2f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			num3 = 4.712389f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			color = Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_hi6, Mathf.Clamp01(num4 * 4f - 3f));
			num3 = (float)System.Math.PI * 7f / 12f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			num3 = 4.45058966f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 41);
			color = ((!(num4 < 1f)) ? Color.Lerp(UIManager.m_col_hi6, UIManager.m_col_hi7, UnityEngine.Random.Range(0f, num4 - 1f) * UIElement.FLICKER) : Color.Lerp(UIManager.m_col_ui4, UIManager.m_col_hi6, Mathf.Clamp01(num4)));
			float a = m_alpha * ((!(GameManager.m_player_ship.m_refire_time <= 0f)) ? 0.2f : 1f);
			num2 = 45f;
			num3 = (float)System.Math.PI * 2f / 3f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, a, 39);
			num3 = 4.18879032f;
			vector.x = Mathf.Sin(num3) * num2;
			vector.y = (0f - Mathf.Cos(num3)) * num2;
			temp_pos.x = pos.x + vector.x;
			temp_pos.y = pos.y + vector.y;
			UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, color, a, 39);
		}

		
		// PROJECTILE STUFF

		public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
		{
			FXWeaponExplosion explosion = FXWeaponExplosion.none;

			if (m_death_particle_override != 0)
			{
				explosion = m_death_particle_override;
			}
			else if (proj.m_death_particle_default != 0)
			{
				explosion = proj.m_death_particle_default;
			}
			if (explosion != FXWeaponExplosion.none)
			{
				ParticleElement particleElement = ParticleManager.psm[3].StartParticle((int)explosion, proj.c_transform.localPosition, proj.c_transform.localRotation, null, proj);
				particleElement.SetParticleScaleAndSimSpeed(1f + strength * 0.3f, 1f - strength * 0.15f);
			}
		}

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
			float multiplier;
			if (GameplayManager.IsMultiplayerActive)
			{
				//proj.c_transform.localScale = Vector3.one * (1.2f + m_strength * 0.3f);
				multiplier = 1.2f + m_strength * 0.4f; // larger than the base game by a little
			}
			else
			{
				//proj.c_transform.localScale = Vector3.one * (1.1f + m_strength * 0.6f);
				multiplier = 1.1f + m_strength * 0.6f;
			}
			proj.c_transform.localScale = Vector3.one * multiplier; // make the visual bigger for a charged shot...
			((CapsuleCollider)proj.c_collider).radius = 0.48f / multiplier; // ... but NOT for the player collider. No resize. Stock is resized. Leads to angry mobs and pitchforks. Appease the masses.
			//Debug.Log("CCF TB firing on " + (GameplayManager.IsDedicatedServer() ? "server" : "client" + ", strength " + m_strength + ", scale " + multiplier + ", hit radius " + ((CapsuleCollider)proj.c_collider).radius));


			m_damage *= 1f + m_strength * ((!GameplayManager.IsMultiplayerActive) ? 2.5f : 1.75f);
			m_push_force *= 1f + m_strength * 0.5f;
			m_push_torque *= 1f + m_strength * 0.25f;
			m_init_speed *= 1f + m_strength * ((!GameplayManager.IsMultiplayerActive) ? 0.3f : 0.1f);
			proj.m_trail_post_lifetime = 1f + m_strength * 2f;
			if (m_upgrade == WeaponUnlock.LEVEL_2A)
			{
				m_init_speed += 8f;
				m_damage *= 1.25f;
			}
			else if (m_upgrade == WeaponUnlock.LEVEL_2B)
			{
				proj.m_homing_acquire_speed = 20f;
				proj.m_homing_strength = 0.2f;
				proj.m_homing_max_dist = 50f;
				proj.m_homing_min_dot = 0.75f;
			}
			if (!save_pos) // moved from ThunderFire() in SFXCueManager since it only ever happens here
			{
				if (proj.m_owner_player.isLocalPlayer)
				{
					GameManager.m_audio.PlayCue2D(380, 0.4f + m_strength * 0.4f, -0.15f + m_strength * 0.3f);
					GameManager.m_audio.PlayCue2D(380, 0.45f + m_strength * 0.4f, -0.25f + m_strength * 0.1f);
					GameManager.m_audio.PlayCue2D(378, 0.4f, -0.5f);
				}
				else
				{
					GameManager.m_audio.PlayCuePos(380, pos, 0.4f + m_strength * 0.4f, -0.15f + m_strength * 0.3f);
					GameManager.m_audio.PlayCuePos(380, pos, 0.45f + m_strength * 0.4f, -0.25f + m_strength * 0.1f);
					GameManager.m_audio.PlayCuePos(378, pos, 0.4f, -0.5f);
				}
			}

			return false;
		}

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
			proj.Explode(layer == 11 || layer == 16);
		}

        public override void WeaponCharge()
		{
            if (!(ps.m_refire_time <= 0f))
			{
				return;
			}
            else if (ps.m_thunder_power == 0f)
            {
                StopThunderboltSelfDamageLoop();
            }

			float num = ((!GameplayManager.IsMultiplayerActive) ? RUtility.FRAMETIME_GAME : RUtility.FRAMETIME_FIXED);
			if (num == 0f)
			{
				return;
			}
			if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2A)
			{
				ps.m_thunder_power += num / 1.75f * ((!player.m_overdrive) ? 1f : 2f);
			}
			else
			{
				ps.m_thunder_power += num / 2f * ((!player.m_overdrive) ? 1f : 2f);
			}
			float num2 = Mathf.Min((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 1f : 1.15f, ps.m_thunder_power);
			if (ps.m_thunder_power > 2f && NetworkManager.IsServer())
			{
				DamageInfo di = default(DamageInfo);
				di.type = DamageType.ENERGY;
				di.owner = ps.c_go;
				di.push_dir = Vector3.zero;
				di.pos = ps.c_transform_position;
                //di.damage = ((!(ps.m_thunder_power > 3f)) ? (ps.m_thunder_power - 2f) : 1f) * num;
                di.damage = ((!(ps.m_thunder_power > 3f)) ? (ps.m_thunder_power - 2f) : 1f) * num * (GameplayManager.IsMultiplayer ? m_tb_overchargedamage_multiplier : 1f) * (ship.triTB ? 1.5f : 1f);
                di.stun_multiplier = 0f;
				if (!player.m_invulnerable)
				{
					ps.ApplyDamage(di);
				}
			}
			if (!ps.m_light_tb_enabled)
			{
				ps.m_light_tb_enabled = true;
				ps.c_lights[4].enabled = ps.m_light_tb_enabled;
			}
			ps.c_lights[4].intensity = Mathf.Min(ps.m_thunder_power, 1.25f) * UnityEngine.Random.Range(1.5f, 1.8f);
			player.PlayCameraShake(CameraShakeType.CHARGE_THUNDER, 0.1f + 0.5f * num2 * num * 100f, 0.8f);
			if (!GameplayManager.IsMultiplayer)
			{
				ps.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (1f * num2 / num));
			}
			m_thunder_sound_timer -= num;
			if (m_thunder_sound_timer <= 0f)
			{
				if (ps.isLocalPlayer)
				{
					GameManager.m_audio.PlayCue2D(381, 0.35f + num2 * UnityEngine.Random.Range(0.4f, 0.5f), -0.25f + num2 * 0.3f, 0f, reverb: true);

                    if (ps.m_thunder_power >= 2f && m_charge_loop_index == -1)
                    {
                        m_charge_loop_index = GameManager.m_audio.PlayCue2DLoop((int)SoundEffect.cine_sfx_warning_loop, 1f, 0f, 0f, true);
                    }
                }
				else
				{
					GameManager.m_audio.PlayCuePos(381, ps.c_transform_position, 0.35f + num2 * UnityEngine.Random.Range(0.4f, 0.5f), -0.25f + num2 * 0.3f);
				}
				m_thunder_sound_timer = 0.22f - num2 * 0.08f;
			}
        }

        public static void StopThunderboltSelfDamageLoop()
        {
            if (m_charge_loop_index != -1)
            {
                GameManager.m_audio.StopSound(m_charge_loop_index);
                m_charge_loop_index = -1;
            }
        }
    }


	// Some necessary hooks. This might actually need to be generalized if any other charged weapons are being considered.

    [HarmonyPatch(typeof(PlayerShip), "OnDestroy")]
    class MPWeaponBehavior_Thunderbolt_PlayerShip_OnDestroy
    {
        static void Postfix()
        {
            Thunderbolt.StopThunderboltSelfDamageLoop();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "OnDisable")]
    class MPWeaponBehavior_Thunderbolt_PlayerShip_OnDisable
    {
        static void Postfix()
        {
            Thunderbolt.StopThunderboltSelfDamageLoop();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class MPWeaponBehavior_Thunderbolt_PlayerShip_Update
    {
        static void Postfix(PlayerShip __instance)
        {
            if ((__instance.m_boosting || __instance.m_dead || __instance.m_dying) && GameplayManager.IsMultiplayerActive && __instance.isLocalPlayer)
            {
                Thunderbolt.StopThunderboltSelfDamageLoop();
            }
        }
    }
}