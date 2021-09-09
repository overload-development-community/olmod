using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    [HarmonyPatch(typeof(NetworkSpawnPoints), "PickGoodRespawnPointForTeam")]
    class MPRespawn_PickGoodRespawnPointForTeam {
        private static Dictionary<int, DateTime> lastRespawn = new Dictionary<int, DateTime>();
        private static FieldInfo _NetworkSpawnPoints_m_player_pos_Field = typeof(NetworkSpawnPoints).GetField("m_player_pos", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _NetworkSpawnPoints_m_player_team_Field = typeof(NetworkSpawnPoints).GetField("m_player_team", BindingFlags.NonPublic | BindingFlags.Static);
        private static MethodInfo _UIManager_VisibilityRaycast_Method = AccessTools.Method(typeof(UIManager), "VisibilityRaycast");

        internal static List<Quaternion> m_player_rot = new List<Quaternion>();

        public static bool Prefix(MpTeam team, List<int> candidates, ref int __result) {
            m_player_rot.Clear();

            for (int i = 0; i < NetworkManager.m_Players.Count; i++) {
                if ((float)NetworkManager.m_Players[i].m_hitpoints > 0f && !NetworkManager.m_Players[i].m_spectator) {
                    m_player_rot.Add(NetworkManager.m_Players[i].c_player_ship.c_transform.rotation);
                }
            }

            var mode = NetworkMatch.GetMode();

            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return true;
            }

            float num = float.MinValue;
            int result = -1;

            var distances = new Dictionary<int, List<float>>();

            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);

            var max = float.MinValue;

            for (int i = 0; i < candidates.Count; i++) {
                var dist = new List<float>();
                Vector3 position = GameManager.m_level_data.m_player_spawn_points[candidates[i]].position;
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
                distances.Add(candidates[i], dist);
            }

            for (int i = 0; i < candidates.Count; i++) {
                float respawnPointScore = GetRespawnPointScore(team, candidates[i], distances, max);

                // Add a +/- 5% random factor.
                respawnPointScore *= UnityEngine.Random.Range(0.95f, 1.05f);

                if (respawnPointScore > num) {
                    num = respawnPointScore;
                    result = i;
                }
            }

            __result = Math.Max(result, 0);
            lastRespawn[__result] = DateTime.Now;

            return false;
        }

        public static float GetRespawnPointScore(MpTeam team, int idx, Dictionary<int, List<float>> distances, float max) {
            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);
            var playerTeams = (List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null);

            int count = playerPositions.Count;

            // For each spawn, find the closest player, and the two players that have the least line of sight to one or the other.
            var spawnPoint = GameManager.m_level_data.m_player_spawn_points[idx];
            var closest = float.MaxValue;
            var leastLoS = float.MaxValue;
            var scale = 1f;
            var seesEnemy = false;
            var seesTeammate = false;
            for (int i = 0; i < count; i++) {
                var dist = Math.Abs(distances[idx][i]);

                Vector3 vector;
                vector.x = playerPositions[i].x - spawnPoint.position.x;
                vector.y = playerPositions[i].y - spawnPoint.position.y;
                vector.z = playerPositions[i].z - spawnPoint.position.z;
                float vectorDist = Mathf.Max(0.1f, vector.magnitude);
                vector.x /= vectorDist;
                vector.y /= vectorDist;
                vector.z /= vectorDist;

                if (team != playerTeams[i] || team == MpTeam.ANARCHY) {
                    closest = Math.Min(closest, dist);

                    if ((bool)_UIManager_VisibilityRaycast_Method.Invoke(null, new object[] { spawnPoint.position, vector, vectorDist })) {
                        var angle = Math.Min(Vector3.Angle(playerPositions[i] - spawnPoint.position, spawnPoint.orientation * Vector3.forward), Vector3.Angle(spawnPoint.position - playerPositions[i], m_player_rot[i] * Vector3.forward));
                        leastLoS = Math.Min(leastLoS, angle);
                        seesEnemy = true;
                    }
                } else {
                    if ((bool)_UIManager_VisibilityRaycast_Method.Invoke(null, new object[] { spawnPoint.position, vector, vectorDist })) {
                        seesTeammate = true;
                    }
                }

                if (dist < 10f) {
                    scale = (float)Math.Pow(Math.Min(scale, dist / 10f), 2);
                }
            }

            // Score the spawn point.
            var score = Math.Min(closest / Math.Max(max, 1f), 1f) * Math.Min(leastLoS / 30f, 1f) * scale;

            // Avoid respawning two ships on the same respawn point within a short amount of time.
            if (lastRespawn.ContainsKey(idx) && lastRespawn[idx] > DateTime.Now.AddSeconds(-2)) {
                score -= (float)(lastRespawn[idx] - DateTime.Now.AddSeconds(-2)).TotalSeconds;
            } else if (NetworkMatch.GetMode() == MatchMode.TEAM_ANARCHY) {
                // If the spawn point is not being avoided, give a bonus in team anarchy if the spawn point sees teammates and no enemies.
                if (seesTeammate && !seesEnemy) {
                    score += 1f;
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
