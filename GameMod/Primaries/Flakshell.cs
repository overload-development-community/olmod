using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Flakshell : PrimaryWeapon
    {
        public Flakshell()
        {
            displayName = "FLAKSHELL";
            Tag2A = "DX";
            Tag2B = "VK";
            icon_idx = (int)AtlasIndex0.WICON_FLAK;
            UsesAmmo = true;
            projprefab = ProjPrefabExt.proj_flakshell;
            //ImpactForce = true;
        }

        public override void Fire(float refire_multiplier)
        {
            Vector3 c_right = ship.c_right;
            Vector3 c_up = ship.c_up;
            //Vector3 c_forward = ship.c_forward;

            ps.FiringVolumeModifier = 0.7f;

            /*
            if (ship.flak_fire_count < 4)
            {
                ship.flak_fire_count++;
            }
            */

            if (ps.isLocalPlayer)
            {
                GameManager.m_audio.PlayCue2D(337, 0.6f, 0.5f, 0f, true);
                GameManager.m_audio.PlayCue2D(338, 0.6f, 0.5f, 0f, true);
            }
            else
            {
                GameManager.m_audio.PlayCuePos(337, ps.c_transform.position, 0.6f, 0.5f, 0f);
                GameManager.m_audio.PlayCuePos(338, ps.c_transform.position, 0.6f, 0.5f, 0f);
            }

            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, (ps.m_alternating_fire ? ps.m_muzzle_right.position : ps.m_muzzle_left.position), player.c_player_ship.c_transform_rotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, (ps.m_alternating_fire ? 0 : 1));

            player.c_player_ship.m_refire_time += 0.22f; //+ (float)Mathf.Max(0, 4 - ship.flak_fire_count) * 0.07f;
            if (!GameplayManager.IsMultiplayer)
            {
                player.c_player_ship.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(20f, 30f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                player.c_player_ship.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(2f, 3f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.9f, 1.1f);
            ship.flak_fire_count++;
            player.UseAmmo(2);
            return;
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();
            Color color;

            float w;
            float num2;
            if (GameManager.m_player_ship.FlakInRange)
            {
                color = UIManager.m_col_hi7;
                w = 0.4f;
                num2 = 103f;
            }
            else
            {
                color = UIManager.m_col_ui3;
                w = 0.3f;
                num2 = 105f;
            }
            float num3 = (float)System.Math.PI / 4f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = (float)System.Math.PI * 3f / 4f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = 3.926991f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = 5.49778748f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = 4.712389f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
            num3 = (float)System.Math.PI / 2f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, w, 0.3f, num3 + (float)System.Math.PI / 2f, color, m_alpha, 37);
        }

        /*
        public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            Debug.Log("CCF flak damaged something = " + damaged_something);
            base.Explode(proj, damaged_something, m_death_particle_override, strength, m_upgrade);
            //base.Explode(proj, true, m_death_particle_override, strength, m_upgrade);

            //GameManager.m_light_manager.CreateLightFlash(proj.c_transform.position, Color.white, 10f, 25f, 0.2f, false);
            //SFXCueManager.PlayCuePos(SFXCue.exp_creeper, proj.c_transform.position, 0.5f);
            //GameManager.m_audio.PlayCuePos((int)SoundEffect.player_hit5, proj.c_transform.position, 0.8f);
        }
        */

        // THIS IS ESSENTIALLY A PROJ_DATA DEFINITION

        public override GameObject GenerateProjPrefab()
        {
            GameObject go = GameObject.Instantiate(ProjectileManager.proj_prefabs[(int)ProjPrefab.proj_enemy_vulcan]);
            Object.DontDestroyOnLoad(go);
            projectile = go.GetComponent<Projectile>();

            projectile.m_type = (ProjPrefab)projprefab;

            projectile.m_damage_robot = 15f;
            projectile.m_damage_player = 6f;
            projectile.m_damage_mp = 6f;
            projectile.m_damage_energy = false;
            projectile.m_stun_multiplier = 1f;
            projectile.m_push_force_robot = 1f;
            projectile.m_push_force_player = 1f;
            projectile.m_push_torque_robot = 1f;
            projectile.m_lifetime_min = 0.23f;
            projectile.m_lifetime_max = -1;
            projectile.m_lifetime_robot_multiplier = 1;
            projectile.m_init_speed_min = 60f;
            projectile.m_init_speed_max = -1;
            projectile.m_init_speed_robot_multiplier = 1;
            projectile.m_acceleration = 0;
            projectile.m_vel_inherit_player = 0;
            projectile.m_vel_inherit_robot = 0;
            projectile.m_track_direction = false;
            projectile.m_homing_strength = 0;
            projectile.m_homing_strength_robot = 0;
            projectile.m_homing_max_dist = 20;
            projectile.m_homing_min_dot = 0;
            projectile.m_homing_acquire_speed = 20;
            projectile.m_bounce_behavior = BounceBehavior.none;
            projectile.m_bounce_max_count = 3;
            projectile.m_spawn_proj_count = 0;
            projectile.m_spawn_proj_type = ProjPrefab.none;
            projectile.m_spawn_proj_pattern = ProjSpawnPattern.RANDOM;
            projectile.m_spawn_proj_angle = 0;
            //projectile.m_death_particle_default = FXWeaponExplosion.gun_sparks_driller_mini;
            //projectile.m_death_particle_damage = FXWeaponExplosion.gun_sparks_driller_mini_dmg;
            projectile.m_death_particle_robot = FXWeaponExplosion.none;
            projectile.m_firing_sfx = SFXCue.weapon_driller_lvl2a;
            //projectile.m_death_sfx = SFXCue.impact_projectile;
            projectile.m_death_sfx = SFXCue.exp_creeper;
            projectile.m_trail_particle = FXWeaponEffect.trail_enemy2;
            projectile.m_trail_renderer = FXTrailRenderer.none;
            projectile.m_trail_post_lifetime = 1;
            projectile.m_muzzle_flash_particle = FXWeaponEffect.muzzle_flash_vulcan;
            projectile.m_muzzle_flash_particle_player = FXWeaponEffect.muzzle_flash_vulcan;

            go.layer = 12;

            return go;
        }


        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            bool bigParticles = false; ;
            
            if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                m_lifetime *= 1.5f;
                m_damage *= 1.2f;
            }
            else if (m_upgrade == WeaponUnlock.LEVEL_2A)
            {
                bigParticles = true;
                m_damage *= 1.6f;
                m_push_torque *= 1.2f;
                proj.m_stun_multiplier *= 1.2f;
            }

            return bigParticles;
        }


        public override void AddWeaponExplosions(ref List<GameObject> ex)
        {
            GameObject go = GameObject.Instantiate(ParticleManager.psm[3].particle_prefabs[(int)FXWeaponExplosion.missile_explosion_creeper]);
            Object.DontDestroyOnLoad(go);

            GameObject sparks1 = go.transform.GetChild(0).gameObject;
            var ps1main = sparks1.GetComponent<ParticleSystem>().main;
            var ps1speed = ps1main.startSpeed;
            ps1speed.constantMin = 9;
            ps1speed.constantMax = 10;
            ps1main.startSpeed = ps1speed;
            ps1main.simulationSpeed = 2f;

            Renderer rend1 = sparks1.GetComponent<Renderer>();
            Material mat1 = rend1.material;
            mat1.SetFloat("_CoreStrength", 105f);
            mat1.SetFloat("_Exponent", 9f);
            rend1.material = mat1;

            GameObject sparks2 = go.transform.GetChild(2).gameObject;
            var ps2main = sparks2.GetComponent<ParticleSystem>().main;
            var ps2speed = ps2main.startSpeed;
            ps2speed.constantMin = 8;
            ps2speed.constantMax = 10;
            ps2main.startSpeed = ps2speed;
            ps2main.simulationSpeed = 2f;

            Renderer rend2 = sparks2.GetComponent<Renderer>();
            Material mat2 = rend2.material;
            mat2.SetFloat("_CoreStrength", 105f);
            mat2.SetFloat("_Exponent", 9f);
            rend2.material = mat1;

            GameObject ring1 = go.transform.GetChild(1).gameObject;
            var ps3main = ring1.GetComponent<ParticleSystem>().main;
            var ps3speed = ps3main.startSpeed;
            ps3speed.constantMin = 6;
            ps3speed.constantMax = 7;
            ps3main.startSpeed = ps3speed;
            ps3main.simulationSpeed = 2f;

            GameObject ring2 = go.transform.GetChild(3).gameObject;
            var ps4main = ring2.GetComponent<ParticleSystem>().main;
            var ps4speed = ps4main.startSpeed;
            ps4speed.constantMin = 8;
            ps4speed.constantMax = 9;
            ps4main.startSpeed = ps4speed;
            ps4main.simulationSpeed = 2f;

            var ps5main = go.GetComponent<ParticleSystem>().main; // smoke is on the main GameObject
            var ps5size = ps4main.startSize;
            ps5size.constantMin = 6.5f;
            ps5size.constantMax = 8;
            ps5main.startSize = ps5size;
            ps5main.simulationSpeed = 2f;


            projectile.m_death_particle_default = (FXWeaponExplosion)ex.Count;
            projectile.m_death_particle_robot = (FXWeaponExplosion)ex.Count;

            // data

            Explosion explosion = go.GetComponent<Explosion>();

            explosion.m_exp_force = 1f;
            explosion.m_exp_radius = 4f;
            explosion.m_damage_radius = 5.5f;
            explosion.m_damage_radius_player = 5f;
            explosion.m_damage_radius_mp = 5.5f;
            explosion.m_player_damage = 18f;
            explosion.m_robot_damage = 20f;
            explosion.m_mp_damage = 18f;
            explosion.m_camera_shake_type = CameraShakeType.EXPLODE_SMALL;
            explosion.m_camera_shake_intensity = 0.3f;

            ex.Add(go);

            // A second reduced damage explosion for a direct shell hit
            projectile.m_death_particle_damage = (FXWeaponExplosion)ex.Count;

            GameObject goDirect = GameObject.Instantiate(go);
            Object.DontDestroyOnLoad(goDirect);

            explosion = goDirect.GetComponent<Explosion>();

            explosion.m_exp_force = 1f;
            explosion.m_exp_radius = 3f;
            explosion.m_damage_radius = 3.5f;
            explosion.m_damage_radius_player = 3.5f;
            explosion.m_damage_radius_mp = 3.5f;
            explosion.m_player_damage = 8f;
            explosion.m_robot_damage = 12f;
            explosion.m_mp_damage = 8f;
            explosion.m_camera_shake_type = CameraShakeType.EXPLODE_SMALL;
            explosion.m_camera_shake_intensity = 0.5f;

            ex.Add(goDirect);
        }
    }
}