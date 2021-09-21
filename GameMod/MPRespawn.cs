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

                    var distance = MPRespawn_ChooseSpawnPoint.FindShortestPath(segnum, segnum2, -1, 9999f);
                    if (distance <= 0f || distance >= 9999f) {
                        distance = RUtility.FindVec3Distance(position - position2);
                    }

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
        private static MethodInfo _NetworkSpawnPoints_GetRandomRespawnPointWithoutFiltering_Method = AccessTools.Method(typeof(NetworkSpawnItem), "GetRandomRespawnPointWithoutFiltering");

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
        private static MethodInfo _UIManager_VisibilityRaycast_Method = AccessTools.Method(typeof(UIManager), "VisibilityRaycast");

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
                var segnum = GameManager.m_level_data.FindSegmentContainingWorldPosition(position);
                for (int j = 0; j < playerPositions.Count; j++) {
                    var playerSegnum = GameManager.m_level_data.FindSegmentContainingWorldPosition(playerPositions[j]);

                    var distance = FindShortestPath(segnum, playerSegnum, -1, 9999f);
                    if (distance <= 0f || distance >= 9999f) {
                        distance = RUtility.FindVec3Distance(position - playerPositions[j]);
                    }

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

                    if ((bool)_UIManager_VisibilityRaycast_Method.Invoke(null, new object[] { spawnPoint.position, vector, vectorDist })) {
                        var angle = Math.Min(Vector3.Angle(playerPositions[i] - spawnPoint.position, spawnPoint.orientation * Vector3.forward), Vector3.Angle(spawnPoint.position - playerPositions[i], m_player_rot[i] * Vector3.forward));
                        leastLoS = Math.Min(leastLoS, angle);
                        seesEnemy = true;
                    }
                } else {
                    if (dist <= 20f && (bool)_UIManager_VisibilityRaycast_Method.Invoke(null, new object[] { spawnPoint.position, vector, vectorDist })) {
                        seesTeammate = true;
                        closestTeammate = Math.Min(dist, closestTeammate);
                    }
                }

                if (dist < 15f) {
                    scale = (float)Math.Pow(Math.Min(scale, dist / 15f), 2);
                }
            }

            // Score the spawn point.
            var score = Math.Min(closest / Math.Max(max, 1f), 1f) * Math.Min(leastLoS / 60f, 1f) * scale;

            // Avoid respawning two ships on the same respawn point within a short amount of time.
            if (lastRespawn.ContainsKey(spawnPoint) && lastRespawn[spawnPoint] > DateTime.Now.AddSeconds(-2)) {
                score -= (float)(lastRespawn[spawnPoint] - DateTime.Now.AddSeconds(-2)).TotalSeconds;
            } else if (NetworkMatch.GetMode() == MatchMode.TEAM_ANARCHY) {
                // If the spawn point is not being avoided, give a bonus in team anarchy if the spawn point sees teammates and no enemies and doesn't have a teammate on the spawn point.
                if (seesTeammate && !seesEnemy && closestTeammate > 5f) {
                    score += 0.5f * scale;
                }
            }

            return score;
        }

        private static MethodInfo _Pathfinding_LegalEntryPortal_Method = AccessTools.Method(typeof(Pathfinding), "LegalEntryPortal");
        private static MethodInfo _Pathfinding_MarkAllSegments_Method = AccessTools.Method(typeof(Pathfinding), "MarkAllSegments");
        private static MethodInfo _Pathfinding_StorePath1_Method = AccessTools.Method(typeof(Pathfinding), "StorePath1");
        private static FieldInfo _Pathfinding_m_level_data_Field = typeof(Pathfinding).GetField("m_level_data", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _Pathfinding_m_path_nodes_Field = typeof(Pathfinding).GetField("m_PathNodes", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _Pathfinding_m_segment_buffer_Field = typeof(Pathfinding).GetField("m_SegmentBuffer", BindingFlags.NonPublic | BindingFlags.Static);

        public static float FindShortestPath(int start_seg, int end_seg, int avoid_seg, float max_distance) {
            int num;
            int num2 = 1;
            int num3 = end_seg;
            float path_distance = 0f;
            if (start_seg == -1 || end_seg == -1) {
                return 9999f;
            }
            if (_Pathfinding_m_level_data_Field.GetValue(null) == null) {
                Debug.Log(Time.frameCount + ": uninitialized level data.");
                return 9999f;
            }
            Vector3 center = Pathfinding.Segments[num3].Center;
            _Pathfinding_MarkAllSegments_Method.Invoke(null, new object[] { });
            if (avoid_seg != -1) {
                ((bool[])_Pathfinding_m_segment_buffer_Field.GetValue(null))[avoid_seg] = true;
            }

            var m_path_nodes = ((Pathfinding.PathNode[])_Pathfinding_m_path_nodes_Field.GetValue(null));

            m_path_nodes[0].segnum = num3;
            m_path_nodes[0].distance = 0f;
            m_path_nodes[0].parent = -1;
            ((bool[])_Pathfinding_m_segment_buffer_Field.GetValue(null))[num3] = true;
            num = 1;
            int num4 = 0;
            Vector3 vector = default(Vector3);
            int num10;
            while (true) {
                int num5;
                if (num4 < num2 + 1) {
                    if (m_path_nodes[num4].segnum != -1) {
                        center = Pathfinding.Segments[m_path_nodes[num4].segnum].Center;
                        num3 = m_path_nodes[num4].segnum;
                        num5 = 0;
                        for (int i = 0; i < 6; i++) {
                            SegmentData segmentData = Pathfinding.Segments[num3];
                            int num6 = segmentData.Portals[i];
                            if (num6 != -1 || segmentData.WarpDestinationSegs[i] != -1) {
                                int num7;
                                if (segmentData.WarpDestinationSegs[i] != -1) {
                                    num7 = segmentData.WarpDestinationSegs[i];
                                } else {
                                    PortalData portalData = Pathfinding.Portals[num6];
                                    if ((segmentData.DecalFlags & (uint)(1 << i)) != 0 || (segmentData.Pathfinding == PathfindingType.GUIDEBOT_ONLY) || !(bool)_Pathfinding_LegalEntryPortal_Method.Invoke(null, new object[] { num3, num6, true })) {
                                        continue;
                                    }
                                    num7 = ((portalData.MasterSegmentIndex != num3) ? portalData.MasterSegmentIndex : portalData.SlaveSegmentIndex);
                                }
                                if (!((bool[])_Pathfinding_m_segment_buffer_Field.GetValue(null))[num7]) {
                                    vector.x = Pathfinding.Segments[num7].Center.x - center.x;
                                    vector.y = Pathfinding.Segments[num7].Center.y - center.y;
                                    vector.z = Pathfinding.Segments[num7].Center.z - center.z;
                                    float num8 = vector.x * vector.x + vector.y * vector.y + vector.z * vector.z;
                                    float num9 = m_path_nodes[num4].distance + (float)Math.Sqrt(num8);
                                    if (num9 < max_distance) {
                                        m_path_nodes[num + num5].distance = num9;
                                        m_path_nodes[num + num5].segnum = num7;
                                        m_path_nodes[num + num5].parent = num4;
                                        ((bool[])_Pathfinding_m_segment_buffer_Field.GetValue(null))[num7] = true;
                                        num5++;
                                        num2 = num + num5;
                                    } else {
                                        m_path_nodes[num + num5].segnum = -1;
                                    }
                                } else {
                                    m_path_nodes[num + num5].segnum = -1;
                                }
                                if (num7 != start_seg) {
                                    continue;
                                }
                                goto IL_0393;
                            }
                            m_path_nodes[num + num5].segnum = -1;
                        }
                        num += num5;
                    }
                    num4++;
                    continue;
                }
                num10 = -1;
                break;
            IL_0393:
                num10 = num + num5 - 1;
                path_distance = m_path_nodes[num10].distance;
                break;
            }
            if (num4 > num2) {
                return 9999f;
            }
            if (num10 == -1) {
                num10 = -1;
            }
            int path_length = (int)_Pathfinding_StorePath1_Method.Invoke(null, new object[] { null, num10, start_seg, end_seg });
            if (path_length == -1) {
                return 9999f;
            }
            return path_distance;
        }
    }
}
