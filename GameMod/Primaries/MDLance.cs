using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class MDLance : PrimaryWeapon
    {
        public MDLance()
        {
            displayName = "MD-LANCE";
            Tag2A = "C";
            Tag2B = "SF";
            icon_idx = (int)AtlasIndex0.WICON_LANCER;
            UsesEnergy = true;
            AllowedCharging = false;
            projprefab = ProjPrefabExt.proj_mdlance;
            itemID = ItemPrefab.entity_item_lancer;
            firingMode = FiringMode.SEMI_AUTO;
            ImpactForce = true;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            // don't allow firing if empty -- this thing is too powerful for that
            if (player.m_energy <= 0f)
            {
                return;
            }

            player.c_player_ship.FiringVolumeModifier = 0.95f;
            Quaternion localRotation = AngleRandomize(player.c_player_ship.c_transform.localRotation, (player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0f : 0.2f, c_up, c_right);
            if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
            {
                if (player.c_player_ship.m_alternating_fire)
                {
                    MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 0);
                }
                else
                {
                    MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
                }
                //player.c_player_ship.m_alternating_fire = !player.c_player_ship.m_alternating_fire;
                player.c_player_ship.m_refire_time += 2f + (0.23f * refire_multiplier);
                if (MPSniperPackets.AlwaysUseEnergy())
                {
                    player.UseEnergy(20f);
                }
                player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 3f, 1.5f);
                return;
            }
            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);

            if (GameplayManager.IsMultiplayerActive)
            {
                if (player.m_overdrive)
                {
                    player.c_player_ship.m_refire_time += 1.8f;
                }
                else
                {
                    player.c_player_ship.m_refire_time += 2.3f + (0.23f * refire_multiplier);
                }
            }
            else if (player.m_overdrive)
            {
                player.c_player_ship.m_refire_time += 2f + ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.28f : 0.2f);
            }
            else
            {
                player.c_player_ship.m_refire_time += 2f + ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.2f : 0.1f);
            }
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                if (GameplayManager.IsMultiplayerActive)
                {
                    player.UseEnergy(20f);
                }
                else
                {
                    player.UseEnergy((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 20f : 15f);
                }
            }
            player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 4f, 2f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            float num2;
            float num3;

            /*
            num2 = 18f;
            num3 = 2.0944f;
            //num3 = 1.5708f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            //UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 38);
            num3 = 4.1888f;
            //num3 = 4.7124f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            //UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 38);
            */

            num2 = 70f;
            num3 = 2.0944f;
            //num3 = 1.5708f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            float a = m_alpha * ((!(GameManager.m_player_ship.m_refire_time <= 0f)) ? 0.2f : 1f);
            float a2 = a * ((GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] != WeaponUnlock.LEVEL_2B || !GameManager.m_player_ship.m_alternating_fire) ? 1f : 0.15f);
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a2, 39);
            num3 = 4.1888f;
            //num3 = 4.7124f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            a2 = a * ((GameManager.m_local_player.m_weapon_level[(int)GameManager.m_local_player.m_weapon_type] != WeaponUnlock.LEVEL_2B || GameManager.m_player_ship.m_alternating_fire) ? 1f : 0.15f);
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a2, 39);

            /*
            // crosshair

            num2 = 22f;
            num3 = 1.5708f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            num3 = 4.7124f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            num2 = 9f;
            num3 = 0f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 42);
            num2 = 27f;
            num3 = (float)System.Math.PI;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 42);
            */

            /*
            // X sight

            num2 = 18f;
            num3 = 0.7854f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 38);
            num3 = 2.3562f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 38);
            num3 = 3.927f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 38);
            num3 = 5.4978f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 38);
            */

            // X-crosshair hybrid

            num2 = 22f;
            num3 = 1.5708f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            num3 = 4.7124f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, m_alpha, 43);
            num3 = 1.0472f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, a2, 41);
            num3 = 2.0944f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, a2, 41);
            num3 = 4.1888f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, a2, 41);
            num3 = 5.236f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y + 9f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui0, a2, 41);
        }

        public override GameObject GenerateProjPrefab()
        {
            GameObject go = GameObject.Instantiate(ProjectileManager.proj_prefabs[(int)ProjPrefab.proj_beam]);
            Object.DontDestroyOnLoad(go);
            projectile = go.GetComponent<Projectile>();

            projectile.m_type = (ProjPrefab)projprefab;

            projectile.m_damage_robot = 40;
            projectile.m_damage_player = 40;
            projectile.m_damage_mp = 40;
            projectile.m_damage_energy = true;
            projectile.m_stun_multiplier = 1.25f;
            projectile.m_push_force_robot = 10;
            projectile.m_push_force_player = 10;
            projectile.m_push_torque_robot = 5;
            projectile.m_push_torque_player = 10;
            projectile.m_lifetime_min = 5;
            projectile.m_lifetime_max = -1;
            projectile.m_lifetime_robot_multiplier = 1;
            projectile.m_init_speed_min = 180;
            projectile.m_init_speed_max = -1;
            projectile.m_init_speed_robot_multiplier = 0.5f;
            projectile.m_acceleration = 0;
            projectile.m_vel_inherit_player = 0;
            projectile.m_vel_inherit_robot = 0;
            projectile.m_track_direction = true;
            projectile.m_homing_strength = 0;
            projectile.m_homing_strength_robot = 0;
            projectile.m_homing_max_dist = 20;
            projectile.m_homing_min_dot = 0;
            projectile.m_homing_acquire_speed = 20;
            projectile.m_bounce_behavior = BounceBehavior.none;
            projectile.m_bounce_max_count = 1;
            projectile.m_spawn_proj_count = 0;
            projectile.m_spawn_proj_type = ProjPrefab.none;
            projectile.m_spawn_proj_pattern = ProjSpawnPattern.RANDOM;
            projectile.m_spawn_proj_angle = 0;
            projectile.m_death_particle_default = FXWeaponExplosion.gun_sparks_beam;
            projectile.m_death_particle_damage = FXWeaponExplosion.gun_sparks_beam_dmg;
            projectile.m_death_particle_robot = FXWeaponExplosion.none;
            projectile.m_firing_sfx = SFXCue.weapon_alien_vulcan;
            projectile.m_death_sfx = SFXCue.impact_energy;
            //projectile.m_trail_particle = FXWeaponEffect.trail_beam;
            projectile.m_trail_renderer = FXTrailRenderer.trail_renderer_lancer;
            projectile.m_trail_post_lifetime = 20;
            projectile.m_muzzle_flash_particle = FXWeaponEffect.muzzle_flash_lancer;
            projectile.m_muzzle_flash_particle_player = FXWeaponEffect.muzzle_flash_lancer_player;

            Light light = go.GetComponent<Light>();
            light.color = new Color(1f, 0f, 0f, 1f);

            Material mat = go.GetComponentInChildren<MeshRenderer>().material;
            mat.SetColor("_TintColor", new Color(0.863f, 0.157f, 0.314f, 1f));

            return go;
        }

        public override void AddTrailRenderers(ref List<GameObject> tr)
        {
            
        }

        public override void AddWeaponEffects(ref List<GameObject> fx)
        {
            GameObject go = GameObject.Instantiate(ParticleManager.psm[2].particle_prefabs[(int)FXWeaponEffect.trail_beam]);
            Object.DontDestroyOnLoad(go);

            // Trail FX
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
            {
                var m = ps.main;
                var col = ps.colorOverLifetime;
                m.startSizeMultiplier = m.startSizeMultiplier * 10f;

                if (ps.name == "trail_beam")
                {
                    var sc = m.startColor;
                    sc.colorMin = new Color(0.659f, 0.067f, 0f, 1f);
                    sc.colorMax = new Color(1f, 0.239f, 0.165f, 1f);
                    m.startColor = sc;

                    sc = col.color;
                    var g = sc.gradient;
                    var c = g.colorKeys;
                    c[1].color = new Color(1f, 0.824f, 0f);
                    g.colorKeys = c;
                    sc.gradient = g;
                    col.color = sc;
                }
                else if (ps.name == "glow")
                {
                    var sc = m.startColor;
                    sc.colorMin = new Color(0.62f, 0.067f, 0f, 1f);
                    sc.colorMax = new Color(0.965f, 0.208f, 0.149f, 1f);
                    m.startColor = sc;

                    sc = col.color;
                    var g = sc.gradient;
                    var c = g.colorKeys;
                    c[1].color = new Color(0.659f, 0f, 0f);
                    g.colorKeys = c;
                    sc.gradient = g;
                    col.color = sc;
                }
                else if (ps.name == "central_streaks2")
                {
                    var sc = m.startColor;
                    sc.colorMin = new Color(0.659f, 0.067f, 0f, 1f);
                    sc.colorMax = new Color(1f, 0.239f, 0.165f, 1f);
                    m.startColor = sc;

                    sc = col.color;
                    var g = sc.gradient;
                    var c = g.colorKeys;
                    c[1].color = new Color(1f, 0.255f, 0.161f);
                    g.colorKeys = c;
                    sc.gradient = g;
                    col.color = sc;
                }
            }

            projectile.m_trail_particle = (FXWeaponEffect)fx.Count;
            fx.Add(go);
        }

        public override void AddWeaponExplosions(ref List<GameObject> ex)
        {
            
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            /*if (GameplayManager.IsMultiplayerActive)
            {
                m_init_speed *= 0.6f; // Just do this manually in the projdata definition above. This *may* need to change at some point if SP stuff is integrated.
            }
            */
            if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                proj.m_firing_sfx = SFXCue.weapon_lancer_lvl2b;
            }

            if (!GameplayManager.IsDedicatedServer() && proj.m_owner_player != null && !save_pos)
            {
                int sound;
                if (proj.m_owner_player.m_overdrive)
                {
                    sound = (int)NewSounds.LancerCharge2s;
                }
                else
                {
                    sound = (int)NewSounds.LancerCharge2s5;
                }
                if (proj.m_owner_player.isLocalPlayer)
                {
                    GameManager.m_audio.PlayCue2D(sound, 1f);
                }
                else
                {
                    GameManager.m_audio.PlayCuePos(sound, pos, 1f);
                }
            }

            return false;
        }
    }
}
