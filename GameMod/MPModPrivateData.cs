﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    public static class ExtMatchMode
    {
        public const MatchMode ANARCHY = MatchMode.ANARCHY;
        public const MatchMode TEAM_ANARCHY = MatchMode.TEAM_ANARCHY;
        public const MatchMode MONSTERBALL = MatchMode.MONSTERBALL;
        public const MatchMode CTF = (MatchMode)3;
        public const MatchMode RACE = (MatchMode)4;
        //public const MatchMode ARENA = (MatchMode)5;
        //public const MatchMode TEAM_ARENA = (MatchMode)6;
        public const MatchMode NUM = (MatchMode)((int)RACE + 1);

        private static readonly string[] Names = new string[] {
            "ANARCHY", "TEAM ANARCHY", "MONSTERBALL", "CTF", "RACE" };
        public static string ToString(MatchMode mode)
        {
            if ((int)mode < 0 || (int)mode >= Names.Length)
                return "UNEXPECTED MODE: " + (int)mode;
            return Names[(int)mode];
        }
    }

    static class MPModPrivateData
    {
        /// <summary>
        /// Projdata file based on balance sessions for olmod 0.4.0.
        /// </summary>
        public const string DEFAULT_PROJ_DATA = @"OVERLOAD_DATA
27
proj_impulse
m_damage_robot;13
m_damage_player;6.5
m_damage_mp;7
m_damage_energy;True
m_stun_multiplier;0.8
m_push_force_robot;17
m_push_force_player;2
m_push_torque_robot;1.5
m_push_torque_player;0.4
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;35
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.95
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks1
m_death_particle_damage;gun_sparks1_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_impulse
m_death_sfx;impact_energy
m_trail_particle;trail_impulse
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_impulse
m_muzzle_flash_particle_player;muzzle_flash_impulse_player
END_ENTRY
proj_vortex
m_spinup_starting_time;4
m_damage_robot;7
m_damage_player;4
m_damage_mp;2.8875
m_damage_energy;True
m_stun_multiplier;1.1
m_push_force_robot;5
m_push_force_player;0.7
m_push_torque_robot;2
m_push_torque_player;0.25
m_lifetime_min;0.3
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;60
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.65
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_vortex
m_death_particle_damage;gun_sparks_vortex_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_cyclone
m_death_sfx;impact_energy
m_trail_particle;trail_cyclone
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_cyclone
m_muzzle_flash_particle_player;none
END_ENTRY
proj_driller
m_damage_robot;18
m_damage_player;7
m_damage_mp;6.45
m_damage_energy;False
m_stun_multiplier;0.9
m_push_force_robot;6
m_push_force_player;0.8
m_push_torque_robot;7
m_push_torque_player;1
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;300
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.5
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_driller
m_death_particle_damage;gun_sparks_driller_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_driller
m_death_sfx;impact_projectile
m_trail_particle;trail_driller
m_trail_renderer;trail_renderer_driller
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_driller
m_muzzle_flash_particle_player;muzzle_flash_driller_player
END_ENTRY
proj_shotgun
m_damage_robot;7
m_damage_player;5
m_damage_mp;1.85
m_damage_energy;False
m_stun_multiplier;0.6
m_push_force_robot;7
m_push_force_player;2
m_push_torque_robot;2.5
m_push_torque_player;0.5
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;130
m_init_speed_max;150
m_init_speed_robot_multiplier;0.5
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_crusher
m_death_particle_damage;gun_sparks_crusher_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_shotgun
m_death_sfx;none
m_trail_particle;none
m_trail_renderer;trail_renderer_driller
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_driller_mini
m_muzzle_flash_particle_player;muzzle_flash_driller_mini_player
END_ENTRY
proj_driller_mini
m_damage_robot;14
m_damage_player;4.5
m_damage_mp;4.5
m_damage_energy;False
m_stun_multiplier;0.95
m_push_force_robot;4
m_push_force_player;0.3
m_push_torque_robot;5
m_push_torque_player;0.5
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;300
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.5
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_driller_mini
m_death_particle_damage;gun_sparks_driller_mini_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_driller_lvl2b
m_death_sfx;impact_projectile
m_trail_particle;trail_driller_mini
m_trail_renderer;trail_renderer_flak
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_driller_mini
m_muzzle_flash_particle_player;muzzle_flash_driller_mini_player
END_ENTRY
proj_flak_cannon
m_damage_robot;5
m_damage_player;2
m_damage_mp;2.8
m_damage_energy;False
m_stun_multiplier;0.7
m_push_force_robot;1.2
m_push_force_player;0.3
m_push_torque_robot;2.5
m_push_torque_player;0.2
m_lifetime_min;0.1
m_lifetime_max;0.11
m_lifetime_robot_multiplier;1.2
m_init_speed_min;120
m_init_speed_max;130
m_init_speed_robot_multiplier;0.6
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_flak
m_death_particle_damage;gun_sparks_flak_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_flak
m_death_sfx;impact_projectile
m_trail_particle;trail_enemy2
m_trail_renderer;trail_renderer_flak
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_flak
m_muzzle_flash_particle_player;muzzle_flash_flak_player
END_ENTRY
proj_reflex
m_damage_robot;13
m_damage_player;6
m_damage_mp;6.325
m_damage_energy;True
m_stun_multiplier;0.5
m_push_force_robot;1.5
m_push_force_player;0.5
m_push_torque_robot;0.5
m_push_torque_player;0.25
m_lifetime_min;1.5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;27
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.85
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;True
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;BOUNCE_ALL
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_reflex
m_death_particle_damage;gun_sparks_reflex_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_reflex
m_death_sfx;impact_energy
m_trail_particle;trail_reflex
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_reflex
m_muzzle_flash_particle_player;muzzle_flash_reflex_player
END_ENTRY
proj_thunderbolt
m_damage_robot;35
m_damage_player;16
m_damage_mp;15
m_damage_energy;True
m_stun_multiplier;2
m_push_force_robot;25
m_push_force_player;0.8
m_push_torque_robot;8
m_push_torque_player;0.3
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;30
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.9
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;True
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_thunder
m_death_particle_damage;gun_sparks_thunder_thru
m_death_particle_robot;none
m_firing_sfx;none
m_death_sfx;impact_energy
m_trail_particle;trail_thunderbolt
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_thunderbolt
m_muzzle_flash_particle_player;muzzle_flash_thunderbolt_player
END_ENTRY
proj_flare
m_damage_robot;1
m_damage_player;1
m_damage_mp;1
m_damage_energy;False
m_stun_multiplier;1
m_push_force_robot;1
m_push_force_player;1
m_push_torque_robot;0
m_push_torque_player;0
m_lifetime_min;10
m_lifetime_max;11
m_lifetime_robot_multiplier;1
m_init_speed_min;15
m_init_speed_max;15.5
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0.5
m_vel_inherit_robot;0.5
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_flare
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;weapon_flare
m_death_sfx;none
m_trail_particle;trail_flare
m_trail_renderer;none
m_trail_post_lifetime;0.75
m_muzzle_flash_particle;none
m_muzzle_flash_particle_player;none
END_ENTRY
proj_flare_sticky
m_damage_robot;1
m_damage_player;1
m_damage_mp;1
m_damage_energy;False
m_stun_multiplier;1
m_push_force_robot;1
m_push_force_player;1
m_push_torque_robot;0
m_push_torque_player;0
m_lifetime_min;7200
m_lifetime_max;7201
m_lifetime_robot_multiplier;1
m_init_speed_min;11
m_init_speed_max;11.5
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0.5
m_vel_inherit_robot;0.5
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_flare
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;weapon_flare
m_death_sfx;none
m_trail_particle;trail_flare_sticky
m_trail_renderer;none
m_trail_post_lifetime;0.75
m_muzzle_flash_particle;none
m_muzzle_flash_particle_player;none
END_ENTRY
proj_enemy_core
m_damage_robot;11
m_damage_player;11
m_damage_mp;11
m_damage_energy;True
m_stun_multiplier;1
m_push_force_robot;1.2
m_push_force_player;1.2
m_push_torque_robot;0.1
m_push_torque_player;0.15
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;15
m_init_speed_max;17
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0.8
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_enemy1
m_death_particle_damage;gun_sparks_enemy2
m_death_particle_robot;none
m_firing_sfx;weapon_enemy1
m_death_sfx;impact_energy
m_trail_particle;trail_enemy1
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_enemy1
m_muzzle_flash_particle_player;none
END_ENTRY
proj_enemy_vulcan
m_damage_robot;8
m_damage_player;5
m_damage_mp;5
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;0.6
m_push_force_player;0.6
m_push_torque_robot;0.1
m_push_torque_player;0.1
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;45
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_driller_mini
m_death_particle_damage;gun_sparks_driller_mini_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_driller_lvl2a
m_death_sfx;impact_projectile
m_trail_particle;trail_enemy2
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_vulcan
m_muzzle_flash_particle_player;none
END_ENTRY
missile_creeper
m_damage_robot;12
m_damage_player;8
m_damage_mp;5
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;3
m_push_force_player;1.5
m_push_torque_robot;3
m_push_torque_player;1
m_lifetime_min;4
m_lifetime_max;4.5
m_lifetime_robot_multiplier;1
m_init_speed_min;13
m_init_speed_max;16
m_init_speed_robot_multiplier;1.25
m_acceleration;0.1
m_vel_inherit_player;0.5
m_vel_inherit_robot;0.25
m_track_direction;False
m_homing_strength;9
m_homing_strength_robot;8
m_homing_max_dist;15
m_homing_min_dot;-1
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_creeper
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;mssile_creeper
m_death_sfx;exp_creeper
m_trail_particle;trail_creeper
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_creeper
m_muzzle_flash_particle_player;none
END_ENTRY
missile_devastator
m_damage_robot;40
m_damage_player;20
m_damage_mp;30
m_damage_energy;False
m_stun_multiplier;1
m_push_force_robot;40
m_push_force_player;3
m_push_torque_robot;20
m_push_torque_player;3
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;25
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;15
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_ultra
m_death_particle_damage;missile_explosion_ultra_strong
m_death_particle_robot;none
m_firing_sfx;mssile_devastator
m_death_sfx;exp_devastator
m_trail_particle;trail_detonator_main
m_trail_renderer;trail_renderer_dev
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_devastator
m_muzzle_flash_particle_player;none
END_ENTRY
missile_devastator_mini
m_damage_robot;5
m_damage_player;3
m_damage_mp;3
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;1
m_push_force_player;0.1
m_push_torque_robot;5
m_push_torque_player;1
m_lifetime_min;0.2
m_lifetime_max;0.35
m_lifetime_robot_multiplier;1
m_init_speed_min;10
m_init_speed_max;18
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;15
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_ultra_mini
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;none
m_death_sfx;none
m_trail_particle;trail_detonator_mini
m_trail_renderer;trail_renderer_dev_mini
m_trail_post_lifetime;1
m_muzzle_flash_particle;none
m_muzzle_flash_particle_player;none
END_ENTRY
missile_falcon
m_damage_robot;20
m_damage_player;8
m_damage_mp;10
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;30
m_push_force_player;3.5
m_push_torque_robot;30
m_push_torque_player;2.2
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;35
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.85
m_acceleration;5
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;60
m_homing_min_dot;0.8
m_homing_acquire_speed;5
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_falcon
m_death_particle_damage;missile_explosion_falcon_weak
m_death_particle_robot;none
m_firing_sfx;mssile_falcon
m_death_sfx;exp_falcon
m_trail_particle;trail_falcon
m_trail_renderer;trail_renderer_falcon
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_falcon
m_muzzle_flash_particle_player;none
END_ENTRY
missile_hunter
m_damage_robot;10
m_damage_player;5
m_damage_mp;2.1
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;10
m_push_force_player;3
m_push_torque_robot;15
m_push_torque_player;1.8
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;17.5
m_init_speed_max;-1
m_init_speed_robot_multiplier;1.1
m_acceleration;2
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0.665
m_homing_strength_robot;0.4
m_homing_max_dist;60
m_homing_min_dot;0.7
m_homing_acquire_speed;10
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_hunter
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;mssile_hunter
m_death_sfx;exp_hunter
m_trail_particle;trail_hunter
m_trail_renderer;trail_renderer_hunter
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_hunter
m_muzzle_flash_particle_player;none
END_ENTRY
missile_pod
m_damage_robot;10
m_damage_player;5
m_damage_mp;3
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;10
m_push_force_player;2
m_push_torque_robot;4
m_push_torque_player;1.5
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;25
m_init_speed_max;30
m_init_speed_robot_multiplier;0.95
m_acceleration;5
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0.3
m_homing_strength_robot;0.25
m_homing_max_dist;40
m_homing_min_dot;0.8
m_homing_acquire_speed;8
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_pod
m_death_particle_damage;missile_explosion_pod_strong
m_death_particle_robot;none
m_firing_sfx;mssile_pod
m_death_sfx;exp_missile_pod
m_trail_particle;trail_missile_pod
m_trail_renderer;trail_renderer_missile_pod
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_missile_pod
m_muzzle_flash_particle_player;none
END_ENTRY
missile_smart
m_damage_robot;25
m_damage_player;15
m_damage_mp;10
m_damage_energy;False
m_stun_multiplier;1
m_push_force_robot;25
m_push_force_player;2
m_push_torque_robot;15
m_push_torque_player;2.5
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;20
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;2
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0.7
m_homing_acquire_speed;5
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_nova
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;mssile_nova
m_death_sfx;exp_nova
m_trail_particle;trail_nova_main
m_trail_renderer;trail_renderer_nova
m_trail_post_lifetime;1
m_muzzle_flash_particle;none
m_muzzle_flash_particle_player;none
END_ENTRY
missile_smart_mini
m_damage_robot;15
m_damage_player;8
m_damage_mp;8
m_damage_energy;True
m_stun_multiplier;1
m_push_force_robot;0.2
m_push_force_player;0.2
m_push_torque_robot;1.5
m_push_torque_player;0.5
m_lifetime_min;3.5
m_lifetime_max;3.6
m_lifetime_robot_multiplier;1
m_init_speed_min;12
m_init_speed_max;15
m_init_speed_robot_multiplier;1.1
m_acceleration;1
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0.3
m_homing_strength_robot;0.25
m_homing_max_dist;20
m_homing_min_dot;0.65
m_homing_acquire_speed;15
m_bounce_behavior;BOUNCE_ALL
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_nova_mini
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;none
m_death_sfx;none
m_trail_particle;trail_nova_mini
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_reflex
m_muzzle_flash_particle_player;none
END_ENTRY
missile_timebomb
m_damage_robot;25
m_damage_player;20
m_damage_mp;25
m_damage_energy;False
m_stun_multiplier;2
m_push_force_robot;2
m_push_force_player;2
m_push_torque_robot;2
m_push_torque_player;2
m_lifetime_min;1.5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;15
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0.2
m_vel_inherit_robot;0.2
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;15
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_timebomb
m_death_particle_damage;missile_explosion_timebomb_strong
m_death_particle_robot;none
m_firing_sfx;mssile_timebomb
m_death_sfx;exp_timebomb
m_trail_particle;trail_timebomb
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;none
m_muzzle_flash_particle_player;none
END_ENTRY
proj_enemy_blaster
m_damage_robot;8
m_damage_player;9
m_damage_mp;9
m_damage_energy;True
m_stun_multiplier;1
m_push_force_robot;1
m_push_force_player;1
m_push_torque_robot;0.1
m_push_torque_player;0.1
m_lifetime_min;2.3
m_lifetime_max;2.4
m_lifetime_robot_multiplier;1
m_init_speed_min;23
m_init_speed_max;25
m_init_speed_robot_multiplier;0.5
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0.85
m_homing_acquire_speed;8
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_enemy_core
m_death_particle_damage;gun_sparks_enemy_core_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_enemy2
m_death_sfx;impact_energy
m_trail_particle;none
m_trail_renderer;trail_blaster1
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_enemy_core
m_muzzle_flash_particle_player;none
END_ENTRY
proj_beam
m_damage_robot;25
m_damage_player;7
m_damage_mp;11.5
m_damage_energy;True
m_stun_multiplier;1.25
m_push_force_robot;20
m_push_force_player;0.8
m_push_torque_robot;5
m_push_torque_player;0.7
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;200
m_init_speed_max;-1
m_init_speed_robot_multiplier;0.5
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_beam
m_death_particle_damage;gun_sparks_beam_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_lancer
m_death_sfx;impact_energy
m_trail_particle;trail_beam
m_trail_renderer;trail_renderer_lancer
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_lancer
m_muzzle_flash_particle_player;muzzle_flash_lancer_player
END_ENTRY
missile_vortex
m_damage_robot;25
m_damage_player;10
m_damage_mp;12
m_damage_energy;False
m_stun_multiplier;1.25
m_push_force_robot;5
m_push_force_player;2
m_push_torque_robot;5
m_push_torque_player;1.5
m_lifetime_min;0.5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;50
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;0
m_homing_min_dot;-1
m_homing_acquire_speed;10
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_singularity
m_death_particle_damage;missile_explosion_singularity_long
m_death_particle_robot;none
m_firing_sfx;mssile_vortex2
m_death_sfx;exp_vortex
m_trail_particle;trail_vortex
m_trail_renderer;trail_renderer_vortex
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_alien_vortex
m_muzzle_flash_particle_player;none
END_ENTRY
missile_alien_pod
m_damage_robot;10
m_damage_player;6
m_damage_mp;6
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;10
m_push_force_player;2
m_push_torque_robot;4
m_push_torque_player;1.5
m_lifetime_min;5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;25
m_init_speed_max;30
m_init_speed_robot_multiplier;0.95
m_acceleration;5
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0.3
m_homing_strength_robot;0.25
m_homing_max_dist;40
m_homing_min_dot;0.8
m_homing_acquire_speed;8
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;missile_explosion_alien_pod
m_death_particle_damage;none
m_death_particle_robot;none
m_firing_sfx;missile_alien_pod
m_death_sfx;exp_missile_pod
m_trail_particle;trail_thunderbolt
m_trail_renderer;trail_renderer_lancer
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_alien_pod
m_muzzle_flash_particle_player;none
END_ENTRY
proj_alien_vulcan
m_damage_robot;8
m_damage_player;5
m_damage_mp;5
m_damage_energy;False
m_stun_multiplier;0.8
m_push_force_robot;0.6
m_push_force_player;0.6
m_push_torque_robot;0.1
m_push_torque_player;0.1
m_lifetime_min;1.5
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;60
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;3
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_beam
m_death_particle_damage;gun_sparks_beam_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_alien_vulcan
m_death_sfx;impact_energy
m_trail_particle;trail_alien_medium
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_alien_medium
m_muzzle_flash_particle_player;none
END_ENTRY
proj_alien_blaster
m_damage_robot;7
m_damage_player;5
m_damage_mp;5
m_damage_energy;True
m_stun_multiplier;0.9
m_push_force_robot;5
m_push_force_player;0.7
m_push_torque_robot;0.75
m_push_torque_player;0.25
m_lifetime_min;0.75
m_lifetime_max;-1
m_lifetime_robot_multiplier;1
m_init_speed_min;30
m_init_speed_max;-1
m_init_speed_robot_multiplier;1
m_acceleration;0
m_vel_inherit_player;0
m_vel_inherit_robot;0
m_track_direction;False
m_homing_strength;0
m_homing_strength_robot;0
m_homing_max_dist;20
m_homing_min_dot;0
m_homing_acquire_speed;20
m_bounce_behavior;none
m_bounce_max_count;1
m_spawn_proj_count;0
m_spawn_proj_type;none
m_spawn_proj_pattern;RANDOM
m_spawn_proj_angle;0
m_death_particle_default;gun_sparks_alien_medium
m_death_particle_damage;gun_sparks_alien_medium_dmg
m_death_particle_robot;none
m_firing_sfx;weapon_enemy_vulcan
m_death_sfx;impact_energy
m_trail_particle;trail_thunderbolt
m_trail_renderer;none
m_trail_post_lifetime;1
m_muzzle_flash_particle;muzzle_flash_alien_medium
m_muzzle_flash_particle_player;none
END_ENTRY
";

        public static int TeamCount
        {
            get { return MPTeams.NetworkMatchTeamCount; }
            set { MPTeams.NetworkMatchTeamCount = value; }
        }
        public static bool RearViewEnabled
        {
            get { return RearView.MPNetworkMatchEnabled; }
            set { RearView.MPNetworkMatchEnabled = value; }
        }
        public static bool JIPEnabled
        {
            get { return MPJoinInProgress.NetworkMatchEnabled; }
            set { MPJoinInProgress.NetworkMatchEnabled = value; }
        }
        public static bool SniperPacketsEnabled
        {
            get { return MPSniperPackets.enabled; }
            set { MPSniperPackets.enabled = value; }
        }
        public static MatchMode MatchMode
        {
            get { return NetworkMatch.GetMode(); }
            set { NetworkMatch.SetMode(value); }
        }
        public static bool SuddenDeathEnabled
        {
            get { return MPSuddenDeath.SuddenDeathMatchEnabled; }
            set { MPSuddenDeath.SuddenDeathMatchEnabled = value; }
        }
        public static int LapLimit;
        public static string MatchNotes { get; set; }
        public static bool HasPassword { get; set; }
        public static bool ScaleRespawnTime { get; set; }
        public static int ModifierFilterMask;
        public static bool ClassicSpawnsEnabled
        {
            get { return MPClassic.matchEnabled; }
            set { MPClassic.matchEnabled = value; }
        }
        public static bool CtfCarrierBoostEnabled
        {
            get { return CTF.CarrierBoostEnabled; }
            set { CTF.CarrierBoostEnabled = value; }
        }
        public static bool AlwaysCloaked
        {
            get { return MPAlwaysCloaked.Enabled; }
            set { MPAlwaysCloaked.Enabled = value; }
        }
        public static string CustomProjdata { get; set; }
        public static bool AllowSmash
        {
            get { return MPSmash.Enabled; }
            set { MPSmash.Enabled = value; }
        }

        public static int MatchTimeLimit
        {
            get { return MPMatchTimeLimits.MatchTimeLimit; }
            set { MPMatchTimeLimits.MatchTimeLimit = value; }
        }
        public static bool AssistScoring { get; set; } = true;

        public static bool JoystickRotationFixSupported
        {
            get { return true; }
            set { JoystickRotationFix.server_support = value; }
        }

        public static int ShipMeshCollider
        {
            get { return MPColliderSwap.selectedCollider; }
            set { MPColliderSwap.selectedCollider = value; }
        }

        public static bool ThunderboltPassthrough
        {
            get { return MPThunderboltPassthrough.isAllowed; }
            set { MPThunderboltPassthrough.isAllowed = value; }
        }

        public static bool DamageNumbers
        {
            get { return MPObserver.DamageNumbersEnabled; }
            set { MPObserver.DamageNumbersEnabled = value; }
        }

        public static bool AudioTauntsSupported
        {
            get { return true; }
            set { MPAudioTaunts.AServer.server_supports_audiotaunts = value; }
        }

        public static JObject Serialize()
        {
            JObject jobject = new JObject();
            jobject["teamcount"] = TeamCount;
            jobject["rearviewenabled"] = RearViewEnabled;
            jobject["jipenabled"] = JIPEnabled;
            jobject["sniperpacketsenabled"] = SniperPacketsEnabled;
            jobject["matchmode"] = (int)MatchMode;
            jobject["suddendeathenabled"] = SuddenDeathEnabled;
            jobject["laplimit"] = LapLimit;
            jobject["matchnotes"] = MatchNotes;
            jobject["haspassword"] = HasPassword;
            jobject["scalerespawntime"] = ScaleRespawnTime;
            jobject["modifierfiltermask"] = ModifierFilterMask;
            jobject["classicspawnsenabled"] = ClassicSpawnsEnabled;
            jobject["ctfcarrierboostenabled"] = CtfCarrierBoostEnabled;
            jobject["alwayscloaked"] = AlwaysCloaked;
            jobject["allowsmash"] = AllowSmash;
            jobject["customprojdata"] = CustomProjdata;
            jobject["matchtimelimit"] = MatchTimeLimit;
            jobject["assistscoring"] = AssistScoring;
            jobject["joystickrotationfixsupported"] = JoystickRotationFixSupported;
            jobject["shipmeshcollider"] = ShipMeshCollider;
            jobject["thunderboltpassthrough"] = ThunderboltPassthrough;
            jobject["damagenumbers"] = DamageNumbers;
            jobject["audiotauntsupport"] = AudioTauntsSupported;
            return jobject;
        }

        public static void Deserialize(JToken root)
        {
            TeamCount = root["teamcount"].GetInt(MPTeams.Min);
            RearViewEnabled = root["rearviewenabled"].GetBool(false);
            JIPEnabled = root["jipenabled"].GetBool(false);
            SniperPacketsEnabled = root["sniperpacketsenabled"].GetBool(false);
            MatchMode = (MatchMode)root["matchmode"].GetInt(0);
            SuddenDeathEnabled = root["suddendeathenabled"].GetBool(false);
            LapLimit = root["laplimit"].GetInt(0);
            MatchNotes = root["matchnotes"].GetString(String.Empty);
            HasPassword = root["haspassword"].GetBool(false);
            ScaleRespawnTime = root["scalerespawntime"].GetBool(false);
            ModifierFilterMask = root["modifierfiltermask"].GetInt(255);
            ClassicSpawnsEnabled = root["classicspawnsenabled"].GetBool(false);
            CtfCarrierBoostEnabled = root["ctfcarrierboostenabled"].GetBool(false);
            AlwaysCloaked = root["alwayscloaked"].GetBool(false);
            AllowSmash = root["allowsmash"].GetBool(false);
            CustomProjdata = root["customprojdata"].GetString(string.Empty);
            MatchTimeLimit = root["matchtimelimit"].GetInt(-1);

            // If client sent new match time limit, apply otherwise ignore (e.g. old olmod client)
            if (MatchTimeLimit >= 0)
                NetworkMatch.m_match_time_limit_seconds = MatchTimeLimit;
            AssistScoring = root["assistscoring"].GetBool(true);
            JoystickRotationFixSupported = root["joystickrotationfixsupported"].GetBool(false);
            ShipMeshCollider = root["shipmeshcollider"].GetInt(0);
            ThunderboltPassthrough = root["thunderboltpassthrough"].GetBool(false);
            DamageNumbers = root["damagenumbers"].GetBool(false);
            AudioTauntsSupported = root["audiotauntsupport"].GetBool(false);
        }

        public static string GetModeString(MatchMode mode)
        {
            return Loc.LS(ExtMatchMode.ToString(mode));
        }

    }

    public class MPModPrivateDataTransfer
    {
        public static void SendTo(int connId)
        {
            var mmpdMsg = new StringMessage(MPModPrivateData.Serialize().ToString(Newtonsoft.Json.Formatting.None));
            NetworkServer.SendToClient(connId, MessageTypes.MsgModPrivateData, mmpdMsg);
        }
        public static void OnReceived(string data)
        {
            Debug.LogFormat("MPModPrivateData: received {0}", data);
            MPModPrivateData.Deserialize(JToken.Parse(data));
        }
    }

    /*
    public class MPModPrivateDataMessage : MessageBase
    {
        public int TeamCount { get; set; }
        public bool RearViewEnabled { get; set; }
        public bool JIPEnabled { get; set; }
        public MatchMode MatchMode { get; set; }
        public int LapLimit { get; set; }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.WritePackedUInt32((uint)TeamCount);
            writer.Write(RearViewEnabled);
            writer.Write(JIPEnabled);
            writer.WritePackedUInt32((uint)MatchMode);
            writer.WritePackedUInt32((uint)LapLimit);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            TeamCount = (int)reader.ReadPackedUInt32();
            RearViewEnabled = reader.ReadBoolean();
            JIPEnabled = reader.ReadBoolean();
            MatchMode = (MatchMode)reader.ReadPackedUInt32();
            LapLimit = (int)reader.ReadPackedUInt32();
        }
    }
    */

    [HarmonyPatch(typeof(MenuManager), "GetMMSGameMode")]
    class MPModPrivateData_MenuManager_GetMMSGameMode
    {
        static bool Prefix(ref string __result)
        {
            __result = MPModPrivateData.GetModeString(MenuManager.mms_mode);

            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPModPrivateData_UIElement_DrawMpMatchSetup
    {
        static void Postfix(UIElement __instance)
        {
            if (MenuManager.m_menu_micro_state == 2 && MenuManager.mms_mode == ExtMatchMode.RACE)
            {
                Vector2 position = Vector2.zero;
                position.y = -279f + 62f * 6;
                var text = ExtMenuManager.mms_ext_lap_limit == 0 ? "NONE" : ExtMenuManager.mms_ext_lap_limit.ToString();
                __instance.SelectAndDrawStringOptionItem("LAP LIMIT", position, 10, text, string.Empty, 1.5f, false);
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPModPrivateData_MenuManager_MpMatchSetup
    {
        static void MatchModeSlider()
        {
            MenuManager.mms_mode = (MatchMode)(((int)MenuManager.mms_mode + (int)ExtMatchMode.NUM + UIManager.m_select_dir) % (int)ExtMatchMode.NUM);
            return;
        }

        static void HandleLapLimit()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                MenuManager.m_menu_micro_state == 2 &&
                UIManager.m_menu_selection == 10)
            {
                //ExtMenuManager.mms_ext_lap_limit = (ExtMenuManager.mms_ext_lap_limit + 21 + UIManager.m_select_dir) % 21;
                ExtMenuManager.mms_ext_lap_limit = Math.Max(0, Math.Min(50, ExtMenuManager.mms_ext_lap_limit + UIManager.m_select_dir * 5));
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool remove = false;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "MaybeReverseOption")
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPModPrivateData_MenuManager_MpMatchSetup), "HandleLapLimit"));
                    continue;
                }

                if (code.opcode == OpCodes.Ldsfld && (code.operand as FieldInfo).Name == "mms_mode")
                {
                    remove = true;
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MPModPrivateData_MenuManager_MpMatchSetup), "MatchModeSlider");
                    yield return code;
                }

                if (code.opcode == OpCodes.Stsfld && (code.operand as FieldInfo).Name == "mms_mode")
                {
                    remove = false;
                    continue;
                }

                if (remove)
                    continue;

                yield return code;
            }
        }
    }

    public class ExtMenuManager
    {
        public static int mms_ext_lap_limit = 10;
    }

    [HarmonyPatch(typeof(NetworkMatch), "GetModeString")]
    class MPModPrivateData_NetworkMatch_GetModeString
    {
        // there's a mode argument but in actual usage this is always NetworkMatch.GetMode()
        // so ignore it here, since the default value MatchMode.NUM means CTF now :(
        static bool Prefix(MatchMode mode, ref string __result)
        {
            __result = MPModPrivateData.GetModeString(NetworkMatch.GetMode());
            return false;
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPModPrivateData_Client_RegisterHandlers
    {
        private static void OnModPrivateData(NetworkMessage msg)
        {
            /*
            MPModPrivateDataMessage mpdm = msg.ReadMessage<MPModPrivateDataMessage>();
            MPModPrivateData.MatchMode = mpdm.MatchMode;
            MPModPrivateData.JIPEnabled = mpdm.JIPEnabled;
            MPModPrivateData.RearViewEnabled = mpdm.RearViewEnabled;
            MPModPrivateData.TeamCount = mpdm.TeamCount;
            MPModPrivateData.LapLimit = mpdm.LapLimit;
            */
            MPModPrivateDataTransfer.OnReceived(msg.ReadMessage<StringMessage>().value);
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MessageTypes.MsgModPrivateData, OnModPrivateData);
        }
    }

    /*
    [HarmonyPatch(typeof(Server), "SendAcceptedToLobby")]
    class MPModPrivateData_Server_SendAcceptedToLobby
    {
        static void Postfix(NetworkConnection conn)
        {
            var server = NetworkMatch.m_client_server_location;
            if (!server.StartsWith("OLMOD ") || server == "OLMOD 0.2.8" || server.StartsWith("OLMOD 0.2.8."))
                return;
            /-*
            var msg = new MPModPrivateDataMessage
            {
                JIPEnabled = MPModPrivateData.JIPEnabled,
                MatchMode = MPModPrivateData.MatchMode,
                RearViewEnabled = MPModPrivateData.RearViewEnabled,
                TeamCount = MPModPrivateData.TeamCount,
                LapLimit = MPModPrivateData.LapLimit
            };
            *-/
            var msg = new StringMessage(MPModPrivateData.Serialize().ToString(Newtonsoft.Json.Formatting.None));

            NetworkServer.SendToClient(conn.connectionId, ModCustomMsg.MsgModPrivateData, msg);
        }
    }
    */

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemOnGameSessionStart")]
    class NetworkMatch_NetSystemOnGameSessionStart
    {
        public static string ModNetSystemOnGameSessionStart(Dictionary<string, object> attributes)
        {
            return attributes.ContainsKey("mod_private_data") ? (string)attributes["mod_private_data"] : "";
        }

        public static void ModNetSystemOnGameSessionStart2(string mpd)
        {
            if (mpd != "")
                MPModPrivateDataTransfer.OnReceived(mpd);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            LocalBuilder a = ilGen.DeclareLocal(typeof(string));
            var codes = instructions.ToList();
            int startIdx = -1;
            int startIdx2 = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "private_match_data")
                {
                    startIdx = i - 3;
                }

                //if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "Unpacked private match data for: {0}")
                if (codes[i].opcode == OpCodes.Ldsfld && ((MemberInfo)codes[i].operand).Name == "m_max_players_for_match" && startIdx2 == -1)
                {
                    startIdx2 = i;
                }
            }

            // insert backwards to preserve indexes

            if (startIdx2 > -1 && startIdx > -1)
            {
                var labels = codes[startIdx2].labels;
                codes[startIdx2].labels = new List<Label>();
                codes.InsertRange(startIdx2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, a) { labels = labels },
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NetworkMatch_NetSystemOnGameSessionStart), "ModNetSystemOnGameSessionStart2"))
                });
            }

            if (startIdx > -1)
            {
                List<CodeInstruction> newCodes = new List<CodeInstruction>();
                for (int i = startIdx; i < startIdx + 3; i++) // copy loads for attributes dict
                {
                    newCodes.Add(new CodeInstruction(codes[i].opcode, codes[i].operand));
                }
                newCodes.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NetworkMatch_NetSystemOnGameSessionStart), "ModNetSystemOnGameSessionStart")));
                newCodes.Add(new CodeInstruction(OpCodes.Stloc_S, a));
                codes.InsertRange(startIdx, newCodes);
            }


            return codes;
        }

    }

    [HarmonyPatch(typeof(NetworkMatch), "StartMatchMakerRequest")]
    class MPModPrivateData_NetworkMatch_StartMatchMakerRequest
    {

        public static void PatchModPrivateData(MatchmakerPlayerRequest matchmakerPlayerRequest)
        {
            if (!MenuManager.m_mp_lan_match) // LAN includes internet match
                return;
            MPModPrivateData.MatchMode = MenuManager.mms_mode;
            MPModPrivateData.RearViewEnabled = RearView.MPMenuManagerEnabled;
            MPModPrivateData.JIPEnabled = MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable;
            MPModPrivateData.TeamCount = MPTeams.MenuManagerTeamCount;
            MPModPrivateData.LapLimit = ExtMenuManager.mms_ext_lap_limit;
            MPModPrivateData.MatchNotes = MPServerBrowser.mms_match_notes;
            MPModPrivateData.SniperPacketsEnabled = true;
            MPModPrivateData.ScaleRespawnTime = Menus.mms_scale_respawn_time;
            MPModPrivateData.ModifierFilterMask = RUtility.BoolArrayToBitmask(MPModifiers.mms_modifier_filter);
            MPModPrivateData.ClassicSpawnsEnabled = Menus.mms_classic_spawns;
            MPModPrivateData.CtfCarrierBoostEnabled = Menus.mms_ctf_boost;
            MPModPrivateData.AlwaysCloaked = Menus.mms_always_cloaked;
            MPModPrivateData.AllowSmash = Menus.mms_allow_smash;
            MPModPrivateData.DamageNumbers = Menus.mms_damage_numbers;
            MPModPrivateData.MatchTimeLimit = Menus.mms_match_time_limit == 0 ? int.MaxValue : Menus.mms_match_time_limit;
            MPModPrivateData.AssistScoring = Menus.mms_assist_scoring;
            MPModPrivateData.ShipMeshCollider = Menus.mms_collision_mesh;
            MPModPrivateData.ThunderboltPassthrough = MPThunderboltPassthrough.isAllowed;
            if (Menus.mms_mp_projdata_fn == "STOCK") {
                MPModPrivateData.CustomProjdata = string.Empty;
            } else {
                try
                {
                    MPModPrivateData.CustomProjdata = System.IO.File.ReadAllText(Menus.mms_mp_projdata_fn);
                    
                }
                catch (Exception)
                {
                    Debug.Log("Unable to read custom projdata file: " + Menus.mms_mp_projdata_fn);
                    MPModPrivateData.CustomProjdata = String.Empty;
                }
            }

            var mpd = (PrivateMatchDataMessage)AccessTools.Field(typeof(NetworkMatch), "m_private_data").GetValue(null);
            MPModPrivateData.HasPassword = mpd.m_password.Contains('_');
            matchmakerPlayerRequest.PlayerAttributes["mod_private_data"] = MPModPrivateData.Serialize().ToString(Newtonsoft.Json.Formatting.None);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int i = 0;
            CodeInstruction last = null;
            CodeInstruction mmprAttributes = null;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "players")
                    mmprAttributes = last;

                if (mmprAttributes == null)
                    last = code;

                if (mmprAttributes != null && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(NetworkMatch), "m_private_data"))
                {
                    i++;

                    if (i == 3)
                    {
                        CodeInstruction ci1 = new CodeInstruction(OpCodes.Ldloc_S, mmprAttributes.operand);
                        CodeInstruction ci2 = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPModPrivateData_NetworkMatch_StartMatchMakerRequest), "PatchModPrivateData"));
                        yield return ci1;
                        yield return ci2;
                    }
                }

                yield return code;
            }
        }
    }
}
