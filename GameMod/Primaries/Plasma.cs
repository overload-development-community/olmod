using HarmonyLib;
using Overload;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class Plasma : PrimaryWeapon
    {
        public Plasma()
        {
            displayName = "PLASMA";
            Tag2A = "RF";
            Tag2B = "L";
            icon_idx = (int)AtlasIndex0.WICON_REFLEX;
            UsesEnergy = true;
            projprefab = ProjPrefabExt.proj_plasma;
            itemID = ItemPrefab.entity_item_reflex;
        }

        public override void Fire(float refire_multiplier)
        {
            player.c_player_ship.FiringVolumeModifier = 0.75f;
            ParticleElement shot1 = MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_right.position, player.c_player_ship.c_transform.localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            ParticleElement shot2 = MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, player.c_player_ship.m_muzzle_left.position, player.c_player_ship.c_transform.localRotation, 0f, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            
            //player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.18f : 0.15f) * refire_multiplier;
            player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.16f : 0.14f) * refire_multiplier;
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy(0.85f);
            }
            player.PlayCameraShake(CameraShakeType.FIRE_REFLEX, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 vector = new Vector2();
            Vector2 temp_pos = new Vector2();

            /*
            float num2 = 65f;
            float num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            float a = m_alpha;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            temp_pos.x = pos.x + vector.x * 1.45f;
            temp_pos.y = pos.y + vector.y * 1.45f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            temp_pos.x = pos.x + vector.x * 1.45f;
            temp_pos.y = pos.y + vector.y * 1.45f;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, a, 37);
            */

            float num2 = 18f;
            float num3 = 0.7854f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.22f, 0.22f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ub0, m_alpha, 38);
            num3 = 2.3562f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.22f, 0.22f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ub0, m_alpha, 38);
            num3 = 3.927f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.22f, 0.22f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ub0, m_alpha, 38);
            num3 = 5.4978f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.22f, 0.22f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ub0, m_alpha, 38);

            num2 = 65f;
            num3 = (float)System.Math.PI * 2f / 3f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;

            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 40);
            num3 = 4.18879032f;
            vector.x = Mathf.Sin(num3) * num2;
            vector.y = (0f - Mathf.Cos(num3)) * num2;
            temp_pos.x = pos.x + vector.x;
            temp_pos.y = pos.y + vector.y;
            UIManager.DrawSpriteUIRotated(temp_pos, 0.3f, 0.3f, num3 + (float)System.Math.PI / 2f, UIManager.m_col_ui4, m_alpha, 40);
        }

        // THIS IS ESSENTIALLY A PROJ_DATA DEFINITION

        public override GameObject GenerateProjPrefab()
        {
            GameObject go = GameObject.Instantiate(ProjectileManager.proj_prefabs[(int)ProjPrefab.proj_enemy_core]);
            Object.DontDestroyOnLoad(go);
            projectile = go.GetComponent<Projectile>();

            projectile.m_type = (ProjPrefab)projprefab;

            projectile.m_damage_robot = 14;
            projectile.m_damage_player = 8.8f;
            projectile.m_damage_mp = 8.8f;
            projectile.m_damage_energy = true;
            projectile.m_stun_multiplier = 1;
            projectile.m_push_force_robot = 1.2f;
            projectile.m_push_force_player = 1.2f;
            projectile.m_push_torque_robot = 0.1f;
            projectile.m_push_torque_player = 0.15f;
            projectile.m_lifetime_min = 5;
            projectile.m_lifetime_max = -1;
            projectile.m_lifetime_robot_multiplier = 1;
            //projectile.m_init_speed_min = 28;
            projectile.m_init_speed_min = 30;
            //projectile.m_init_speed_min = 31;
            projectile.m_init_speed_max = -1;
            projectile.m_init_speed_robot_multiplier = 1;
            projectile.m_acceleration = 0;
            projectile.m_vel_inherit_player = 0;
            projectile.m_vel_inherit_robot = 0;
            projectile.m_track_direction = false;
            projectile.m_homing_strength = 0;
            projectile.m_homing_strength_robot = 0;
            projectile.m_homing_max_dist = 20;
            projectile.m_homing_min_dot = 0.8f;
            projectile.m_homing_acquire_speed = 20;
            projectile.m_bounce_behavior = BounceBehavior.none;
            projectile.m_bounce_max_count = 1;
            projectile.m_spawn_proj_count = 0;
            projectile.m_spawn_proj_type = ProjPrefab.none;
            projectile.m_spawn_proj_pattern = ProjSpawnPattern.RANDOM;
            projectile.m_spawn_proj_angle = 0;
            //projectile.m_death_particle_default = FXWeaponExplosion.gun_sparks_enemy1;
            //projectile.m_death_particle_damage = FXWeaponExplosion.gun_sparks_enemy2;
            projectile.m_death_particle_default = FXWeaponExplosion.gun_sparks_reflex;
            projectile.m_death_particle_damage = FXWeaponExplosion.gun_sparks_reflex_dmg;
            projectile.m_death_particle_robot = FXWeaponExplosion.none;
            projectile.m_firing_sfx = SFXCue.none;
            projectile.m_death_sfx = SFXCue.impact_nova_orb;
            //projectile.m_trail_particle = FXWeaponEffect.trail_enemy1;
            //projectile.m_trail_particle = FXWeaponEffect.trail_reflex;
            projectile.m_trail_particle = FXWeaponEffect.none;
            projectile.m_trail_renderer = FXTrailRenderer.none;
            projectile.m_trail_post_lifetime = 0.4f;
            projectile.m_muzzle_flash_particle = FXWeaponEffect.muzzle_flash_reflex;
            projectile.m_muzzle_flash_particle_player = FXWeaponEffect.muzzle_flash_reflex_player;

            var physmat = projectile.c_collider.sharedMaterial;
            Object.Destroy(projectile.c_collider);
            var coll = go.AddComponent<SphereCollider>();
            coll.material = physmat;
            coll.radius = 0.52f; //0.45? 0.55?
            projectile.c_collider = coll;
            go.layer = 12;

            projectile.c_rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous; // WHY WAS IT DISCRETE

            Light light = go.GetComponent<Light>();
            light.color = new Color(0f, 1f, 0.11f, 1f);

            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
            {
                var m = ps.main;
                //Debug.Log("CCF ps name = " + ps.name + " scale " + m.startSizeMultiplier);
                m.startSizeMultiplier = m.startSizeMultiplier * 2f;

                if (ps.name == "_glow")
                {
                    m.startColor = new Color(0f, 1f, 0.11f, 1f);
                }
                else if (ps.name == "_glow3")
                {
                    m.startColor = new Color(0.196f, 0.855f, 0.353f, 0.322f);
                }
                else if (ps.name == "_glow2")
                {
                    var sc = m.startColor;
                    sc.colorMin = new Color(0f, 1f, 0.11f, 0.75f);
                    sc.colorMax = new Color(0.196f, 0.855f, 0.353f, 1f);
                    m.startColor = sc;
                }
            }

            return go;
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (!GameplayManager.IsDedicatedServer() && proj.m_owner_player != null && !save_pos)
            {
                if (proj.m_owner_player.isLocalPlayer)
                {
                    GameManager.m_audio.PlayCue2D((int)NewSounds.PlasmaFire3, vol: 0.4f);
                }
                else
                {
                    GameManager.m_audio.PlayCuePos((int)NewSounds.PlasmaFire3, pos, vol: 0.4f);
                }
            }

            return false;
        }
    }
}