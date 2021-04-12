using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod {
    [HarmonyPatch(typeof(NetworkSpawnPoints), "PickGoodRespawnPointForTeam")]
    class MPRespawn_PickGoodRespawnPointForTeam {
        private static FieldInfo _NetworkSpawnPoints_m_player_pos_Field = typeof(NetworkSpawnPoints).GetField("m_player_pos", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _NetworkSpawnPoints_m_player_team_Field = typeof(NetworkSpawnPoints).GetField("m_player_team", BindingFlags.NonPublic | BindingFlags.Static);

        public static bool Prefix(MpTeam team, List<int> candidates, ref int __result) {
            var mode = NetworkMatch.GetMode();

            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return true;
            }

            float num = float.MinValue;
            int result = -1;

            var distances = new List<List<float>>();

            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);

            var max = float.MinValue;

            for (int i = 0; i < candidates.Count; i++) {
                var dist = new List<float>();
                for (int j = 0; j < playerPositions.Count; j++) {
                    Vector3 position = GameManager.m_level_data.m_player_spawn_points[candidates[i]].position;
                    var segnum = GetSegmentNumber(position);
                    var playerSegnum = GetSegmentNumber(playerPositions[j]);

                    var distance = Pathfinding.FindConnectedDistance(segnum, playerSegnum, out int _);

                    if (distance > max) {
                        max = distance;
                    }

                    dist.Add(distance);
                }
                distances.Add(dist);
            }

            for (int i = 0; i < candidates.Count; i++) {
                float respawnPointScore = GetRespawnPointScore(team, candidates[i], distances, max);
                if (respawnPointScore > num) {
                    num = respawnPointScore;
                    result = i;
                }
            }

            __result = Math.Max(result, 0);

            return false;
        }

        private static int GetSegmentNumber(Vector3 position) {
            var best = -1;
            var bestMagnitude = float.MaxValue;

            for (int i = 0; i < Pathfinding.Segments.Length; i++) {
                var magnitude = (position - Pathfinding.Segments[i].Center).magnitude;
                if (magnitude < bestMagnitude) {
                    best = i;
                    bestMagnitude = magnitude;
                }
            }

            return best;
        }

        public static float GetRespawnPointScore(MpTeam team, int idx, List<List<float>> distances, float max) {
            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);
            var playerTeams = (List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null);

            Vector3 position = GameManager.m_level_data.m_player_spawn_points[idx].position;

            float num = UnityEngine.Random.Range(max / 2f, max);
            int count = playerPositions.Count;
            float num2 = 3f / (2f + count);

            for (int i = 0; i < count; i++) {
                var dist = distances[idx][i];

                if (team != playerTeams[i] || team == MpTeam.ANARCHY) {
                    float num3 = (RUtility.FindVec3Distance(position - playerPositions[i]) + dist) / 2;
                    if (num3 < 40f) {
                        float num4 = (40f - num3) * (40f - num3) * 0.2f;
                        num -= num4 * num2;
                        if (num3 < 10f) {
                            num -= (10f - num3) * 50f;
                        }
                    }
                    num += dist / 4;
                } else {
                    float num5 = (RUtility.FindVec3Distance(position - playerPositions[i]) + dist) / 2;
                    if (num5 > 25f) {
                        num -= (num5 - 25f) * 2f * num2;
                    } else if (num5 < 5f) {
                        num -= (5f - num5) * 100f;
                    }
                }
            }

            return num;
        }
    }
}
