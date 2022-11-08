using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameMod.Metadata;
using GameMod.Objects.Models;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    [Mod(Mods.PrimarySpawns)]
    public static class PrimarySpawns {
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
        public static int GetMaxPrimaries() {
            var count = NetworkMatch.m_players.Count;
            return (int)(Mathf.Lerp(RobotManager.m_multi_weapon_count2, RobotManager.m_multi_weapon_count8 + (RobotManager.m_multi_weapon_count8 - RobotManager.m_multi_weapon_count2) * (4f / 3f), (count - 2f) / 14f) + 0.5f);
        }

        /// <summary>
        /// Determine if Lancer should spawn.
        /// </summary>
        /// <returns></returns>
        public static bool SpawnLancer() {
            var lancer = Budget.Find(b => b.Type == WeaponType.LANCER);

            // If lancer is not even in the budget, return false.
            if (lancer == null) {
                return false;
            }

            // If we've set LancerPlayers, use that to determine if it should spawn.
            if (LancerPlayers > 0) {
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
        public static void SpawnPrimary() {
            var max = GetMaxPrimaries();

            // Do not spawn a primary if we're at or above max.
            var totalActive = Budget.Sum(b => b.Active);
            if (totalActive >= max) {
                return;
            }

            // Filter out lancer if we shouldn't spawn it.
            var spawnLancer = SpawnLancer();
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
            foreach (var weapon in filteredBudget) {
                if (weapon.Remaining <= 0) {
                    continue;
                }

                current += weapon.Remaining;
                if (random <= current) {
                    spawn = weapon;
                    break;
                }
            }

            // If we didn't find anything to spawn, bail!  Can happen if all of the level's primaries are disallowed on the server.
            if (spawn == null) {
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
}
