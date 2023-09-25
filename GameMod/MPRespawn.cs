using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class MPRespawn {
        public static bool EnableItemSpawnsAsPlayerSpawns = false;

        public struct ItemSpawnPoint {
            public int index;
            public LevelData.SpawnPoint spawnPoint;

            public ItemSpawnPoint(int ix, Quaternion orientation) {
                index = ix;
                spawnPoint = GameManager.m_level_data.m_item_spawn_points[ix];
                spawnPoint.orientation = orientation;
            }
        }

        public static List<ItemSpawnPoint> spawnPointsFromItems = new List<ItemSpawnPoint>();
        public static Dictionary<LevelData.SpawnPoint, Dictionary<LevelData.SpawnPoint, float>> spawnPointDistances = new Dictionary<LevelData.SpawnPoint, Dictionary<LevelData.SpawnPoint, float>>();
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPRespawn_InitBeforeEachMatch {
        private static void Prefix() {
            MPRespawn.spawnPointsFromItems.Clear();
            MPRespawn.spawnPointDistances.Clear();
            MPSpawnExtension.ResetForNewLevel();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ProcessPregame")]
    class MPRespawn_ProcessPregame {
        public static readonly int layerMask = (int)Math.Pow((int)UnityObjectLayers.LEVEL, 2) + (int)Math.Pow((int)UnityObjectLayers.DOOR, 2) + (int)Math.Pow((int)UnityObjectLayers.LAVA, 2);

        private static void Prefix() {
            // Check mode, bail if not Anarchy or Team Anarchy.
            var mode = NetworkMatch.GetMode();
            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return;
            }

            // Bail if we have spawn points.
            if (MPRespawn.spawnPointDistances.Count > 0) {
                return;
            }

            // If enabled, get all of the item spawn points and check to see if they can be reused as player spawn points.
            if (MPRespawn.EnableItemSpawnsAsPlayerSpawns) {
                for (int i = 0; i < GameManager.m_level_data.m_item_spawn_points.Length; i++) {
                    // TODO: Figure out how to bail when supers are involved.
                    // if (spawn point is super) {
                    //     continue;
                    // }

                    var itemSpawnPoint = GameManager.m_level_data.m_item_spawn_points[i];

                    float largestDistance = 0f;
                    Quaternion largestQuaternion = Quaternion.identity;

                    for (float angle = 0f; angle < 360f; angle += 15f) {
                        var quaternion = Quaternion.Euler(0, angle, 0);

                        if (Physics.Raycast(itemSpawnPoint.position, quaternion * Vector3.forward, out RaycastHit hitInfo, 9999f)) {
                            var distance = hitInfo.distance;
                            if (distance > largestDistance) {
                                largestDistance = distance;
                                largestQuaternion = quaternion;
                            }
                        }
                    }

                    if (largestDistance > 20f) {
                        MPRespawn.spawnPointsFromItems.Add(new MPRespawn.ItemSpawnPoint(i, largestQuaternion));
                    }
                }
            }

            // Generate a lookup of the distance between spawn points.
            var spawnPoints = new List<LevelData.SpawnPoint>();
            spawnPoints.AddRange(MPRespawn.spawnPointsFromItems.Select(s => s.spawnPoint));
            spawnPoints.AddRange(GameManager.m_level_data.m_player_spawn_points);

            foreach (var spawn1 in spawnPoints) {
                var position = spawn1.position;
                var segnum = GameManager.m_level_data.FindSegmentContainingWorldPosition(position);
                MPRespawn.spawnPointDistances.Add(spawn1, new Dictionary<LevelData.SpawnPoint, float>());
                foreach (var spawn2 in spawnPoints) {
                    if (spawn1 == spawn2) {
                        continue;
                    }

                    var position2 = spawn2.position;
                    var segnum2 = GameManager.m_level_data.FindSegmentContainingWorldPosition(position2);

                    var distance = RUtility.FindVec3Distance(position - position2);

                    MPRespawn.spawnPointDistances[spawn1].Add(spawn2, distance);
                }
            }
        }
    }

    [HarmonyPatch(typeof(NetworkSpawnPoints), "ChooseSpawnPoint")]
    class MPRespawn_ChooseSpawnPoint {
        private static FieldInfo _NetworkSpawnPoints_m_player_pos_Field = typeof(NetworkSpawnPoints).GetField("m_player_pos", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _NetworkSpawnPoints_m_player_team_Field = typeof(NetworkSpawnPoints).GetField("m_player_team", BindingFlags.NonPublic | BindingFlags.Static);
        private static MethodInfo _NetworkSpawnPoints_GetRespawnPointCandidates_Method = AccessTools.Method(typeof(NetworkSpawnPoints), "GetRespawnPointCandidates");
        private static MethodInfo _NetworkSpawnPoints_GetRandomRespawnPointWithoutFiltering_Method = AccessTools.Method(typeof(NetworkSpawnPoints), "GetRandomRespawnPointWithoutFiltering");

        private static bool Prefix(MpTeam team, ref LevelData.SpawnPoint __result) {
            // Check mode, bail if not Anarchy or Team Anarchy.
            var mode = NetworkMatch.GetMode();
            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return true;
            }

            var respawnPointCandidates = GetRespawnPointCandidates(team);

            if (respawnPointCandidates.Count == 0) {
                __result = (LevelData.SpawnPoint)_NetworkSpawnPoints_GetRandomRespawnPointWithoutFiltering_Method.Invoke(null, new object[] { });
            } else if (NetworkManager.m_Players.Count == 0) {
                __result = respawnPointCandidates[UnityEngine.Random.Range(0, respawnPointCandidates.Count)];
            } else {
                var scores = GetRespawnPointScores(team, respawnPointCandidates, true);
                __result = scores.OrderByDescending(s => s.Value).First().Key;
            }

            lastRespawn[__result] = DateTime.Now;
            return false;
        }

        public static List<LevelData.SpawnPoint> GetRespawnPointCandidates(MpTeam team) {
            var respawnPointCandidates = (
                from s in (List<int>)_NetworkSpawnPoints_GetRespawnPointCandidates_Method.Invoke(null, new object[] { team })
                select GameManager.m_level_data.m_player_spawn_points[s]
            ).ToList();

            IEnumerable<LevelData.SpawnPoint> validItemSpawnPoints;
            if (NetworkManager.IsServer()) {
                validItemSpawnPoints = MPRespawn.spawnPointsFromItems.Where(i => Item.HasLiveItem == null || i.index > Item.HasLiveItem.Length || !Item.HasLiveItem[i.index]).Select(i => i.spawnPoint);
            } else {
                validItemSpawnPoints = MPRespawn.spawnPointsFromItems.Where(i => !Item.m_ItemList.Exists(it => Math.Abs(it.transform.position.x - i.spawnPoint.position.x) < 0.1f && Math.Abs(it.transform.position.y - i.spawnPoint.position.y) < 0.1f && Math.Abs(it.transform.position.z - i.spawnPoint.position.z) < 0.1f)).Select(i => i.spawnPoint);
            }

            respawnPointCandidates.AddRange(validItemSpawnPoints);

            return respawnPointCandidates;
        }

        private static Dictionary<LevelData.SpawnPoint, DateTime> lastRespawn = new Dictionary<LevelData.SpawnPoint, DateTime>();

        internal static List<Quaternion> m_player_rot = new List<Quaternion>();

        public static Dictionary<LevelData.SpawnPoint, float> GetRespawnPointScores(MpTeam team, List<LevelData.SpawnPoint> candidates, bool randomness = false) {
            var m_player_pos = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);
            var m_player_team = (List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null);

            m_player_pos.Clear();
            m_player_team.Clear();
            for (int i = 0; i < NetworkManager.m_Players.Count; i++) {
                if ((float)NetworkManager.m_Players[i].m_hitpoints > 0f && !NetworkManager.m_Players[i].m_spectator) {
                    m_player_pos.Add(NetworkManager.m_Players[i].c_player_ship.c_transform.position);
                    m_player_team.Add(NetworkManager.m_Players[i].m_mp_team);
                }
            }

            m_player_rot.Clear();

            for (int i = 0; i < NetworkManager.m_Players.Count; i++) {
                if ((float)NetworkManager.m_Players[i].m_hitpoints > 0f && !NetworkManager.m_Players[i].m_spectator) {
                    m_player_rot.Add(NetworkManager.m_Players[i].c_player_ship.c_transform.rotation);
                }
            }

            var distances = new Dictionary<LevelData.SpawnPoint, List<float>>();

            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);

            var max = float.MinValue;

            for (int i = 0; i < candidates.Count; i++) {
                var dist = new List<float>();
                var position = candidates[i].position;
                for (int j = 0; j < playerPositions.Count; j++) {
                    var distance = RUtility.FindVec3Distance(position - playerPositions[j]);

                    if (distance > max) {
                        max = distance;
                    }

                    dist.Add(distance);
                }

                for (int j = 0; j < candidates.Count; j++) {
                    if (i == j) {
                        continue;
                    }

                    var distance = MPRespawn.spawnPointDistances[candidates[i]][candidates[j]];

                    if (distance > max) {
                        max = distance;
                    }
                }
                distances.Add(candidates[i], dist);
            }

            var scores = new Dictionary<LevelData.SpawnPoint, float>();

            for (int i = 0; i < candidates.Count; i++) {
                var respawnPointScore = GetRespawnPointScore(team, candidates[i], distances[candidates[i]], max);

                if (randomness) {
                    respawnPointScore *= UnityEngine.Random.Range(0.95f, 1.05f);
                }

                scores.Add(candidates[i], respawnPointScore);
            }

            return scores;
        }

        public static float GetRespawnPointScore(MpTeam team, LevelData.SpawnPoint spawnPoint, List<float> distances, float max) {
            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);
            var playerTeams = (List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null);

            var count = playerPositions.Count;

            // For each spawn, find the closest player, and the two players that have the least line of sight to one or the other.
            var closest = float.MaxValue;
            var leastLoS = float.MaxValue;
            var scale = 1f;
            var seesEnemy = false;
            var seesTeammate = false;
            var closestTeammate = float.MaxValue;
            for (int i = 0; i < count; i++) {
                var dist = Math.Abs(distances[i]);

                Vector3 vector;
                vector.x = playerPositions[i].x - spawnPoint.position.x;
                vector.y = playerPositions[i].y - spawnPoint.position.y;
                vector.z = playerPositions[i].z - spawnPoint.position.z;
                var vectorDist = Mathf.Max(0.1f, vector.magnitude);
                vector.x /= vectorDist;
                vector.y /= vectorDist;
                vector.z /= vectorDist;

                if (team != playerTeams[i] || NetworkMatch.GetMode() == MatchMode.ANARCHY) {
                    closest = Math.Min(closest, dist);

                    if (VisibilityRaycast(spawnPoint.position, vector, vectorDist)) {
                        var angle = Math.Min(Vector3.Angle(playerPositions[i] - spawnPoint.position, spawnPoint.orientation * Vector3.forward), Vector3.Angle(spawnPoint.position - playerPositions[i], m_player_rot[i] * Vector3.forward));
                        leastLoS = Math.Min(leastLoS, angle);
                        seesEnemy = true;
                    }
                } else {
                    if (dist <= 30f && VisibilityRaycast(spawnPoint.position, vector, vectorDist)) {
                        seesTeammate = true;
                        closestTeammate = Math.Min(dist, closestTeammate);
                    }
                }

                if (dist < 15f) {
                    scale = (float)Math.Pow(Math.Min(scale, dist / 15f), 2);
                }
            }

            if (closest > 80f) {
                scale *= 80f / closest;
            }

            // Score the spawn point.
            var score = Math.Min(closest / Math.Max(max, 1f), 1f) * Math.Min(leastLoS / 60f, 1f) * scale;

            // Avoid respawning two ships on the same respawn point within a short amount of time.
            if (lastRespawn.ContainsKey(spawnPoint) && lastRespawn[spawnPoint] > DateTime.Now.AddSeconds(-2)) {
                score -= (float)(lastRespawn[spawnPoint] - DateTime.Now.AddSeconds(-2)).TotalSeconds;
            } else if (NetworkMatch.GetMode() == MatchMode.TEAM_ANARCHY) {
                // If the spawn point is not being avoided, give a bonus in team anarchy if the spawn point sees teammates and no enemies and doesn't have a teammate on the spawn point and no enemy is within 8 cubes.
                if (seesTeammate && !seesEnemy && closestTeammate > 5f && closest > 32f) {
                    score += 0.5f * scale;
                }
            }

            return score;
        }

        private static bool VisibilityRaycast(Vector3 pos, Vector3 td_vec, float dist) {
            int layerMask = 67125248;
            RaycastHit hitInfo;
            bool flag = Physics.Raycast(pos, td_vec, out hitInfo, dist, layerMask);
            return !flag;
        }
    }
}
