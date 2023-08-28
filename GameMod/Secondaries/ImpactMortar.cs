using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    public class ImpactMortar : SecondaryWeapon
    {
        public ImpactMortar()
        {
            displayName = "IMPACT MORTAR";
            displayNamePlural = "IMPACT MORTARS";
            Tag2A = "LT";
            Tag2B = "XS";
            icon_idx = (int)AtlasIndex0.MISSILE_TIMEBOMB1;
            projprefab = ProjPrefabExt.missile_mortar;
            ammo = 8;
            ammoUp = 10;
            ammoSuper = 4;
            ImpactForce = true;
            MoveSync = true;
            ExplodeSync = true;
            firingMode = FiringMode.SEMI_AUTO;
            itemID = ItemPrefab.entity_item_devastator;
            projMeshName = "Mortar";
            extraNames = new string[] { "MortarEmissive" };
            extras = new GameObject[1];
        }

        public override void Fire(float refire_multiplier)
        {
            //Vector3 c_right = ship.c_right;
            //Vector3 c_up = ship.c_up;
            Vector3 c_forward = ship.c_forward;
            //Quaternion localRotation = ps.c_transform.localRotation;
            //Quaternion rot = AngleRandomize(ps.c_transform.localRotation, 0.1f, c_up, c_right);

            WeaponUnlock level = player.m_missile_level[(int)player.m_missile_type];

            MPSniperPackets.MaybePlayerFire(player, (ProjPrefab)projprefab, ps.m_muzzle_center2.position, ps.c_transform.localRotation, 0f, level);
            ps.m_refire_missile_time += 1.5f;
            /*
            if (!GameplayManager.IsMultiplayer)
            {
                ps.c_rigidbody.AddForce(c_forward * (UnityEngine.Random.Range(-400f, -500f) * ps.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                ps.c_rigidbody.AddTorque(c_right * (UnityEngine.Random.Range(-1500f, -1000f) * RUtility.FIXED_FT_INVERTED));
            }
            */
            ps.c_rigidbody.AddForce(c_forward * (-300f * ps.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
            player.PlayCameraShake(CameraShakeType.FIRE_TIMEBOMB, 1f, 1f);
        }

        public override void DrawHUDReticle(Vector2 pos, float m_alpha)
        {
            Vector2 temp_pos = new Vector2();

            Color c = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7, UnityEngine.Random.value * UIElement.FLICKER);
            Color c2 = UIManager.m_col_ui2;

            temp_pos.x = pos.x;
            temp_pos.y = pos.y + 48f;
            UIManager.DrawQuadUIRotated(temp_pos, 10.8f, 10.8f, (float)System.Math.PI / 4f, c2, m_alpha, 35);
            if (GameManager.m_local_player.CanFireMissileAmmo())
            {
                UIManager.DrawQuadUIRotated(temp_pos, 6f, 6f, (float)System.Math.PI / 4f, c, m_alpha, 4);
            }
        }

        public override bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                m_death_particle_override = proj.m_death_particle_damage;
                //m_damage *= 2f;
                //proj.m_homing_strength = 10f;
                //proj.m_homing_min_dot = -1f;
                //m_lifetime += 0.5f;
                m_init_speed *= 1.5f;
                proj.m_homing_max_dist = 25f;
            }
            else if (m_upgrade == WeaponUnlock.LEVEL_2B)
            {
                m_death_particle_override = proj.m_death_particle_damage;
                m_init_speed *= 1.4f;
                m_damage *= 2f;
            }
            else
            {
                m_death_particle_override = proj.m_death_particle_default;
            }

            if (!GameplayManager.IsDedicatedServer() && proj.m_owner_player != null && !save_pos)
            {
                if (proj.m_owner_player.isLocalPlayer)
                {
                    GameManager.m_audio.PlayCue2D((int)NewSounds.MortarFire3, vol: 0.8f);
                }
                else
                {
                    GameManager.m_audio.PlayCuePos((int)NewSounds.MortarFire3, pos, vol: 0.9f);
                }
            }

            return false;
        }

        public override void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            base.Explode(proj, true, m_death_particle_override, strength, m_upgrade);
            //Debug.Log("CCF Exploded at " + Time.time);

            //float num8 = ((m_upgrade != WeaponUnlock.LEVEL_2A) ? 5f : 7.5f);
            /*
            if (NetworkManager.IsServer())
            {
                float num9 = ((!GameplayManager.IsMultiplayer) ? Mathf.Min(num8 * 2f, GameplayManager.SLOW_MO_TIMER + num8) : 0.25f);
                Server.SendSlowMoTimerToAllClients(Mathf.RoundToInt(num9 * 1000000f));
            }
            */
            GameManager.m_light_manager.CreateLightFlash(proj.c_transform.position, Color.white, 10f, 25f, 0.2f, false);
            GameManager.m_audio.PlayCuePos((int)NewSounds.MortarExplode2, proj.c_transform.position); 
            //SFXCueManager.PlayRawSoundEffect2D(SoundEffect.wep_missile_timebomb_on, 1f, 0f, 0f, reverb: true);
            //SFXCueManager.PlayRawSoundEffect2D(SoundEffect.wep_missile_timebomb_on, 1f, 0f, 0f, reverb: true);
            if (m_upgrade >= WeaponUnlock.LEVEL_1)
            {
                RobotManager.StunAll(proj.c_transform.position, (m_upgrade != WeaponUnlock.LEVEL_2A) ? 2.5f : 3f, (m_upgrade != WeaponUnlock.LEVEL_2A) ? 1.5f : 2f, 1f);
            }
        }

        // THIS IS ESSENTIALLY A PROJ_DATA DEFINITION

        public override GameObject GenerateProjPrefab()
        {
            GameObject go = GameObject.Instantiate(ProjectileManager.proj_prefabs[(int)ProjPrefab.missile_devastator]);
            Object.DontDestroyOnLoad(go);
            projectile = go.GetComponent<Projectile>();

            projectile.m_type = (ProjPrefab)projprefab;

            projectile.m_damage_robot = 120f;
            projectile.m_damage_player = 90f;
            projectile.m_damage_mp = 90f;
            projectile.m_damage_energy = false;
            projectile.m_stun_multiplier = 8f;
            projectile.m_push_force_robot = 10f;
            projectile.m_push_torque_robot = 10f;
            projectile.m_push_torque_player = 10f;
            projectile.m_lifetime_min = 2f;
            projectile.m_lifetime_max = -1;
            projectile.m_lifetime_robot_multiplier = 1;
            projectile.m_init_speed_min = 14f;
            projectile.m_init_speed_max = -1;
            projectile.m_init_speed_robot_multiplier = 1;
            projectile.m_acceleration = 0;
            projectile.m_vel_inherit_player = 0.8f;
            projectile.m_vel_inherit_robot = 0.2f;
            projectile.m_homing_strength = 0;
            projectile.m_homing_strength_robot = 0;
            projectile.m_homing_max_dist = 15;
            projectile.m_homing_min_dot = 0;
            projectile.m_homing_acquire_speed = 20;
            projectile.m_bounce_behavior = BounceBehavior.BOUNCE_ALL;
            projectile.m_bounce_max_count = 5;
            projectile.m_spawn_proj_count = 0;
            projectile.m_spawn_proj_type = ProjPrefab.none;
            projectile.m_spawn_proj_pattern = ProjSpawnPattern.RANDOM;
            projectile.m_spawn_proj_angle = 0;
            //projectile.m_death_particle_damage = FXWeaponExplosion.missile_explosion_timebomb_strong;
            //projectile.m_death_particle_damage = FXWeaponExplosion.missile_explosion_ultra_strong;
            //projectile.m_death_particle_robot = FXWeaponExplosion.none;
            projectile.m_firing_sfx = SFXCue.none;
            projectile.m_death_sfx = SFXCue.none;
            projectile.m_trail_particle = FXWeaponEffect.trail_enemy1;
            projectile.m_trail_renderer = FXTrailRenderer.none;
            projectile.m_muzzle_flash_particle = FXWeaponEffect.muzzle_flash_devastator;
            projectile.m_muzzle_flash_particle_player = FXWeaponEffect.muzzle_flash_devastator;

            projectile.c_rigidbody.drag = 0.7f;

            // visual stuff

            // Child 0: _glow
            // Child 1: _level_collide
            // Child 2: _devastator_flare
            // Child 3: _light
            // Child 4: DevastatorA2

            MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
            mf.mesh = projMesh.GetComponent<MeshFilter>().sharedMesh;
            mf.transform.localPosition = Vector3.zero;
            mf.transform.localRotation = Quaternion.identity;
            mf.transform.localScale = Vector3.one * 1.3f;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            Material mat = mr.material;
            mat.SetColor("_EmissionColor", new Color(0.628f, 0.102f, 0f));
            mat.SetTexture("_EmissionMap", extras[0].GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_MainTex"));
            //Debug.Log("CCF texture name is " + mr.material.GetTexture("_EmissionMap").name);
            mr.material = mat;
            mat.EnableKeyword("_EMISSION");

            var glowps = go.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            glowps.startSize = 1.5f;

            // May need to screw with lights and adding it to the root object. We'll see.
            //Light origLight = go.GetComponentInChildren<Light>();
            //Light newLight = go.AddComponent<Light>();

            GameObject light1 = go.transform.GetChild(3).gameObject;
            light1.transform.localPosition = new Vector3(0.28f, 0f, 0f);
            light1.GetComponent<Light>().intensity = 0.6f;

            GameObject.Instantiate(light1, new Vector3(-0.28f, 0f, 0f), Quaternion.identity, go.transform);
            GameObject.Instantiate(light1, new Vector3(0f, 0.28f, 0f), Quaternion.identity, go.transform);
            GameObject.Instantiate(light1, new Vector3(0f, -0.28f, 0f), Quaternion.identity, go.transform);

            go.transform.GetChild(0).localPosition = Vector3.zero;

            GameObject levelColl = go.transform.GetChild(1).gameObject;
            CapsuleCollider oldColl = levelColl.GetComponent<CapsuleCollider>();
            SphereCollider newColl = levelColl.AddComponent<SphereCollider>(); // Predictable bounces need a sphere.
            newColl.sharedMaterial = oldColl.sharedMaterial;
            newColl.material.bounceCombine = PhysicMaterialCombine.Maximum; // Somewhere, this should be generalized if the projectile has a bounce modifier set. This'll work for now.
            newColl.material.bounciness = 0.8f;
            //newColl.radius = 0.35f;
            newColl.radius = 0.3f; // This overlaps the walls slightly but you're bouncing anyways, and you need to be able to thread a hole.
            Object.Destroy(oldColl);

            GameObject.Destroy(go.transform.GetChild(2).gameObject);
            GameObject.Destroy(go.transform.GetChild(0).GetChild(0).gameObject);

            projectile.c_rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // item prefab

            PrefabManager.item_prefabs[(int)itemID].SetActive(false);
            itemPrefab = GameObject.Instantiate(PrefabManager.item_prefabs[(int)itemID]);
            PrefabManager.item_prefabs[(int)itemID].SetActive(true); // ??? maybe?
            Object.DontDestroyOnLoad(itemPrefab);
            Item item = itemPrefab.GetComponent<Item>();

            MeshFilter[] itemMFs = itemPrefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            foreach (MeshFilter m in itemMFs)
            {
                //Debug.Log("CCF setting item mesh on " + m.gameObject.name);
                m.mesh = projMesh.GetComponent<MeshFilter>().sharedMesh;
                m.transform.localScale = Vector3.one * 1.8f;
                m.transform.localPosition = new Vector3(0f, -0.143f, 0f);
                m.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            }

            //itemPrefab.SetActive(false);
            item.m_active = false;
            item.m_amount = 1;
            item.reg_amount = 1;
            //item.m_type = ItemType.MISSILE_TIMEBOMB; // here temporarily

            //Object.DestroyImmediate(itemPrefab.GetComponent<UnityEngine.Networking.NetworkIdentity>());
            //itemPrefab.AddComponent<UnityEngine.Networking.NetworkIdentity>();

            //Debug.Log("CCF NEW PREFAB ID IS " + item.GetComponent<UnityEngine.Networking.NetworkIdentity>().assetId);
            //Debug.Log("CCF DEV PREFAB ID IS " + PrefabManager.item_prefabs[MPWeapons.MissileItems[5]].GetComponent<UnityEngine.Networking.NetworkIdentity>().assetId);

            MPWeapons.RegisterSpawnItemExtHandler(itemPrefab, displayName);

            return go;
        }

        public override void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if (proj.ShouldPlayDamageEffect(layer))
            {
                Debug.Log("CCF Collided at " + Time.time);
                proj.Explode(damaged_something: true);
            }
        }

        // EXPLOSION -- still not satisfied with this aesthetically, but it's functional.

        public override void AddWeaponExplosions(ref List<GameObject> ex)
        {
            GameObject go = GameObject.Instantiate(ParticleManager.psm[3].particle_prefabs[(int)FXWeaponExplosion.missile_explosion_ultra_strong]);
            Object.DontDestroyOnLoad(go);

            GameObject ring1 = go.transform.GetChild(10).gameObject;
            var ps1main = ring1.GetComponent<ParticleSystem>().main;
            var startsize = ps1main.startSize;
            startsize.constantMax = 10f;
            startsize.constantMin = 9f;
            ps1main.startSize = startsize;

            GameObject ring2 = GameObject.Instantiate(ring1, go.transform);
            var ps2main = ring2.GetComponent<ParticleSystem>().main;
            var sc2 = ps2main.startColor;
            //sc2.colorMin = new Color(1f, 0.585f, 0.035f, 0.706f);
            //sc2.colorMax = new Color(1f, 0.765f, 0.149f, 0.745f);
            sc2.colorMin = new Color(1f, 0.285f, 0.035f, 0.606f);
            sc2.colorMax = new Color(1f, 0.465f, 0.149f, 0.645f);
            ps2main.startColor = sc2;

            var sol2 = ring2.GetComponent<ParticleSystem>().sizeOverLifetime;
            var size2 = sol2.size;
            var curve2 = size2.curve;
            var curve2key = curve2.keys[0];
            curve2key.value = 0.5f;
            curve2.keys[0] = curve2key;
            size2.curve = curve2;
            sol2.size = size2;

            startsize = ps2main.startSize;
            startsize.constantMax = 13f;
            startsize.constantMin = 12f;
            ps2main.startSize = startsize;

            var ps2render = ring2.GetComponent<ParticleSystemRenderer>();
            ps2render.material = Assets.materials["_ring_superbright1_yellow"];
            
            GameObject ring3 = GameObject.Instantiate(ring1, go.transform);
            var ps3main = ring3.GetComponent<ParticleSystem>().main;
            var sc3 = ps3main.startColor;
            //sc3.colorMin = new Color(1f, 0.596f, 0.036f, 0.721f);
            //sc3.colorMax = new Color(1f, 0.902f, 0f, 0.759f);
            sc3.colorMin = new Color(1f, 0.296f, 0.036f, 0.721f);
            sc3.colorMax = new Color(1f, 0.402f, 0f, 0.759f);
            ps3main.startColor = sc3;

            var sol3 = ring3.GetComponent<ParticleSystem>().sizeOverLifetime;
            var size3 = sol3.size;
            var curve3 = size3.curve;
            var curve3key = curve3.keys[0];
            curve3key.value = 0.5f;
            curve3.keys[0] = curve3key;
            size3.curve = curve3;
            sol3.size = size3;

            startsize = ps3main.startSize;
            startsize.constantMax = 14f;
            startsize.constantMin = 13f;
            ps3main.startSize = startsize;

            var ps3render = ring3.GetComponent<ParticleSystemRenderer>();
            //ps3render.material = Assets.materials["_ring_superbright2"];
            ps3render.material = Assets.materials["_ring_superbright1_yellow"];
            /*
            var ps3anim = ring3.GetComponent<ParticleSystem>().textureSheetAnimation;
            ps3anim.enabled = false;
            
            Material mat = ps3render.material;
            mat.SetTexture("_MainTex", extras[0].GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_DecalTex"));
            ps3render.material = mat;
            */
            /*
            GameObject ring4 = GameObject.Instantiate(ring3, go.transform);
            startsize = ps4main.startSize;
            startsize.constantMax = 13f;
            startsize.constantMin = 12f;
            ps4main.startSize = startsize;

            var ps4render = ring4.GetComponent<ParticleSystemRenderer>();
            ps4render.material = Assets.materials["_ring_superbright2"];
            */

            GameObject.Destroy(go.transform.GetChild(9).gameObject);
            GameObject.Destroy(go.transform.GetChild(7).gameObject);
            GameObject.Destroy(go.transform.GetChild(6).gameObject);
            GameObject.Destroy(go.transform.GetChild(4).gameObject);
            GameObject.Destroy(go.transform.GetChild(3).gameObject);
            GameObject.Destroy(go.transform.GetChild(2).gameObject);
            GameObject.Destroy(go.transform.GetChild(0).gameObject);

            projectile.m_death_particle_damage = (FXWeaponExplosion)ex.Count;
            projectile.m_death_particle_robot = (FXWeaponExplosion)ex.Count;

            // data

            Explosion explosion = go.GetComponent<Explosion>();

            explosion.m_exp_force = 250f;
            explosion.m_exp_radius = 22f;
            explosion.m_damage_radius = 20f;
            explosion.m_damage_radius_player = 14f;
            explosion.m_damage_radius_mp = 18f;
            explosion.m_player_damage  = 160f;
            explosion.m_robot_damage = 400f;
            explosion.m_mp_damage= 200f;
            explosion.m_camera_shake_type = CameraShakeType.EXPLODE_LARGE;
            explosion.m_camera_shake_intensity = 3f;

            ex.Add(go);
        }
    }
}
