using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    /// <summary>
    /// Represents a budget for a single weapon type.
    /// </summary>
    public class PrimaryBudget
    {
        /// <summary>
        /// The weapon type.
        /// </summary>
        public WeaponType Type { get; set; }

        /// <summary>
        /// The amount of max budget.  Defined by the map maker.
        /// </summary>
        public float Budget { get; set; }

        /// <summary>
        /// The remaining budget.
        /// </summary>
        public float Remaining { get; set; }

        /// <summary>
        /// The number of active weapons of this type currently in play.
        /// </summary>
        public int Active { get; set; }
    }

    public static class MPPrimaries
    {
        /// <summary>
        /// The active game's primary budget.
        /// </summary>
        public static List<PrimaryBudget> Budget { get; set; } = new List<PrimaryBudget>();

        /// <summary>
        /// The amount of time until the next primary should spawn.
        /// </summary>
        public static float SpawnWeaponTimer { get; set; }

        /// <summary>
        /// The minimum number of players required for the lancer to spawn.
        /// </summary>
        public static int LancerPlayers { get; set; }

        /// <summary>
        /// Gets the max number of primaries that can spawn in at any given time.
        /// </summary>
        /// <returns></returns>
        public static int GetMaxPrimaries()
        {
            var count = NetworkMatch.m_players.Count;
            return (int)(Mathf.Lerp(RobotManager.m_multi_weapon_count2, RobotManager.m_multi_weapon_count8 + (RobotManager.m_multi_weapon_count8 - RobotManager.m_multi_weapon_count2) * (4f/3f), (count - 2f) / 14f) + 0.5f);
        }

        public static string GetBudgetString()
        {
            var ret = "";
            foreach (var weapon in Budget)
            {
                ret = $"{ret}{weapon.Type.ToString().Substring(0, 2)} {weapon.Remaining:N2}/{weapon.Budget:N2} {weapon.Active} - ";
            }
            return ret;
        }

        /// <summary>
        /// Determine if Lancer should spawn.
        /// </summary>
        /// <returns></returns>
        public static bool SpawnLancer()
        {
            var lancer = Budget.Find(b => b.Type == WeaponType.LANCER);

            // If lancer is not even in the budget, return false.
            if (lancer == null)
            {
                return false;
            }

            // If we've set LancerPlayers, use that to determine if it should spawn.
            if (LancerPlayers > 0)
            {
                return NetworkMatch.m_players.Count >= LancerPlayers;
            }

            // Otherwise, use the old spawning algorithm (simplified via Linq) to determine whether to spawn the lancer.
            var max = GetMaxPrimaries();

            var weaponsHigher = Budget.Where(b => b.Budget > lancer.Budget).Sum(b => (int)Mathf.Ceil(b.Budget - lancer.Budget));

            return max >= weaponsHigher + 1;
        }

        /// <summary>
        /// Spawns a primary at random.
        /// </summary>
        public static void SpawnPrimary()
        {
            var max = GetMaxPrimaries();

            // Do not spawn a primary if we're at or above max.
            var totalActive = Budget.Sum(b => b.Active);
            if (totalActive >= max)
            {
                return;
            }

            var spawnLancer = true;
            if (MPShips.allowed != 0)
            {
                // Filter out lancer if we shouldn't spawn it.
                spawnLancer = SpawnLancer();
                
            }
            var filteredBudget = Budget.Where(b => b.Type != WeaponType.LANCER || spawnLancer);

            // Get the total budget and the total remaining.
            var totalBudget = filteredBudget.Sum(b => b.Budget);
            var totalRemaining = filteredBudget.Sum(b => b.Remaining);

            if (totalRemaining == 0) {
                return;
            }

            // Pick a random number from 0 to totalRemaining.
            var random = UnityEngine.Random.Range(0f, totalRemaining);

            // Loop through the budget and pick out one random weapon from the budget.
            var current = 0f;
            PrimaryBudget spawn = null;
            foreach (var weapon in filteredBudget)
            {
                if (weapon.Remaining <= 0) {
                    continue;
                }

                current += weapon.Remaining;
                if (random <= current)
                {
                    spawn = weapon;
                    break;
                }
            }

            // If we didn't find anything to spawn, bail!  Can happen if all of the level's primaries are disallowed on the server.
            if (spawn == null)
            {
                return;
            }

            // Spawn the weapon.
            ItemPrefab item = ChallengeManager.WeaponTypeToPrefab(spawn.Type);

            // NetworkMatch.SpawnItem(item, false);
            AccessTools.Method(typeof(NetworkMatch), "SpawnItem").Invoke(null, new object[] { item, false });

            // Increment the active count and decrement remaining budget accordingly.
            spawn.Active++;
            spawn.Remaining = Mathf.Max(0f, spawn.Remaining - max / totalBudget);

            // Reset weapon spawn timer.
            if (MPClassic.matchEnabled) {
                SpawnWeaponTimer = UnityEngine.Random.Range(30f / max, 60f / max);
            } else {
                SpawnWeaponTimer = UnityEngine.Random.Range(240f / max, 480f / max);
            }
        }
    }

    /// <summary>
    /// Override the original PowerupLevelStart to use a primary budget.
    /// </summary>
    [HarmonyPatch(typeof(NetworkMatch), "PowerupLevelStart")]
    public class MPPrimaries_NetworkMatch_PowerupLevelStart
    {
        public static bool Prefix(ref int[] ___m_spawn_weapon_count)
        {
            // Setup the primary budget.
            MPPrimaries.Budget.Clear();
            foreach (var weapon in RobotManager.m_multiplayer_spawnable_weapons)
            {
                var weaponType = (WeaponType)weapon.type;
                if (NetworkMatch.IsWeaponAllowed(weaponType))
                {
                    MPPrimaries.Budget.Add(new PrimaryBudget
                    {
                        Type = weaponType,
                        Budget = weapon.percent,
                        Remaining = weapon.percent,
                        Active = 0
                    });
                }
            }

            // Spawn in initial primaries.
            var primaries = MPPrimaries.GetMaxPrimaries();

            for (int i = 0; i < primaries; i++)
            {
                MPPrimaries.SpawnPrimary();
            }

            // This is the rest of the PowerupLevelStart code, currently unmodified.
            var num = RobotManager.m_multi_missile_count;
            for (int n = 0; n < num; n++)
            {
                MissileType missileType = NetworkMatch.RandomAllowedMissileSpawn();
                if (missileType != MissileType.NUM)
                {
                    ItemPrefab item2 = ChallengeManager.MissileTypeToPrefab(missileType);
                    // NetworkMatch.SpawnItem(item2, false);
                    AccessTools.Method(typeof(NetworkMatch), "SpawnItem").Invoke(null, new object[] { item2, false });
                }
            }
            NetworkMatch.SetSpawnMissileTimer();
            NetworkMatch.SetSpawnSuperTimer();
            NetworkMatch.SetSpawnBasicTimer();

            // Get rid of weapon counts so they aren't triggered by the original function.
            for (var i = 0; i < 8; i++) {
                ___m_spawn_weapon_count[i] = 0;
            }

            // Short circuit the original code.
            return false;
        }
    }

    /// <summary>
    /// Refresh the weapon spawn budget with the specified type.
    /// </summary>
    [HarmonyPatch(typeof(NetworkMatch), "AddWeaponSpawn")]
    public class MPPrimaries_NetworkMatch_AddWeaponSpawn
    {
        public static bool Prefix(WeaponType wt)
        {
            // Get the primary from the budget list, bail if there isn't one.
            var primary = MPPrimaries.Budget.Find(b => b.Type == wt);
            if (primary == null)
            {
                return false;
            }

            // Bail if there are none active.
            if (primary.Active == 0)
            {
                return false;
            }

            // Restore some remaining budget, decrement the active count.
            primary.Remaining += (primary.Budget - primary.Remaining) / primary.Active;
            primary.Active--;

            // Set timer if it's less than 0.
            if (MPPrimaries.SpawnWeaponTimer <= 0)
            {
                if (MPClassic.matchEnabled) {
                    MPPrimaries.SpawnWeaponTimer = UnityEngine.Random.Range(15f, 30f);
                } else {
                    MPPrimaries.SpawnWeaponTimer = UnityEngine.Random.Range(60f, 120f);
                }
            }

            // Short circuit the original code.
            return false;
        }
    }

    /// <summary>
    /// Try to spawn a primary if the timer is ready for it.
    /// </summary>
    [HarmonyPatch(typeof(NetworkMatch), "MaybeSpawnPowerup")]
    public class MPPrimaries_NetworkMatch_MaybeSpawnPowerup
    {
        public static void Prefix()
        {
            MPPrimaries.SpawnWeaponTimer -= RUtility.FRAMETIME_GAME;
            if (MPPrimaries.SpawnWeaponTimer <= 0f)
            {
                MPPrimaries.SpawnPrimary();
            }
        }
    }

    /// <summary>
    /// Initialize LancerPlayers to 0 before reading the multiplayer mode file.
    /// </summary>
    [HarmonyPatch(typeof(RobotManager), "ReadMultiplayerModeFile")]
    public class MPPrimaries_RobotManager_ReadMultiplayerModeFile
    {
        public static void Prefix()
        {
            MPPrimaries.LancerPlayers = 0;
        }
    }

    /// <summary>
    /// Set LancerPlayers if it's included in the multiplayer mode file.
    /// </summary>
    [HarmonyPatch(typeof(RobotManager), "ParseTagMultiplayer")]
    public class MPPrimaries_RobotManager_ParseTagMultiplayer
    {
        public static void Prefix(string[] words)
        {
            string text = words[0];
            switch (text)
            {
                case "$lancer_players":
                    try
                    {
                        if (words.Length == 2)
                        {
                            MPPrimaries.LancerPlayers = (int)words[1].ToFloat();
                        }
                        else
                        {
                            Debug.Log("No count set for lancer players, ignoring.  Format is, for example, \"$lancer_players;4\"");
                        }
                    }
                    catch (Exception)
                    {
                        Debug.Log("Error setting $lancer_players.  Format is, for example, \"$lancer_players;4\"");
                    }
                    break;
            }
        }
    }
}
