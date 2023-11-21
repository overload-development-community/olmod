using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;
using Tobii.Gaming;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    // ====================================================================
    // Projectile Prefabs
    // ====================================================================

    public enum ProjPrefabExt
    {
        none,
        missile_alien_pod,
        missile_creeper,
        missile_devastator,
        missile_devastator_mini,
        missile_falcon,
        missile_hunter,
        missile_pod,
        missile_smart,
        missile_smart_mini,
        missile_timebomb,
        missile_vortex,
        proj_alien_blaster,
        proj_alien_vulcan,
        proj_beam,
        proj_driller,
        proj_driller_mini,
        proj_enemy_blaster,
        proj_enemy_core,
        proj_enemy_vulcan,
        proj_flak_cannon,
        proj_flare,
        proj_flare_sticky,
        proj_impulse,
        proj_melee,
        proj_reflex,
        proj_shotgun,
        proj_thunderbolt,
        proj_vortex,
        // new projectiles start here
        proj_flakshell,
        proj_plasma,
        proj_mdlance,
        missile_mortar,
        // new projectiles end here
        num
    }

    public enum FiringMode
    {
        AUTO,
        SEMI_AUTO,
        CHARGED,
        STREAM,
        DETONATOR
    }

    public class WeaponExtHash : MonoBehaviour
    {
        public NetworkHash128 assetId;
    }

    // ====================================================================
    // MPWeapons
    // ====================================================================
    public static class MPWeapons
    {        
        // holds the main static-but-not-really initialized copy of each weapon for duplication
        public static Dictionary<string, PrimaryWeapon> MasterPrimaries = new Dictionary<string, PrimaryWeapon>();
        public static Dictionary<string, SecondaryWeapon> MasterSecondaries = new Dictionary<string, SecondaryWeapon>();

        // Any non-stock weapons need to be added here for use during initialization, and new entries created in the enum.
        // This include stock weapons with custom FX added (rather than stock fx modified) due to the way the arrays are modified. A more elegant solution needs to be created. 
        public static Weapon[] ExtWeapons = new Weapon[]
        {
            new MSImpulse(),
            new Burstfire(),
            new Flakshell(),
            new Plasma(),
            new MDLance(),
            new ImpactMortar()
        };

        // The active list of primary weapons at any given time. Contains stock weapons at launch.
        public static PrimaryWeapon[] primaries = new PrimaryWeapon[8]
        {
            new Impulse(),
            new Cyclone(),
            new Reflex(),
            new Crusher(),
            new Driller(),
            new Flak(),
            new Thunderbolt(),
            new Lancer()
        };

        // The active list of secondary weapons at any given time. Contains stock missiles at launch.
        public static SecondaryWeapon[] secondaries = new SecondaryWeapon[8]
        {
            new Falcon(),
            new MissilePod(),
            new Hunter(),
            new Creeper(),
            new Nova(),
            new Devastator(),
            new TimeBomb(),
            new Vortex()
        };

        // populated in MPWeapons_ProjectileManager_ReadProjPresetData, allows looking up a Weapon from a ProjPrefabExt index
        public static Weapon[] WeaponLookup;

        // Initialized in MPWeapons_ProjectileManager_ReadProjPresetData, allows substitution of spew.
        public static GameObject[] item_prefabs; // contains references to the default item prefabs. Don't change this.
        public static int[] WeaponItems = new int[8] // contains the indexes for the appropriate weapon->prefab translation
        {
            (int)ItemPrefab.entity_item_impulse,
            (int)ItemPrefab.entity_item_cyclone,
            (int)ItemPrefab.entity_item_reflex,
            (int)ItemPrefab.entity_item_crusher,
            (int)ItemPrefab.entity_item_driller,
            (int)ItemPrefab.entity_item_flak,
            (int)ItemPrefab.entity_item_thunderbolt,
            (int)ItemPrefab.entity_item_lancer
        };
        public static int[] MissileItems = new int[8] // contains the indexes for the appropriate missile->prefab translation
        {
            (int)ItemPrefab.entity_item_falcon4pack,
            (int)ItemPrefab.entity_item_missile_pod,
            (int)ItemPrefab.entity_item_hunter4pack,
            (int)ItemPrefab.entity_item_creeper,
            (int)ItemPrefab.entity_item_nova,
            (int)ItemPrefab.entity_item_devastator,
            (int)ItemPrefab.entity_item_timebomb,
            (int)ItemPrefab.entity_item_vortex
        };

        public static Dictionary<NetworkHash128, GameObject> m_registered_prefabs; // a direct reference to the one in Overload.Client
        public static Dictionary<Weapon, NetworkHash128> newItemPrefabs = new Dictionary<Weapon, NetworkHash128>(); // new prefabs generated from weapons

        private static Weapon reflexCheck = primaries[2]; // stopgap until I figure out a better way to do this.
        public static bool ReflexEnabled = false; // start off in "sidearm" mode

        public static bool Initialized = false;

        public static void SetReflexSlotEnabled()
        {
            //Debug.Log("CCF SetReflexSlotEnabled called, primary table contains reflex right now? " + primaries.Contains(reflexCheck));
            ReflexEnabled = Menus.mms_classic_spawns || !primaries.Contains(reflexCheck);

            MenuManager.mms_powerup_filter[2] = ReflexEnabled;
        }

        public static void MaybeFireWeapon(PlayerShip ps, Player player, Ship ship)
        {
            if (ps.m_refire_time > 0f || ps.c_player.m_spectator)
            {
                return;
            }

            bool flag = false;
            if (!player.CanFireWeaponAmmo())
            {
                if ((float)player.m_energy <= 0f)
                {
                    if ((int)player.m_ammo <= 0)
                    {
                        if (player.WeaponUsesAmmo(player.m_weapon_type))
                        {
                            player.SwitchToEnergyWeapon();
                        }
                        flag = true;
                    }
                    else if (!player.SwitchToAmmoWeapon())
                    {
                        flag = true;
                    }
                }
                else
                {
                    player.SwitchToEnergyWeapon();
                }
                if (!flag)
                {
                    ps.m_refire_time = 0.5f;
                    return;
                }
            }
            if (GameplayManager.IsMultiplayerActive && player.m_spawn_invul_active)
            {
                player.m_timer_invuln = (float)player.m_timer_invuln - (float)NetworkMatch.m_respawn_shield_seconds;
            }
            ps.m_alternating_fire = !ps.m_alternating_fire;
            float refire_multiplier = ((!flag) ? 1f : 3f);
            ps.FiringVolumeModifier = 1f;
            ps.FiringPitchModifier = 0f;

            ship.primaries[(int)player.m_weapon_type].Fire(refire_multiplier);

            if (ps.m_refire_time < 0.01f)
            {
                ps.m_refire_time = 0.01f;
            }
        }

        public static void MaybeFireMissile(PlayerShip ps, Player player, Ship ship)
        {
            if (ps.m_refire_missile_time > 0f || player.m_spectator)
            {
                return;
            }
            if (!player.CanFireMissileAmmo())
            {
                player.m_old_missile_type = player.m_missile_type;
                if (player.m_missile_type_prev != MissileType.NUM)
                {
                    player.Networkm_missile_type = player.m_missile_type_prev;
                    if ((int)player.m_missile_ammo[(int)player.m_missile_type] <= 0)
                    {
                        player.SwitchToNextMissileWithAmmo();
                    }
                    else
                    {
                        ps.MissileSelectFX();
                    }
                    player.UpdateCurrentMissileName();
                }
                else
                {
                    player.SwitchToNextMissileWithAmmo();
                }
                player.FindBestPrevMissile();
                ps.m_refire_missile_time = 0.5f;
                return;
            }
            if (GameplayManager.IsMultiplayerActive && player.m_spawn_invul_active)
            {
                player.m_timer_invuln = (float)player.m_timer_invuln - (float)NetworkMatch.m_respawn_shield_seconds;
            }

            Vector2 lastPos = Vector2.zero;

            if (!GameplayManager.IsMultiplayer && MenuManager.opt_use_tobii_secondaryaim && UIManager.GetEyeTrackingActivePos(ref lastPos, smoothed: false))
            {
                Vector3 direction = ps.c_camera.ScreenPointToRay(TobiiAPI.GetGazePoint().Screen).direction;
                ps.c_transform.localRotation.SetLookRotation(direction, ship.c_up);
            }
            if (!Player.CheatUnlimited)
            {
                //player.m_missile_ammo[(int)player.m_missile_type] -= 1;
                ref CodeStage.AntiCheat.ObscuredTypes.ObscuredInt reference = ref player.m_missile_ammo[(int)player.m_missile_type];
                reference = reference - 1;
            }
            ps.FiringVolumeModifier = 1f;

            ship.secondaries[(int)player.m_missile_type].Fire(1f); // refire_multiplier isn't used with secondaries

            if (ps.m_refire_missile_time < 5f) // this looks odd but it's to prevent DETONATOR-type missiles from switching away if they're empty (ie. set your DETONATOR refires quite high)
            {
                player.MaybeSwitchToNextMissile();
            }
            if (ps.m_refire_missile_time < 0.01f)
            {
                ps.m_refire_missile_time = 0.01f;
            }

            // Moved from MPCreeperSync
            // When sniper packets are enabled, this code is not needed as missile firing synchronization happens automatically.
            if (MPSniperPackets.enabled)
            {
                return;
            }

            if (!GameplayManager.IsMultiplayerActive || !Server.IsActive() || !(ps.m_refire_missile_time == 1f && player.m_old_missile_type != MissileType.NUM && player.m_missile_ammo[(int)player.m_old_missile_type] == 0)) // just switched?
                return;

            // make sure ammo is also zero on the client
            player.CallRpcSetMissileAmmo((int)player.m_old_missile_type, 0);

            // workaround for not updating missle name in hud
            player.CallRpcSetMissileType(player.m_missile_type);
            player.CallTargetUpdateCurrentMissileName(player.connectionToClient);
        }

        // THIS NEEDS FINISHING - not currently used
        public static bool CycleWeapon(Player p, bool prev = false)
        {
            int curr = (int)p.m_weapon_type;
            int next = curr;

            for (int i = 0; i < 9; i++) // try all 8 slots then give up and go back to the first
            {
                next = (next + ((!prev) ? 1 : 7)) % 8;

                //if (primaries[MPWeaponCycling.pPos[i]])
            }
            next = (next + ((!prev) ? 1 : 7)) % 8;

            return false;
        }

        public static void SetWeapon(Player p, WeaponType wt)
        {
            p.m_weapon_type_prev = p.m_weapon_type;
            p.Networkm_weapon_type = wt;
            p.CallCmdSetCurrentWeapon(p.m_weapon_type);
            p.c_player_ship.WeaponSelectFX();
            p.UpdateCurrentWeaponName();
        }

        public static void UpdateWeaponList()
        {
            if (!Initialized)
                return;

            if (MPShips.allowed > 0)
            {
                primaries[0] = MasterPrimaries["MSImpulse"];
                primaries[1] = MasterPrimaries["Cyclone"];
                primaries[2] = MasterPrimaries["Plasma"];
                primaries[3] = MasterPrimaries["Crusher"];
                primaries[4] = MasterPrimaries["Burstfire"];
                //primaries[5] = MasterPrimaries["Flak"];
                primaries[5] = MasterPrimaries["Flakshell"];
                primaries[6] = MasterPrimaries["Thunderbolt"];
                primaries[7] = MasterPrimaries["MDLance"];

                secondaries[0] = MasterSecondaries["Falcon"];
                secondaries[1] = MasterSecondaries["MissilePod"];
                secondaries[2] = MasterSecondaries["Hunter"];
                secondaries[3] = MasterSecondaries["Creeper"];
                secondaries[4] = MasterSecondaries["Nova"];
                secondaries[5] = MasterSecondaries["Devastator"];
                secondaries[6] = MasterSecondaries["ImpactMortar"];
                secondaries[7] = MasterSecondaries["Vortex"];
                PrefabManager.item_prefabs[MissileItems[6]] = secondaries[6].itemPrefab;
                PrefabManager.item_prefabs[MissileItems[6]].GetComponent<Item>().m_type = ItemType.MISSILE_TIMEBOMB;
            }
            else
            {
                primaries[0] = MasterPrimaries["Impulse"];
                primaries[1] = MasterPrimaries["Cyclone"];
                primaries[2] = MasterPrimaries["Reflex"];
                primaries[3] = MasterPrimaries["Crusher"];
                primaries[4] = MasterPrimaries["Driller"];
                primaries[5] = MasterPrimaries["Flak"];
                primaries[6] = MasterPrimaries["Thunderbolt"];
                primaries[7] = MasterPrimaries["Lancer"];

                secondaries[0] = MasterSecondaries["Falcon"];
                secondaries[1] = MasterSecondaries["MissilePod"];
                secondaries[2] = MasterSecondaries["Hunter"];
                secondaries[3] = MasterSecondaries["Creeper"];
                secondaries[4] = MasterSecondaries["Nova"];
                secondaries[5] = MasterSecondaries["Devastator"];
                secondaries[6] = MasterSecondaries["TimeBomb"];
                secondaries[7] = MasterSecondaries["Vortex"];
                PrefabManager.item_prefabs[MissileItems[6]] = secondaries[6].itemPrefab;
                PrefabManager.item_prefabs[MissileItems[6]].GetComponent<Item>().m_type = ItemType.MISSILE_TIMEBOMB;
            }

            SetReflexSlotEnabled();

            MethodInfo PRIMARY_REFRESH = typeof(Player.WeaponNamesType).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo SECONDARY_REFRESH = typeof(Player.MissileNamesType).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo SECONDARY_PLURAL_REFRESH = typeof(Player.MissileNamesPluralType).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < 8; i++)
            {
                Player.WEAPON_2A_TAG[i] = MPWeapons.primaries[i].Tag2A;
                Player.WEAPON_2B_TAG[i] = MPWeapons.primaries[i].Tag2B;
                Player.MISSILE_2A_TAG[i] = MPWeapons.secondaries[i].Tag2A;
                Player.MISSILE_2B_TAG[i] = MPWeapons.secondaries[i].Tag2B;

                Player.MAX_MISSILE_AMMO[i] = MPWeapons.secondaries[i].ammo; // Interestingly, these arrays are readonly. This however does *not* make the elements readonly, only the array structure itself. Huh.
                Player.MAX_MISSILE_AMMO_UP[i] = MPWeapons.secondaries[i].ammoUp;
                Player.SUPER_MISSILE_AMMO_MP[i] = MPWeapons.secondaries[i].ammoSuper;
            }

            PRIMARY_REFRESH.Invoke(Player.WeaponNames, new object[0]);
            SECONDARY_REFRESH.Invoke(Player.MissileNames, new object[0]);
            SECONDARY_PLURAL_REFRESH.Invoke(Player.MissileNamesPlural, new object[0]);

            //Debug.Log("CCF missile prefab " + ((ItemPrefab)MissileItems[6]).ToString() + " currently is of m_type " + PrefabManager.item_prefabs[MissileItems[6]].GetComponent<Item>().m_type.ToString());
        }

        public static void UpdateProjectileSync()
        {
            MPCreeperSync.MoveSync.Clear();
            MPCreeperSync.ExplodeSync.Clear();

            foreach (Weapon w in primaries.Union<Weapon>(secondaries))
            {
                if (w.MoveSync)
                {
                    //Debug.Log("CCF Adding " + w.displayName + " to the movement sync list");
                    MPCreeperSync.MoveSync.Add(w.projprefab);
                }
                if (w.ExplodeSync)
                {
                    //Debug.Log("CCF Adding " + w.displayName + " to the explosion sync list");
                    MPCreeperSync.ExplodeSync.Add(w.projprefab);
                }
            }
        }


        public static void SetPrimaryNames(ref string[] values)
        {
            for (int i = 0; i < 8; i++)
            {
                values[i] = Loc.LS(primaries[i].displayName);
            }
        }

        public static void SetSecondaryNames(ref string[] values)
        {
            for (int i = 0; i < 8; i++)
            {
                values[i] = Loc.LS(secondaries[i].displayName);
            }
        }

        public static bool IsOwnedByPlayer(Projectile proj)
        {
            if (proj.m_owner != null && proj.m_owner.GetComponent<Player>() != null)
            {
                return true;
            }
            return false;
        }

        public static void AddForceAndTorque(PlayerShip ps, DamageInfo di)
        {
            Weapon weapon = WeaponLookup[(int)di.weapon];
            if (weapon == null || !WeaponLookup[(int)di.weapon].ImpactForce)
                return;

            Vector3 force = di.push_force * di.push_dir / 5f; // *1000f in the Projectile stuff, gotta undo it somewhat

            ps.c_rigidbody.AddForceAtPosition(force, Vector3.LerpUnclamped(ps.c_transform_position, di.pos, 10f), ForceMode.Impulse);
        }

        // Item spawn handler replacements - same as stock, they were just private
        public static GameObject NetworkSpawnItemExtHandler(Vector3 pos, NetworkHash128 asset_id)
        {
            GameObject prefabFromAssetId = Client.GetPrefabFromAssetId(asset_id);
            if (prefabFromAssetId == null)
            {
                Debug.LogErrorFormat("Error looking up item prefab with asset_id {0}", asset_id.ToString());
                return null;
            }
            GameObject gameObject = Object.Instantiate(prefabFromAssetId, pos, Quaternion.identity);
            if (gameObject == null)
            {
                Debug.LogErrorFormat("Error instantiating item prefab {0}", prefabFromAssetId.name);
                return null;
            }
            return gameObject;
        }

        public static void NetworkUnspawnItemExtHandler(GameObject spawned)
        {
            Object.Destroy(spawned);
        }

        public static void RegisterSpawnItemExtHandler(GameObject prefab, string nameForHash)
        {
            if (m_registered_prefabs == null)
            {
                m_registered_prefabs = (Dictionary<NetworkHash128, GameObject>)AccessTools.Field(typeof(Client), "m_registered_prefabs").GetValue(null);
            }

            if (prefab == null)
            {
                Debug.LogError("null prefab passed to MPWeapons::RegisterSpawnExtHandler()");
                return;
            }

            if (!m_registered_prefabs.ContainsValue(prefab))
            {
                NetworkHash128 assetId = NetworkHash128.Parse(nameForHash);

                NetworkIdentity ni = prefab.GetComponent<NetworkIdentity>();
                FieldInfo m_AssetId = AccessTools.Field(typeof(NetworkIdentity), "m_AssetId");
                m_AssetId.SetValue(ni, assetId);
                m_registered_prefabs.Add(assetId, prefab);

                Debug.Log("CCF Adding item prefab for " + nameForHash + " to the m_registered_prefabs dictionary with hash " + assetId.ToString());

                ClientScene.RegisterSpawnHandler(assetId, NetworkSpawnItemExtHandler, NetworkUnspawnItemExtHandler);
            }
        }
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Weapon Template
    // ====================================================================
    //
    //
    // ====================================================================


    public abstract class Weapon
    {
        public string displayName;
        public string Tag2A;
        public string Tag2B;
        public int icon_idx; // look up icon indices in AtlasIndex0.cs

        public FiringMode firingMode = FiringMode.AUTO;

        public ProjPrefabExt projprefab;
        public Projectile projectile;
        public FXWeaponEffect bounceFX = FXWeaponEffect.none;
        public FXWeaponExplosion bounceExp = FXWeaponExplosion.none;

        protected ItemPrefab itemID = ItemPrefab.none; // only used during init, does not need to be set for stock weapons
        public GameObject itemPrefab;
        public NetworkHash128 hash; // this will be set when a handler is registered

        public bool MineHoming = false; // set true to use omni-directional homing instead of missile-style
        public bool ImpactForce = false; // set true to allow the weapon to shove people around in multiplayer
        public bool WarnSelect = false; // plays a loud warning when this weapon is selected.
        public bool MoveSync = false; // designates that the projectile should have its position and velocity forcibly synced
        public bool ExplodeSync = false; // designates that the projectile should have its explosion forcibly synced

        public Ship ship;
        public PlayerShip ps;
        public Player player;

        public string projMeshName;
        public GameObject projMesh; // replacement projectile mesh
        public string[] extraNames;
        public GameObject[] extras = new GameObject[0];

        //temporary
        public abstract void Fire(float refire_multiplier);
        //public abstract void ServerFire(Player player, float refire_multiplier);


        //public abstract void FirePressed();

        //public abstract void FireReleased();

        public abstract void DrawHUDReticle(Vector2 pos, float m_alpha);

        // Assigns the offset ID and creates (or returns, if there isn't a custom overridden method in the weapon) the prefabs associated with this weapon.
        // This only needs to be overridden if the weapon uses a custom projectile.
        public virtual GameObject GenerateProjPrefab()
        {
            GameObject go = ProjectileManager.proj_prefabs[(int)projprefab];
            projectile = go.GetComponent<Projectile>();
            if (itemID != ItemPrefab.none)
            {
                itemPrefab = PrefabManager.item_prefabs[(int)itemID];
            }
            else
            {
                Debug.Log("ERROR GENERATING WEAPON " + displayName + " - GenerateProjPrefab was called on a stock weapon, this should not happen. This weapon's drop item is now an impulse.");
                itemPrefab = PrefabManager.item_prefabs[(int)ItemPrefab.entity_item_impulse];
            }
            return go;
        }

        // Called when the weapon's projectile (or sub-projectile) hits something. Extend and call base.Explode() as the first thing (or replace this entirely) for custom behaviour.
        public virtual void Explode(Projectile proj, bool damaged_something, FXWeaponExplosion m_death_particle_override, float strength, WeaponUnlock m_upgrade)
        {
            FXWeaponExplosion explosion = FXWeaponExplosion.none;

            if (m_death_particle_override != 0)
            {
                explosion = m_death_particle_override;
            }
            else if (damaged_something && proj.m_death_particle_damage != 0)
            {
                explosion = proj.m_death_particle_damage;
            }
            else if (proj.m_death_particle_default != 0)
            {
                explosion = proj.m_death_particle_default;
            }
            if (explosion != FXWeaponExplosion.none)
            {
                ParticleManager.psm[3].StartParticleInstant((int)explosion, proj.c_transform.localPosition, proj.c_transform.localRotation, null, proj);
            }
        }

        // Sets any specific characteristics of the projectiles being fired. Returns true if trail particles should be bigger than normal.
        public virtual bool ProjectileFire(Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        { return false; }

        public virtual void ProcessCollision(Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            if (!m_bounce_allow)
            {
                proj.Explode(proj.ShouldPlayDamageEffect(layer));
            }
        }

        public virtual RigidbodyInterpolation Interpolation(Projectile proj)
        {
            return RigidbodyInterpolation.Interpolate;
        }

        public virtual void WeaponCharge()
        { }

        // Takes a ref to a List containing extra TrailRenderers and adds to it if necessary.
        public virtual void AddTrailRenderers(ref List<GameObject> tr)
        { }

        // Takes a ref to a List containing extra WeaponEffects and adds to it if necessary.
        public virtual void AddWeaponEffects(ref List<GameObject> fx)
        { }

        // Takes a ref to a List containing extra WeaponExplosions and adds to it if necessary.
        public virtual void AddWeaponExplosions(ref List<GameObject> ex)
        { }

        // This may occasionally need to be overridden (say if you need to attach a MonoBehaviour to the Player's game object after it's been assigned to the Weapon)
        public virtual void SetShip(Ship s)
        {
            ship = s;
            ps = s.ps;
            player = s.player;
        }

        protected Quaternion AngleRandomize(Quaternion rot, float angle, Vector3 c_up, Vector3 c_right)
        {
            Vector2 insideUnitCircle = UnityEngine.Random.insideUnitCircle;
            return AngleSpreadY(AngleSpreadX(rot, angle * insideUnitCircle.x, c_up), angle * insideUnitCircle.y, c_right);
        }

        protected Quaternion AngleSpreadX(Quaternion rot, float angle, Vector3 c_up)
        {
            Quaternion quaternion = Quaternion.AngleAxis(angle, c_up);
            return quaternion * rot;
        }

        protected Quaternion AngleSpreadY(Quaternion rot, float angle, Vector3 c_right)
        {
            Quaternion quaternion = Quaternion.AngleAxis(angle, c_right);
            return quaternion * rot;
        }

        protected Quaternion AngleSpreadZ(Quaternion rot, float angle, Vector3 c_forward)
        {
            Quaternion quaternion = Quaternion.AngleAxis(angle, c_forward);
            return quaternion * rot;
        }

        // makes a shallow copy of the Weapon object for use with a specific player
        public Weapon Copy()
        {
            return (Weapon)MemberwiseClone();
        }
    }

    public abstract class PrimaryWeapon : Weapon
    {
        public bool UsesAmmo;
        public bool UsesEnergy;
        public bool AllowedCharging = true;
    }

    public abstract class SecondaryWeapon : Weapon
    {
        public string displayNamePlural;
        public int ammo;
        public int ammoUp;
        public int ammoSuper;
        public WeaponUnlock AmmoLevelCap = WeaponUnlock.LEVEL_1; // I'm annoyed by this. Set it to the last level that a projectile should have regular capacity -EXCEPT- if you want it to be exclusive to 2A or 2B in which case, set that level.
        public ProjPrefabExt subproj = ProjPrefabExt.none;
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Utility Functions
    // ====================================================================
    //
    //
    // ====================================================================

    /*
    // CCF VERIFIED -- NO LONGER NEEDED
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPWeapons_PlayerShip_MaybeFireWeapon
    {
        static bool Prefix(PlayerShip __instance, Vector3 ___c_forward, Vector3 ___c_up, Vector3 ___c_right, ref int ___flak_fire_count)
        {
            if (!(__instance.m_refire_time <= 0f) || __instance.c_player.m_spectator)
            {
                return false;
            }

            Player player = __instance.c_player;

            // get the Ship reference and update the replacement fields
            Ship ship = MPShips.GetShip(__instance);
            ship.c_forward = ___c_forward;
            ship.c_up = ___c_up;
            ship.c_right = ___c_right;
            ship.flak_fire_count = ___flak_fire_count;

            bool flag = false;
            if (!player.CanFireWeaponAmmo())
            {
                if ((float)player.m_energy <= 0f)
                {
                    if ((int)player.m_ammo <= 0)
                    {
                        if (player.WeaponUsesAmmo(player.m_weapon_type))
                        {
                            player.SwitchToEnergyWeapon();
                        }
                        flag = true;
                    }
                    else if (!player.SwitchToAmmoWeapon())
                    {
                        flag = true;
                    }
                }
                else
                {
                    player.SwitchToEnergyWeapon();
                }
                if (!flag)
                {
                    __instance.m_refire_time = 0.5f;
                    return false;
                }
            }
            if (GameplayManager.IsMultiplayerActive && player.m_spawn_invul_active)
            {
                player.m_timer_invuln = (float)player.m_timer_invuln - (float)NetworkMatch.m_respawn_shield_seconds;
            }
            __instance.m_alternating_fire = !__instance.m_alternating_fire;
            float refire_multiplier = ((!flag) ? 1f : 3f);
            __instance.FiringVolumeModifier = 1f;
            __instance.FiringPitchModifier = 0f;

            ship.primaries[(int)player.m_weapon_type].Fire(refire_multiplier);

            if (__instance.m_refire_time < 0.01f)
            {
                __instance.m_refire_time = 0.01f;
            }

            // update the flak counter since it's used elsewhere
            ___flak_fire_count = ship.flak_fire_count;

            return false;
        }

        // THIS CAN GET COMMENTED OUT, AS CAN THE PREFIX ABOVE
        public static void MaybeFireWeapon(PlayerShip ps, Player player, Ship ship)
        {
            if (!(ps.m_refire_time <= 0f) || ps.c_player.m_spectator)
            {
                return;
            }

            bool flag = false;
            if (!player.CanFireWeaponAmmo())
            {
                if ((float)player.m_energy <= 0f)
                {
                    if ((int)player.m_ammo <= 0)
                    {
                        if (player.WeaponUsesAmmo(player.m_weapon_type))
                        {
                            player.SwitchToEnergyWeapon();
                        }
                        flag = true;
                    }
                    else if (!player.SwitchToAmmoWeapon())
                    {
                        flag = true;
                    }
                }
                else
                {
                    player.SwitchToEnergyWeapon();
                }
                if (!flag)
                {
                    ps.m_refire_time = 0.5f;
                    return;
                }
            }
            if (GameplayManager.IsMultiplayerActive && player.m_spawn_invul_active)
            {
                player.m_timer_invuln = (float)player.m_timer_invuln - (float)NetworkMatch.m_respawn_shield_seconds;
            }
            ps.m_alternating_fire = !ps.m_alternating_fire;
            float refire_multiplier = ((!flag) ? 1f : 3f);
            ps.FiringVolumeModifier = 1f;
            ps.FiringPitchModifier = 0f;

            ship.primaries[(int)player.m_weapon_type].Fire(refire_multiplier);

            if (ps.m_refire_time < 0.01f)
            {
                ps.m_refire_time = 0.01f;
            }
        }
    }
    */

    [HarmonyPatch(typeof(PlayerShip), "ProcessFiringControls")]
    static class MPWeapons_PlayerShip_ProcessFiringControls
    {
        public static bool Prefix(PlayerShip __instance, Vector3 ___c_forward, Vector3 ___c_up, Vector3 ___c_right, ref int ___flak_fire_count)
        {
            // get the Ship reference and update the replacement fields
            //Ship ship = MPShips.GetShip(__instance);
            Ship ship;
            if (!MPShips.SelectedShips.TryGetValue(__instance.netId, out ship))
            {
                return true; // bail out to the legacy code until this is resolved
            }

            Player player = __instance.c_player;

            ship.c_forward = ___c_forward;
            ship.c_up = ___c_up;
            ship.c_right = ___c_right;
            ship.flak_fire_count = ___flak_fire_count;

            bool CanFire = (MPShips.FireWhileBoost || !__instance.m_boosting) && __instance.m_wheel_select_state == WheelSelectState.NONE;
            PrimaryWeapon primary = ship.primaries[(int)player.m_weapon_type];
            SecondaryWeapon secondary = (player.m_missile_type != MissileType.NUM) ? ship.secondaries[(int)player.m_missile_type] : ship.secondaries[0]; // if it's forced loadouts and there isn't one equipped at all, pull Falcons up as a filler.

            FiringMode pMode = (primary.firingMode == FiringMode.SEMI_AUTO && __instance.c_player.m_overdrive) ? FiringMode.AUTO : primary.firingMode;
            FiringMode sMode = secondary.firingMode;
            bool firing = false;

            // Primary firing
            if (player.m_weapon_type != WeaponType.NUM)
            {
                if (player.IsPressed(CCInput.FIRE_WEAPON) && CanFire)
                {
                    firing = true;

                    if (player.JustPressed(CCInput.FIRE_WEAPON))
                    {
                        if (__instance.m_refire_time <= 0f)
                        {
                            __instance.m_refire_time = 0f;
                        }
                        else if (pMode == FiringMode.SEMI_AUTO && GameplayManager.IsMultiplayerActive && __instance.m_refire_time > 0f)
                        {
                            __instance.m_refire_time = Mathf.Max(__instance.m_refire_time, 0.05f); // 1f / 20f -- from MPAnticheat.cs
                        }
                    }

                    if (pMode == FiringMode.CHARGED && ((float)player.m_energy > 0f || player.m_overdrive))
                    {
                        primary.WeaponCharge();
                    }
                    if (pMode == FiringMode.AUTO || (pMode == FiringMode.SEMI_AUTO && player.JustPressed(CCInput.FIRE_WEAPON)))
                    {
                        MPWeapons.MaybeFireWeapon(__instance, player, ship);
                    }
                }
                else if (player.JustReleased(CCInput.FIRE_WEAPON))
                {
                    if (pMode == FiringMode.CHARGED)
                    {
                        if (CanFire)
                        {
                            MPWeapons.MaybeFireWeapon(__instance, player, ship);
                        }
                        else
                        {
                            __instance.m_thunder_power = 0f;
                        }
                    }
                    /* I don't think this is needed? It's already covered in the weapon classes
                    else if (c_player.m_weapon_type == WeaponType.LANCER)
                    {
                        m_alternating_fire = !m_alternating_fire;
                    }
                    */
                }
                else
                {
                    __instance.m_thunder_power = 0f;
                }
            }
            if (!firing)
            {
                ship.flak_fire_count = 0;
            }
            // update the flak counter since it's used elsewhere
            ___flak_fire_count = ship.flak_fire_count;


            // Secondary firing
            if (player.m_missile_type != MissileType.NUM)
            {
                if (player.IsPressed(CCInput.FIRE_MISSILE) && CanFire)
                {
                    if (player.JustPressed(CCInput.FIRE_MISSILE))
                    {
                        __instance.m_refire_missile_time = (__instance.m_refire_missile_time < 0f) ? 0f : __instance.m_refire_missile_time;
                    }

                    if (sMode == FiringMode.DETONATOR)
                    {
                        if (__instance.m_refire_missile_time > 0f && player.JustPressed(CCInput.FIRE_MISSILE) && player.m_missile_level[(int)player.m_missile_type] >= WeaponUnlock.LEVEL_1) // a missile with a detonator is in-flight, and "Fire Missile" was just pressed (and it's at least level 1)
                        {
                            ProjectileManager.ExplodePlayerDetonators(player);
                        }
                        else
                        {
                            MPWeapons.MaybeFireMissile(__instance, player, ship);
                        }
                    }
                    if (sMode == FiringMode.AUTO || sMode == FiringMode.STREAM || (sMode == FiringMode.SEMI_AUTO && player.JustPressed(CCInput.FIRE_MISSILE)))
                    {
                        MPWeapons.MaybeFireMissile(__instance, player, ship);
                    }
                }
                else if (player.JustReleased(CCInput.FIRE_MISSILE) && CanFire && sMode == FiringMode.STREAM)
                {
                    __instance.m_alternating_missile_fire = !__instance.m_alternating_missile_fire;
                }
            }

            // Moved over from MPSniperPackets.cs
            if (player.isLocalPlayer && (MPSniperPackets.enabled))
            {
                if (player.JustReleased(CCInput.FIRE_WEAPON))
                {
                    if (primary.UsesEnergy)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                        {
                            m_player_id = player.netId,
                            m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                            m_value = player.m_energy
                        });
                    }
                    if (primary.UsesAmmo)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                        {
                            m_player_id = player.netId,
                            m_type = PlayerSyncResourceMessage.ValueType.AMMO,
                            m_value = player.m_ammo
                        });
                    }
                }

                if (player.JustReleased(CCInput.USE_BOOST))
                {
                    Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                    {
                        m_player_id = player.netId,
                        m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                        m_value = player.m_energy
                    });
                }

                if (player.isLocalPlayer && player.JustReleased(CCInput.FIRE_MISSILE))
                {
                    MPSniperPackets.justFiredDev = false;
                }
            }

            return false;
        }
    }

    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "WeaponUsesAmmo")]
    static class MPWeapons_Player_WeaponUsesAmmo
    {
        public static bool Prefix(ref bool __result, Player __instance, WeaponType wt)
        {
            if (wt == WeaponType.NUM)
            {
                wt = __instance.m_weapon_type;
            }

            __result = MPWeapons.primaries[(int)wt].UsesAmmo;

            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "WeaponUsesAmmo2")]
    static class MPWeapons_Player_WeaponUsesAmmo2
    {
        public static bool Prefix(ref bool __result, WeaponType wt)
        {
            __result = MPWeapons.primaries[(int)wt].UsesAmmo;

            return false;
        }
    }


    //CCF NEEDS REBUILDING STILL - WILL NEED TO REDO AUTOSELECT TO DO THIS PROPERLY AAAERGFGGGHHH
    /*
    // Rebuilt to handle modular weapons, autoselect ordering, and weapon exclusion
    [HarmonyPatch(typeof(Player), "NextWeapon")]
    static class MPWeapons_Player_NextWeapon
    {
        public static bool Prefix(Player __instance, bool prev = false)
        {
            if (__instance.CanFireWeapon())
            {
            }

            return false;
        }
    }
    */


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "CanFireWeapon")]
    static class MPWeapons_Player_CanFireWeapon
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            if ((int)__instance.m_ammo > 0 && __instance.AnyAmmoWeapons())
            {
                if (__instance.WeaponUsesAmmo(__instance.m_weapon_type))
                {
                    __result = true;
                    return false;
                }
                __result = (float)__instance.m_energy > 0f;
                return false;
            }
            if (__instance.WeaponUsesAmmo(__instance.m_weapon_type))
            {
                __result = false;
                return false;
            }
            __result = true;
            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "CanFireWeaponAmmo")]
    static class MPWeapons_Player_CanFireWeaponAmmo
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            if (__instance.m_overdrive || Player.CheatUnlimited)
            {
                __result = true;
                return false;
            }
            if (MPWeapons.primaries[(int)__instance.m_weapon_type].UsesEnergy && __instance.m_energy > 0f)
            {
                __result = true;
                return false;
            }
            else if (MPWeapons.primaries[(int)__instance.m_weapon_type].UsesAmmo && __instance.m_ammo > 0f)
            {
                __result = true;
                return false;
            }
            else
            {
                __result = false;
                return false;
            }
        }
    }


    [HarmonyPatch(typeof(Projectile), "IsMissile")]
    static class MPWeapons_Projectile_IsMissile
    {
        public static bool Prefix(ref bool __result, Projectile __instance)
        {
            if (MPWeapons.WeaponLookup[(int)__instance.m_type].GetType().IsSubclassOf(typeof(SecondaryWeapon)))
            {
                __result = true;
            }
            else
            {
                __result = false;
            }

            return false;
        }
    }


    [HarmonyPatch(typeof(Player), "GetMaxMissileAmmo")]
    static class MPWeapons_Player_GetMaxMissileAmmo
    {
        public static bool Prefix(ref int __result, Player __instance, MissileType mt)
        {
            WeaponUnlock level = MPWeapons.secondaries[(int)mt].AmmoLevelCap;

            if ((level == WeaponUnlock.LEVEL_2A || level == WeaponUnlock.LEVEL_2B) && __instance.m_missile_level[(int)mt] == level)
            {
                __result = Player.MAX_MISSILE_AMMO_UP[(int)mt];
            }
            else if (__instance.m_missile_level[(int)mt] > level)
            {
                __result = Player.MAX_MISSILE_AMMO_UP[(int)mt];
            }
            else
            {
                __result = Player.MAX_MISSILE_AMMO[(int)mt];
            }

            //Debug.Log("CCF max missile ammo for pickup index " + (int)mt + " is: " + __result +" -- actual maxes are " + Player.MAX_MISSILE_AMMO[(int)mt] + " and upgraded " + Player.MAX_MISSILE_AMMO_UP[(int)mt]);
            //Debug.Log("CCF current missile ammo for pickup index " + (int)mt + " is: " + __instance.m_missile_ammo[(int)mt]);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "MissileSelectFX")]
    static class MPWeapons_PlayerShip_MissileSelectFX
    {
        public static bool Prefix(PlayerShip __instance)
        {
            if (GameManager.m_game_state != GameManager.GameState.GAMEPLAY)
            {
                return false;
            }
            if (__instance.isLocalPlayer)
            {
                UIElement.WEAPON_SELECT_FLASH = 1.25f;
                UIElement.WEAPON_SELECT_NAME = string.Format(Loc.LS("{0} SELECTED"), Player.MissileNames[__instance.c_player.m_missile_type]);
                SFXCueManager.PlayCue2D(SFXCue.hud_cycle_typeA2);
                GameManager.m_audio.PlayCue2D(362, 0.1f);

                // moved from MPAutoSelection and MPSniperPackets, should cover both cases
                if (MPWeapons.secondaries[(int)__instance.c_player.m_missile_type].WarnSelect && __instance.c_player.m_old_missile_type != __instance.c_player.m_missile_type)
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_warning_selected_dev);

                    /*
                    if (MPAutoSelection.zorc)
                    {
                        SFXCueManager.PlayCue2D(SFXCue.enemy_boss1_alert, 1f, 0f, 0f, false);
                        GameplayManager.AlertPopup(string.Format(Loc.LS("{0} SELECTED"), Player.MissileNames[__instance.c_player.m_missile_type]), string.Empty, 5f);
                    }
                    */
                }

            }
            __instance.SetRefireDelayAfterMissileSwitch();

            return false;
        }
    }

    // THESE 2 METHODS NEED AN OVERHAUL -- NEXTWEAPON NEEDS REIMPLEMENTING WITH AUTOSELECT STUFF REWRITTEN
    /*
    // TODO - bring in changes from MPAutoSelection
    // - bring in changes from MPSniperPackets
    // - bring in changes from MPWeaponCycling
    // NEEDS VERIFYING AGAIN
    [HarmonyPatch(typeof(Player), "SwitchToAmmoWeapon")]
    static class MPWeapons_Player_SwitchToAmmoWeapon
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            __result = false;
            MPWeaponCycling.PBypass = true;

            for (int i = 0; i < 8; i++)
            {
                if (MPWeapons.primaries[i].UsesAmmo)
                {
                    __instance.Networkm_weapon_type = (WeaponType)i;
                    __instance.NextWeapon();
                    __result = true;
                    break;
                }
            }

            MPWeaponCycling.PBypass = false;

            return false;

            for (int i = 0; i < 8; i++)
            {


            }
        }
    }


    // TODO - bring in changes from MPAutoSelection
    // - bring in changes from MPSniperPackets
    // - bring in changes from MPWeaponCycling
    // NEEDS VERIFYING AGAIN
    [HarmonyPatch(typeof(Player), "SwitchToEnergyWeapon")]
    static class MPWeapons_Player_SwitchToEnergyWeapon
    {
        public static bool Prefix(Player __instance)
        {
            MPWeaponCycling.PBypass = true;

            for (int i = 7; i >= 0; i--)
            {
                if (MPWeapons.primaries[i].UsesEnergy)
                {
                    __instance.Networkm_weapon_type = (WeaponType)i;
                }
                __instance.NextWeapon();
                break;
            }

            MPWeaponCycling.PBypass = false;

            return false;
        }
    }
    */


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "AnyAmmoWeapons")]
    static class MPWeapons_Player_AnyAmmoWeapons
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            __result = false;
            for (int i = 0; i < 8; i++)
            {
                if (MPWeapons.primaries[i].UsesAmmo && __instance.m_weapon_level[i] != WeaponUnlock.LOCKED)
                {
                    __result = true;
                    break;
                }
            }

            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "OnlyAmmoWeapons")]
    static class MPWeapons_Player_OnlyAmmoWeapons
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            __result = true;
            for (int i = 0; i < 8; i++)
            {
                if (MPWeapons.primaries[i].UsesEnergy && __instance.m_weapon_level[i] != WeaponUnlock.LOCKED)
                {
                    __result = false;
                    break;
                }
            }

            return false;
        }
    }


    // hooks in to allow weapons to physically punch ships in multiplayer
    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    static class MPWeapons_PlayerShip_ApplyDamage
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {

            foreach (var code in codes)
            {
                yield return code;
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(PlayerShip), "CallRpcApplyDamageEffects"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons), "AddForceAndTorque"));
                }
            }
        }
    }


    // replaces the kill type assessment function -- this needs replacing with something more elegant
    [HarmonyPatch(typeof(Player), "GetKillTypeFromDamageInfo")]
    static class MPWeapons_Player_GetKillTypeFromDamageInfo
    {
        public static bool Prefix(ref int __result, DamageInfo di)
        {
            for (int i = 0; i < 8; i++)
            {
                if ((int)di.weapon == (int)MPWeapons.primaries[i].projprefab)
                {
                    __result = i;
                    return false;
                }
                else if ((int)di.weapon == (int)MPWeapons.secondaries[i].projprefab || (int)di.weapon == (int)MPWeapons.secondaries[i].subproj)
                {
                    __result = i + 8;
                    return false;
                }
            }

            if (di.weapon == ProjPrefab.proj_melee)
            {
                __result = 16;
            }
            else
            {
                __result = -1;
            }
            return false;
        }
    }


    // name replacement for primaries
    [HarmonyPatch(typeof(Player.WeaponNamesType), "Refresh")]
    static class MPWeapons_Player_WeaponNamesType_Refresh
    {
        // Postfix instead of Pre(false) to allow the array to be created properly in the original before the values are replaced
        public static void Postfix(ref string[] ___m_values)
        {
            for (int i = 0; i < 8; i++)
            {
                ___m_values[i] = Loc.LS(MPWeapons.primaries[i].displayName);
            }
        }
    }


    // name replacement for secondaries
    [HarmonyPatch(typeof(Player.MissileNamesType), "Refresh")]
    static class MPWeapons_Player_MissileNamesType_Refresh
    {
        // Postfix instead of Pre(false) to allow the array to be created properly in the original
        public static void Postfix(ref string[] ___m_values)
        {
            for (int i = 0; i < 8; i++)
            {
                ___m_values[i] = Loc.LS(MPWeapons.secondaries[i].displayName);
            }
        }
    }

    // name replacement for secondaries, plural version
    [HarmonyPatch(typeof(Player.MissileNamesPluralType), "Refresh")]
    static class MPWeapons_Player_MissileNamesPluralType_Refresh
    {
        // Postfix instead of Pre(false) to allow the array to be created properly in the original
        public static void Postfix(ref string[] ___m_values)
        {
            for (int i = 0; i < 8; i++)
            {
                ___m_values[i] = Loc.LS(MPWeapons.secondaries[i].displayNamePlural);
            }
        }
    }

    /*
    // missile limit and weapon tag changes
    [HarmonyPatch(typeof(Player), MethodType.Constructor)]
    static class MPWeapons_Player_Constructor
    {
        static MethodInfo PRIMARY_REFRESH = typeof(Player.WeaponNamesType).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
        static MethodInfo SECONDARY_REFRESH = typeof(Player.MissileNamesType).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
        static MethodInfo SECONDARY_PLURAL_REFRESH = typeof(Player.MissileNamesPluralType).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix()
        {
            //if (MPWeapons.NeedsUpdate)
            //{
            //MPWeapons.NeedsUpdate = false;

            for (int i = 0; i < 8; i++)
            {
                Player.WEAPON_2A_TAG[i] = MPWeapons.primaries[i].Tag2A;
                Player.WEAPON_2B_TAG[i] = MPWeapons.primaries[i].Tag2B;
                Player.MISSILE_2A_TAG[i] = MPWeapons.secondaries[i].Tag2A;
                Player.MISSILE_2B_TAG[i] = MPWeapons.secondaries[i].Tag2B;

                Player.MAX_MISSILE_AMMO[i] = MPWeapons.secondaries[i].ammo; // Interestingly, these arrays are readonly. This however does *not* make the elements readonly, only the array structure itself. Huh.
                Player.MAX_MISSILE_AMMO_UP[i] = MPWeapons.secondaries[i].ammoUp;
                Player.SUPER_MISSILE_AMMO_MP[i] = MPWeapons.secondaries[i].ammoSuper;

                //Debug.Log("CCF setting max missile ammo for pickup index " + i + " to: " + Player.MAX_MISSILE_AMMO[i]);
                //Debug.Log("CCF setting max missile ammo (upgraded) for pickup index " + i + " to: " + Player.MAX_MISSILE_AMMO_UP[i]);
                //Debug.Log("CCF setting super missile ammo for pickup index " + i + " to: " + Player.SUPER_MISSILE_AMMO_MP[i]);
            }

            PRIMARY_REFRESH.Invoke(Player.WeaponNames, new object[0]);
            SECONDARY_REFRESH.Invoke(Player.MissileNames, new object[0]);
            SECONDARY_PLURAL_REFRESH.Invoke(Player.MissileNamesPlural, new object[0]);
        }
    }
    */

    // Primary weapon icon references
    [HarmonyPatch(typeof(UIElement), "DrawHUDPrimaryWeapon")]
    public static class MPWeapons_UIElement_DrawHUDPrimaryWeapon
    {
        public static int GetIcon(WeaponType wt)
        {
            return MPWeapons.primaries[(int)wt].icon_idx - 26; // 26 is added to the weapon index to get the sprite
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool found = false;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 26)
                {
                    found = true;
                }
                else if (found && code.opcode == OpCodes.Add)
                {
                    found = false;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_UIElement_DrawHUDPrimaryWeapon), "GetIcon"));
                }
                yield return code;
            }
        }
    }


    // Secondary weapon icon references
    [HarmonyPatch(typeof(UIElement), "DrawHUDSecondaryWeapon")]
    public static class MPWeapons_UIElement_DrawHUDSecondaryWeapon
    {
        public static int GetIcon(MissileType mt)
        {
            return MPWeapons.secondaries[(int)mt].icon_idx - 104; // 104 is added to the weapon index to get the sprite
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool found = false;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 104)
                {
                    found = true;
                }
                else if (found && code.opcode == OpCodes.Add)
                {
                    found = false;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_UIElement_DrawHUDSecondaryWeapon), "GetIcon"));
                }
                yield return code;
            }
        }
    }


    // Primary weapon reticle references
    [HarmonyPatch(typeof(UIElement), "DrawHUDWeaponReticle")]
    public static class MPWeapons_UIElement_DrawHUDWeaponReticle
    {
        public static void DrawReticle(WeaponType wt, Vector2 pos, float m_alpha)
        {
            MPWeapons.primaries[(int)wt].DrawHUDReticle(pos, m_alpha);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Switch) // skip the whole thing
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UIElement), "m_alpha"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_UIElement_DrawHUDWeaponReticle), "DrawReticle"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(UIElement), "m_alpha"));
                    yield return new CodeInstruction(OpCodes.Ret);
                    break;
                }
                else
                {
                    yield return code;
                }
            }
        }
    }


    // Secondary weapon reticle references
    [HarmonyPatch(typeof(UIElement), "DrawHUDMissileReticle")]
    public static class MPWeapons_UIElement_DrawHUDMissileReticle
    {
        public static void DrawReticle(MissileType mt, Vector2 pos, float m_alpha)
        {
            MPWeapons.secondaries[(int)mt].DrawHUDReticle(pos, m_alpha);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Switch) // skip the whole thing
                {
                    //yield return new CodeInstruction(OpCodes.Ldarga_S, 1);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UIElement), "m_alpha"));
                    yield return new CodeInstruction(OpCodes.Ldarg_2); // we're passing through "ret_fade * m_alpha" instead of just m_alpha here
                    yield return new CodeInstruction(OpCodes.Mul);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_UIElement_DrawHUDMissileReticle), "DrawReticle"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(UIElement), "m_alpha"));
                    yield return new CodeInstruction(OpCodes.Ret);
                    break;
                }
                else
                {
                    yield return code;
                }
            }
        }
    }


    [HarmonyPatch(typeof(Player), "AddEnergyDefault")]
    public static class MPWeapons_Player_AddEnergyDefault
    {
        public static bool Prefix(Player __instance)
        {
            if (MPWeapons.primaries[(int)__instance.m_weapon_type].AllowedCharging)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    // ====================================================================
    //
    //
    // ====================================================================
    // Temp Lancer Functions
    // ====================================================================
    //
    //
    // ====================================================================

    /*
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPWeapons_PlayerShip_MaybeFireWeapon_LANCER
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, ILGenerator gen)
        {
            int found = 0;
            int count = -1;

            Label lbl = gen.DefineLabel();

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 14)
                {
                    yield return code;
                    found++;
                    count = 0;
                }
                else if (found == 1)
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "c_player"));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "m_energy"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CodeStage.AntiCheat.ObscuredTypes.ObscuredFloat), "op_Implicit", new System.Type[] { typeof(CodeStage.AntiCheat.ObscuredTypes.ObscuredFloat) })); 
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    yield return new CodeInstruction(OpCodes.Ble, lbl);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.95f);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PlayerShip), "FiringVolumeModifier"));
                    found++;
                }
                else if (count >= 0 && count < 2 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Player), "PlayCameraShake"))
                {
                    yield return code;
                    count++;
                }
                else if (count == 3) // we're modifying the command immediately following the count == 2 stuff down below
                {
                    code.labels.Add(lbl);
                    yield return code;
                    count++;
                }
                else
                {
                    yield return code;
                }

                if (count == 2) // we're injecting after the second PlayCameraShake
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "c_player"));
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 24f);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Player), "UseEnergy"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "m_refire_time"));
                    //yield return new CodeInstruction(OpCodes.Ldc_R4, 3f);
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeapons), "refire"));
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PlayerShip), "m_refire_time"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons), "LancerMDSound"));
                    count++;
                }
            }
        }
    }
    */


    // ====================================================================
    //
    //
    // ====================================================================
    // Projectile Extension Functions
    // ====================================================================
    //
    //
    // ====================================================================

    // Currently the Projectile checks in OnSniperPacket are -completely- disabled. Re-write this.

    // FOR SECONDARIES - don't extend ProjectileTypeHasLaunchDataSynced(ProjPrefab type), sniper packets already negates it
    // BouncesOffShield needs converting to read from the weapon classes, or build the list at init -- worry about this later

    // FixedUpdateDynamic has code for the homing missiles to update their tracking, needs to be pulled out eventually.
    // UpdateDynamic has some insertions in OLmod. Deal with them  at the same time as FixedUpdateDynamic.

    // ProcessCollision needs separating
    // OnTriggerEnter *may* need updating?
    // OnCollisionEnter has some specific cases with secondaries and Thunderbolt to keep an eye on. Will need generalizing at some point.
    // OnCollisionExit has a specific case for the Reflex bounces if updated to homing. I wouldn't worry about this yet.

    // RotationTowardsNearbyVisibleEnemy is very specific, but may need to be patched at some point (especially if the Earthshaker is going to be a thing)

    // if single player is going to be a thing, the Robot files and the robot damage stuff in the main classes needs looking at. Otherwise, ignore.
    // There are classes in OLMod that will need to be generalized. Ignore Monsterball and Race for now.
    // CreeperSync -- some stuff still needs generalizing

    // Extends the Projectile lists in ProjectileManager to accomodate new types of Projectile prefabs
    //[HarmonyPriority(Priority.Last)]
    //[HarmonyPatch(typeof(ProjectileManager), "ReadProjPresetData")]
    // apparently this is the only reliable place I can inject for now.
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(PilotManager), "Initialize")]
    public static class MPWeapons_ProjectileManager_ReadProjPresetData
    {
        public static void Postfix()
        {
            Debug.Log("Loading additional projectile prefabs and effects...");

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMod.Resources.meshes")) // probably should have its own embedded file but for now this works
            {
                var ab = AssetBundle.LoadFromStream(stream);

                // do weapon mesh loading here as well since it's the same asset file
                foreach (Weapon w in MPWeapons.ExtWeapons)
                {
                    if (w.projMeshName != null) // if it's null, we're planning to use using the Kodachi default mesh
                    {
                        w.projMesh = Object.Instantiate(ab.LoadAsset<GameObject>(w.projMeshName));
                        Object.DontDestroyOnLoad(w.projMesh);
                        //Debug.Log("Prefab replacement mesh loaded: " + s.mesh.name);
                    }
                    for (int i = 0; i < w.extras.Length; i++)
                    {
                        w.extras[i] = Object.Instantiate(ab.LoadAsset<GameObject>(w.extraNames[i]));
                        Object.DontDestroyOnLoad(w.extras[i]);
                        //Debug.Log("Extra object loaded: " + w.extras[i].name);
                    }
                }

                ab.Unload(false);
            }

            MPWeapons.WeaponLookup = new Weapon[(int)ProjPrefabExt.num];

            // extension weapons are done first in case they reuse stock projectiles, they can get overridden this way. This should get generalized at some point to be able to load arbitrary weapons.
            foreach (Weapon weapon in MPWeapons.ExtWeapons)
            {
                MPWeapons.WeaponLookup[(int)weapon.projprefab] = weapon;

                if (weapon.GetType().IsSubclassOf(typeof(SecondaryWeapon)))
                {
                    MPWeapons.MasterSecondaries[weapon.GetType().Name] = (SecondaryWeapon)weapon;

                    if (((SecondaryWeapon)weapon).subproj != ProjPrefabExt.none)
                    {
                        MPWeapons.WeaponLookup[(int)((SecondaryWeapon)weapon).subproj] = weapon;
                    }
                }
                else
                {
                    MPWeapons.MasterPrimaries[weapon.GetType().Name] = (PrimaryWeapon)weapon;
                }
                Debug.Log("CCF added " + weapon.GetType().Name + " to master weapon dictionaries");
            }
            foreach (PrimaryWeapon weapon in MPWeapons.primaries)
            {
                MPWeapons.WeaponLookup[(int)weapon.projprefab] = weapon;
                MPWeapons.MasterPrimaries[weapon.GetType().Name] = weapon;
                Debug.Log("CCF added " + weapon.GetType().Name + " to master weapon dictionaries");
            }
            foreach (SecondaryWeapon weapon in MPWeapons.secondaries)
            {
                MPWeapons.WeaponLookup[(int)weapon.projprefab] = weapon;
                MPWeapons.MasterSecondaries[weapon.GetType().Name] = weapon;
                if (weapon.subproj != ProjPrefabExt.none)
                {
                    MPWeapons.WeaponLookup[(int)weapon.subproj] = weapon;
                }
                Debug.Log("CCF added " + weapon.GetType().Name + " to master weapon dictionaries");
            }

            System.Array.Resize(ref ProjectileManager.proj_list, (int)ProjPrefabExt.num);
            System.Array.Resize(ref ProjectileManager.proj_prefabs, (int)ProjPrefabExt.num);
            System.Array.Resize(ref ProjectileManager.proj_info, (int)ProjPrefabExt.num);

            // The particle arrays are always referenced by index in the actual methods, so enums aren't needed.

            List<GameObject> TrailRenderers = ParticleManager.psm[1].particle_prefabs.ToList();
            List<GameObject> WeaponEffects = ParticleManager.psm[2].particle_prefabs.ToList();
            List<GameObject> WeaponExplosions = ParticleManager.psm[3].particle_prefabs.ToList();

            foreach (Weapon weapon in MPWeapons.ExtWeapons)
            {
                int idx = (int)weapon.projprefab;
                GameObject prefab = weapon.GenerateProjPrefab();
                if (idx >= 29) // don't touch the stock projectile prefabs (projdata doesn't count), make a new one as a copy if you want to mess with it
                {
                    ProjectileManager.proj_list[idx] = new List<ProjElement>();
                    //ProjectileManager.proj_prefabs[idx] = weapon.GenerateProjPrefab();
                    ProjectileManager.proj_prefabs[idx] = prefab;
                    ProjectileManager.proj_info[idx] = ProjectileManager.proj_prefabs[idx].GetComponent<Projectile>();
                    ProjectileManager.proj_info[idx].DieNoExplode(false);
                    ProjectileManager.proj_prefabs[idx].SetActive(false);

                    //weapon.AddTrailRenderers(ref TrailRenderers);
                    //weapon.AddWeaponEffects(ref WeaponEffects);
                    //weapon.AddWeaponExplosions(ref WeaponExplosions);
                }
                weapon.AddTrailRenderers(ref TrailRenderers);
                weapon.AddWeaponEffects(ref WeaponEffects);
                weapon.AddWeaponExplosions(ref WeaponExplosions);
            }

            ParticleManager.psm[1].particle_prefabs = TrailRenderers.ToArray();
            System.Array.Resize(ref ParticleManager.psm[1].particle_list, TrailRenderers.Count);
            System.Array.Resize(ref ParticleManager.psm[1].particle_frame_count, TrailRenderers.Count);
            ParticleManager.psm[1].m_num_particles = TrailRenderers.Count;
            for (int i = 1; i < ParticleManager.psm[1].particle_list.Length; i++)
            {
                if (ParticleManager.psm[1].particle_list[i] == null)
                {
                    ParticleManager.psm[1].particle_list[i] = new List<ParticleElement>();
                    ParticleManager.psm[1].InitializeSlots(i, 1);
                }
                ParticleManager.psm[1].particle_frame_count[i] = 0;
            }

            ParticleManager.psm[2].particle_prefabs = WeaponEffects.ToArray();
            System.Array.Resize(ref ParticleManager.psm[2].particle_list, WeaponEffects.Count);
            System.Array.Resize(ref ParticleManager.psm[2].particle_frame_count, WeaponEffects.Count);
            ParticleManager.psm[2].m_num_particles = WeaponEffects.Count;
            for (int i = 1; i < ParticleManager.psm[2].particle_list.Length; i++)
            {
                if (ParticleManager.psm[2].particle_list[i] == null)
                {
                    ParticleManager.psm[2].particle_list[i] = new List<ParticleElement>();
                    ParticleManager.psm[2].InitializeSlots(i, 1);
                }
                ParticleManager.psm[2].particle_frame_count[i] = 0;
            }

            ParticleManager.psm[3].particle_prefabs = WeaponExplosions.ToArray();
            System.Array.Resize(ref ParticleManager.psm[3].particle_list, WeaponExplosions.Count);
            System.Array.Resize(ref ParticleManager.psm[3].particle_frame_count, WeaponExplosions.Count);
            ParticleManager.psm[3].m_num_particles = WeaponExplosions.Count;
            for (int i = 1; i < ParticleManager.psm[3].particle_list.Length; i++)
            {
                if (ParticleManager.psm[3].particle_list[i] == null)
                {
                    ParticleManager.psm[3].particle_list[i] = new List<ParticleElement>();
                    ParticleManager.psm[3].InitializeSlots(i, 1);
                }
                ParticleManager.psm[3].particle_frame_count[i] = 0;
            }

            // populate the stock item prefabs in each weapon. Only needs to be done for the stock weapons, the rest are either set or built in GenerateProjPrefab()
            for (int i = 0; i < 8; i++)
            {
                MPWeapons.primaries[i].itemPrefab = PrefabManager.item_prefabs[MPWeapons.WeaponItems[i]];
                MPWeapons.secondaries[i].itemPrefab = PrefabManager.item_prefabs[MPWeapons.MissileItems[i]];
            }

            // Stock projectile fixes
            ProjectileManager.proj_info[(int)ProjPrefab.proj_reflex].c_rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous; // WHY WAS IT DISCRETE
            ProjectileManager.proj_prefabs[(int)ProjPrefab.proj_thunderbolt].transform.GetChild(2).transform.localPosition = new Vector3(0f, 0f, 0.4f); // level collider was way too far back compared to the player collider

            MPWeapons.Initialized = true;
            MPWeapons.UpdateWeaponList();

            Debug.Log("Additional projectiles prefabs and effects loaded");
        }
    }

    [HarmonyPatch(typeof(NetworkSim), "PauseAllProjectiles")]
    public static class MPWeapons_NetworkSim_PauseAllProjectiles
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 29)
                {
                    code.operand = (sbyte)ProjPrefabExt.num;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(ProjectileManager), "DestroyAll")]
    public static class MPWeapons_ProjectileManager_DestroyAll
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 29)
                {
                    code.operand = (sbyte)ProjPrefabExt.num;
                }
                yield return code;
            }
        }
    }


    [HarmonyPatch(typeof(ProjectileManager), "FireProjectile")]
    public static class MPWeapons_ProjectileManager_FireProjectile
    {
        public static bool CheckAddDetonator(ProjPrefab proj)
        {
            Weapon weapon = MPWeapons.WeaponLookup[(int)proj];
            return weapon != null && weapon.firingMode == FiringMode.DETONATOR && (int)proj == (int)weapon.projprefab; // we don't want any possible subprojectiles getting detonators added to them
        }

        // There are checks for subprojectiles in here. However, they are bypassed if Sniper Packets is on, and if they're not, this thing isn't going to work anyways, so they're not dealt with.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int step = 0;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Server), "SendProjectileFiredToClients"))
                {
                    step = 1;
                    yield return code;
                }
                else if (step == 1) // got some labels to deal with
                {
                    //if (type == ProjPrefab.missile_devastator) -->>  if (CheckAddDetonator(type))
                    step = 2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_ProjectileManager_FireProjectile), "CheckAddDetonator"));
                }
                else if (step == 2) // skip an instruction
                {
                    step = 3;
                }
                else if (step == 3) // code.opcode == OpCodes.Bne_Un, change it to a false check
                {
                    code.opcode = OpCodes.Brfalse;
                    yield return code;
                    step = 4;
                }
                else
                {
                    yield return code;
                }
            }
        }
    }


    [HarmonyPatch(typeof(Projectile), "Fire")]
    public static class MPWeapons_Projectile_Fire
    {
        public static bool IntermediateFire(ProjPrefab prefab, Projectile proj, Vector3 pos, Quaternion rot, ref int m_bounces, ref float m_damage, ref FXWeaponExplosion m_death_particle_override, ref float m_init_speed, ref float m_lifetime, ref float m_homing_cur_strength, ref float m_push_force, ref float m_push_torque, ref WeaponUnlock m_upgrade, bool save_pos, ref float m_strength, ref float m_vel_inherit)
        {
            RigidbodyInterpolation ri = RigidbodyInterpolation.Interpolate;
            bool bigParticles = false;

            // This will need revising if robot weapons come into play at some point. Right now it contains projectile customizations for the non-player weapons and the flare, since they don't have Weapon classes of their own.
            switch (prefab)
            {
                case ProjPrefab.none:
                case (ProjPrefab)ProjPrefabExt.num: 
                case ProjPrefab.proj_melee:
                    break;
                case ProjPrefab.missile_alien_pod:
                    if (m_upgrade <= WeaponUnlock.LEVEL_0)
                    {
                        m_init_speed *= 1.25f;
                        proj.m_homing_strength = 0f;
                    }
                    else if (m_upgrade == WeaponUnlock.LEVEL_2A)
                    {
                        m_lifetime = UnityEngine.Random.Range(0.12f, 0.2f);
                        m_homing_cur_strength = 0f;
                        proj.m_death_sfx = SFXCue.none;
                        proj.m_firing_sfx = SFXCue.none;
                    }
                    break;
                case ProjPrefab.proj_enemy_core:
                    if (m_upgrade >= WeaponUnlock.LEVEL_1)
                    {
                        m_init_speed *= 1.25f;
                    }
                    break;
                case ProjPrefab.proj_enemy_blaster:
                    proj.c_mesh_transform.Rotate(Vector3.up, UnityEngine.Random.Range(0f, 360f), Space.Self);
                    if (m_upgrade >= WeaponUnlock.LEVEL_1)
                    {
                        m_init_speed *= 1.25f;
                    }
                    break;
                case ProjPrefab.proj_enemy_vulcan:
                    if (m_upgrade >= WeaponUnlock.LEVEL_1)
                    {
                        m_init_speed *= 1.15f;
                    }
                    break;
                case ProjPrefab.proj_alien_vulcan:
                case ProjPrefab.proj_alien_blaster:
                    break;
                case ProjPrefab.proj_flare:
                case ProjPrefab.proj_flare_sticky:
                    if (proj.m_type == ProjPrefab.proj_flare_sticky)
                    {
                        proj.c_rigidbody.isKinematic = false;
                        Projectile.StickyFlareCount++;
                        if (Projectile.StickyFlareCount > 10)
                        {
                            ProjectileManager.KillOldestStickyFlare();
                        }
                    }
                    if (GameplayManager.IsMultiplayerActive)
                    {
                        m_init_speed += 3f;
                    }
                    if ((bool)proj.m_owner_player && proj.m_owner_player.c_player_ship.m_boosting)
                    {
                        m_init_speed += 10f;
                    }
                    break;
                default:
                    Weapon weapon = MPWeapons.WeaponLookup[(int)prefab];
                    bigParticles = weapon.ProjectileFire(proj, pos, rot, ref m_bounces, ref m_damage, ref m_death_particle_override, ref m_init_speed, ref m_lifetime, ref m_homing_cur_strength, ref m_push_force, ref m_push_torque, ref m_upgrade, save_pos, ref m_strength, ref m_vel_inherit);
                    ri = weapon.Interpolation(proj);
                    break;
            }

            proj.c_rigidbody.interpolation = ri;
            proj.c_rigidbody.velocity = proj.c_transform.forward * m_init_speed;

            if (GameplayManager.IsMultiplayerActive)
            {
                proj.m_homing_strength *= 0.9f;
                proj.m_team = ProjTeam.ENEMY;
            }
            //else
            //{
            if (m_vel_inherit > 0f)
            {
                //Rigidbody componentInChildren = proj.m_owner.GetComponentInChildren<Rigidbody>();
                Rigidbody playerRB = proj.m_owner_player.c_player_ship.c_rigidbody;
                Vector3 velocity = playerRB.velocity;
                proj.c_rigidbody.AddForce(proj.c_rigidbody.mass * m_vel_inherit / RUtility.FRAMETIME_FIXED * velocity);
                //Debug.Log("CCF vel_inherit triggered, original velocity: " + velocity + ", new velocity " + playerRB.velocity);

            }
            //}

            return bigParticles;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool found = false;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stloc_3)
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_bounces"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_damage"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_death_particle_override"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_init_speed"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_lifetime"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_homing_cur_strength"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_push_force"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_push_torque"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_upgrade"));
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 7); // method argument save_pos
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_strength"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_vel_inherit"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_Projectile_Fire), "IntermediateFire"));
                    yield return new CodeInstruction(OpCodes.Stloc_2);
                }
                else if (found) // skip the entire switch statement
                {
                    if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Overload.NetworkManager), "IsHeadless")) // there *are* some specific cases after this... but I don't care about them. They're too specific.
                    {
                        found = false;
                        yield return code;
                    }
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
    

    // Self-splash was WIMPY. Fixed. 75% of what you do to your enemies now, at 75% the radius (or the originals, if they were higher than that). This is a starting point only.
    [HarmonyPatch(typeof(ExplosionDelayed), "CopyData")]
    public static class MPWeapons_ExplosionDelayed_CopyData
    {
        public static void Postfix(ExplosionDelayed __instance)
        {
            if (MPShips.allowed != 0)
            {
                __instance.m_damage_radius_player = Mathf.Max(__instance.m_damage_radius_player, __instance.m_damage_radius_mp * 0.75f);
                __instance.m_player_damage = Mathf.Max(__instance.m_player_damage, __instance.m_mp_damage * 0.75f);
            }
        }
    }


    //[HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Projectile), "Explode")]
    public static class MPWeapons_Projectile_Explode
    {
        public static bool Prefix(bool damaged_something, Projectile __instance, ProjElement ___m_proj_element, ParticleElement ___m_trail_effect_pe, ParticleElement ___m_trail_renderer_pe, float ___m_strength, FXWeaponExplosion ___m_death_particle_override, WeaponUnlock ___m_upgrade)
        {
            //bool skipping = false;
            /*
            if (!GameplayManager.IsMultiplayerActive || !MPCreeperSync.ExplodeSync.Contains((ProjPrefabExt)__instance.m_type) || __instance.m_projectile_id == -1 || __instance.RemainingLifetime() < -4f)
            {
                Debug.Log("CCF Skipping sync - MPActive " + GameplayManager.IsMultiplayerActive + " ExplodeSync " + MPCreeperSync.ExplodeSync.Contains((ProjPrefabExt)___m_type) + " ID " + (__instance.m_projectile_id == -1) + " lifetime " + (__instance.RemainingLifetime() < -4f));
                //return true;
            }
            else
            {
                if (!Server.IsActive() && !CreeperSyncExplode.m_allow_explosions) // ignore explosions on client if creeper-sync active
                {
                    //return CreeperSyncExplode.m_allow_explosions;
                    return false;
                }
                var msg = new ExplodeMsg();
                msg.m_id = __instance.m_projectile_id;
                msg.m_pos = __instance.c_transform.position;
                msg.m_damaged_something = damaged_something;
                foreach (var conn in UnityEngine.Networking.NetworkServer.connections)
                    if (conn != null && MPTweaks.ClientHasNetVersion(conn.connectionId, MPCreeperSync.NET_VERSION_CREEPER_SYNC))
                    {
                        UnityEngine.Networking.NetworkServer.SendToClient(conn.connectionId, MessageTypes.MsgExplode, msg);
                    }

                Debug.Log("CCF sending explode projectile index " + msg.m_id + ", time is " + Time.time + " -- are we server? " + GameplayManager.IsDedicatedServer());
                //return true;
            }
            */

            //Debug.Log("CCF IS THIS THING FIRING??? " + Time.time);
            if (GameplayManager.IsMultiplayerActive && MPCreeperSync.ExplodeSync.Contains((ProjPrefabExt)__instance.m_type) && __instance.m_projectile_id != -1 && __instance.RemainingLifetime() > -4f)
            {
                //Debug.Log("CCF Skipping sync - MPActive " + GameplayManager.IsMultiplayerActive + " ExplodeSync " + MPCreeperSync.ExplodeSync.Contains((ProjPrefabExt)__instance.m_type) + " ID " + (__instance.m_projectile_id == -1) + " lifetime " + (__instance.RemainingLifetime() < -4f));
                if (!Server.IsActive() && !CreeperSyncExplode.m_allow_explosions) // ignore explosions on client if creeper-sync active
                {
                    //return CreeperSyncExplode.m_allow_explosions;
                    return false;
                }
                var msg = new ExplodeMsg();
                msg.m_id = __instance.m_projectile_id;
                msg.m_pos = __instance.c_transform.position;
                msg.m_damaged_something = damaged_something;
                foreach (var conn in UnityEngine.Networking.NetworkServer.connections)
                    if (conn != null && MPTweaks.ClientHasNetVersion(conn.connectionId, MPCreeperSync.NET_VERSION_CREEPER_SYNC))
                    {
                        UnityEngine.Networking.NetworkServer.SendToClient(conn.connectionId, MessageTypes.MsgExplode, msg);
                    }

                //Debug.Log("CCF sending explode projectile index " + msg.m_id + ", time is " + Time.time);
            }
            //Debug.Log("CCF Skipping sync - MPActive " + GameplayManager.IsMultiplayerActive + " ExplodeSync " + MPCreeperSync.ExplodeSync.Contains((ProjPrefabExt)__instance.m_type) + " ID " + (__instance.m_projectile_id == -1) + " lifetime " + (__instance.RemainingLifetime() < -4f));

            __instance.m_alive = false;
            if (__instance.m_death_sfx != 0)
            {
                SFXCueManager.PlayCuePos(__instance.m_death_sfx, __instance.c_transform.localPosition, __instance.m_death_sound_volume, UnityEngine.Random.Range(-0.1f, 0.1f));
            }
            if (___m_trail_effect_pe != null)
            {
                ___m_trail_effect_pe.DelayedDestroy(__instance.m_trail_post_lifetime, detach: true);
            }
            if (___m_trail_renderer_pe != null)
            {
                ___m_trail_renderer_pe.DelayedDestroy(__instance.m_trail_post_lifetime, detach: true);
            }
            if (GameplayManager.IsMultiplayerActive && __instance.m_collider_to_ignore != null)
            {
                Physics.IgnoreCollision(__instance.c_collider, __instance.m_collider_to_ignore, ignore: false);
            }

            Weapon weapon = MPWeapons.WeaponLookup[(int)__instance.m_type];

            if (weapon != null)
            {
                //Debug.Log("CCF Calling Explode");
                weapon.Explode(__instance, damaged_something, ___m_death_particle_override, ___m_strength, ___m_upgrade);
            }
            ___m_proj_element.Destroy();
            return false;
        }
    }


    [HarmonyPatch(typeof(Projectile), "FindATarget")]
    public static class MPWeapons_Projectile_FindATarget
    {
        public static bool UseMineHoming(Projectile proj)
        {
            return MPWeapons.WeaponLookup[(int)proj.m_type].MineHoming;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool found = false;

            foreach (var code in codes)
            {
                if (!found && code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(Projectile), "m_type"))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_Projectile_FindATarget), "UseMineHoming"));
                    found = true;
                }
                else if (found)
                {
                    if (code.opcode == OpCodes.Beq)
                    {
                        code.opcode = OpCodes.Brtrue;
                        yield return code;
                    }
                    else if (code.opcode == OpCodes.Bne_Un)
                    {
                        code.opcode = OpCodes.Br;
                        found = false;
                        yield return code;
                    }
                }
                else
                {
                    yield return code;
                }
            }
        }
    }


    [HarmonyPatch(typeof(Projectile), "ProcessCollision")]
    public static class MPWeapons_Projectile_ProcessCollision
    {
        public static bool ProcessCollisionHandoff(/*ProjPrefab m_type,*/ Projectile proj, GameObject collider, Vector3 collision_normal, int layer, ref bool m_bounce_allow, ref int m_bounces, ref Transform m_cur_target, ref Player m_cur_target_player, ref Robot m_cur_target_robot, ref float m_damage, ref float m_lifetime, ref float m_target_timer, ParticleElement m_trail_effect_pe)
        {
            Weapon weapon = MPWeapons.WeaponLookup[(int)proj.m_type];
            if (weapon != null)
            {
                if (proj.m_bounce_behavior == BounceBehavior.none)  // hey look the field does something now
                {
                    m_bounce_allow = false; // defaults to true, updated every frame
                }
                else
                {
                    m_bounces++; // this needs to be incremented before ProcessCollision() fires for the weapon in case they need to know what bounce they're on.
                }

                switch (proj.m_type)
                {
                    case ProjPrefab.proj_flare:
                        break;
                    case ProjPrefab.proj_flare_sticky:
                        if (!proj.is_attached && layer == 14 && proj.c_rigidbody != null)
                        {
                            proj.is_attached = true;
                            proj.c_rigidbody.isKinematic = true;
                            proj.c_transform.parent = collider.transform;
                            ParticleManager.psm[3].StartParticleInstant(1, proj.c_transform.localPosition, proj.c_transform.rotation);
                            SFXCueManager.PlayRawSoundEffectPos(SoundEffect.wep_driller_fire_low3_r2, proj.c_transform.localPosition, 0.4f, UnityEngine.Random.Range(-0.3f, -0.2f));
                        }
                        break;
                    default:
                        weapon.ProcessCollision(proj, collider, collision_normal, layer, ref m_bounce_allow, ref m_bounces, ref m_cur_target, ref m_cur_target_player, ref m_cur_target_robot, ref m_damage, ref m_lifetime, ref m_target_timer, m_trail_effect_pe);
                        break;
                }

                if (m_bounce_allow && proj.m_alive)
                {
                    if (m_bounces > proj.m_bounce_max_count) // how about we actually respect this number :P
                    {
                        proj.Explode();
                    }
                    else
                    {
                        if (weapon.bounceFX != FXWeaponEffect.none)
                        {
                            ParticleManager.psm[2].StartParticleInstant((int)weapon.bounceFX, proj.c_transform.localPosition - collision_normal * 0.1f, Quaternion.FromToRotation(Vector3.forward, collision_normal));
                        }
                        if (weapon.bounceExp != FXWeaponExplosion.none)
                        {
                            ParticleManager.psm[3].StartParticleInstant((int)weapon.bounceExp, proj.c_transform.localPosition, proj.c_transform.rotation);
                        }

                    }
                    m_bounce_allow = false;
                }
                return true;
            }
            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, ILGenerator gen) // THIS IS BROKEN AND I DON'T KNOW WHY
        {
            bool found = false;
            Label label = gen.DefineLabel();

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 6)
                {
                    found = true;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // this Projectile
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // GameObject collider
                    yield return new CodeInstruction(OpCodes.Ldarg_2); // Vector3 collision_normal
                    yield return new CodeInstruction(OpCodes.Ldloc_2); // int layer
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_bounce_allow"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_bounces"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_cur_target"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_cur_target_player"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_cur_target_robot"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_damage"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_lifetime"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(Projectile), "m_target_timer"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), "m_trail_effect_pe"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons_Projectile_ProcessCollision), "ProcessCollisionHandoff"));
                    yield return new CodeInstruction(OpCodes.Brfalse, label);
                    yield return new CodeInstruction(OpCodes.Ret); // skip the rest unless weapon was null above for some reason
                }
                else if (found)
                {
                    code.labels.Add(label);
                    yield return code;
                    found = false;
                }
                else if (!found)
                {
                    yield return code;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Item), "Spew")]
    public static class MPWeapons_Item_Spew
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool found = false;

            foreach (var code in codes)
            {
                if (!found && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Overload.NetworkManager), "IsServer"))
                {
                    found = true;
                    //CodeInstruction newcode = new CodeInstruction(OpCodes.Ldarg_0);
                    CodeInstruction newcode = new CodeInstruction(OpCodes.Ldloc_0);
                    code.MoveLabelsTo(newcode);
                    yield return newcode;
                    //yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameObject), "get_gameObject"));
                    //yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Item), "c_go"));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), "SetActive")); // Most of the time this doesn't matter. With faked prefabs, it does.
                }
                yield return code;
            }
        }
    }
}

