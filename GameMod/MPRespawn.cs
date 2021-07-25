using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod {
    [HarmonyPatch(typeof(NetworkSpawnPoints), "PickGoodRespawnPointForTeam")]
    class MPRespawn_PickGoodRespawnPointForTeam {
        private static Dictionary<int, DateTime> lastRespawn = new Dictionary<int, DateTime>();
        private static FieldInfo _NetworkSpawnPoints_m_player_pos_Field = typeof(NetworkSpawnPoints).GetField("m_player_pos", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _NetworkSpawnPoints_m_player_team_Field = typeof(NetworkSpawnPoints).GetField("m_player_team", BindingFlags.NonPublic | BindingFlags.Static);

        private static List<Quaternion> m_player_rot = new List<Quaternion>();

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
                for (int j = 0; j < playerPositions.Count; j++) {
                    Vector3 position = GameManager.m_level_data.m_player_spawn_points[candidates[i]].position;
                    var segnum = GetSegmentNumber(position);
                    var playerSegnum = GetSegmentNumber(playerPositions[j]);

                    var distance = Pathfinding.FindConnectedDistance(segnum, playerSegnum, out int _);
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
                if (respawnPointScore > num) {
                    num = respawnPointScore;
                    result = i;
                }
            }

            __result = Math.Max(result, 0);
            lastRespawn[__result] = DateTime.Now;

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

        public static float GetRespawnPointScore(MpTeam team, int idx, Dictionary<int, List<float>> distances, float max) {
            var playerPositions = (List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null);
            var playerTeams = (List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null);

            int count = playerPositions.Count;

            // For each spawn, find the closest player, and the two players that have the least line of sight to one or the other.
            var spawnPoint = GameManager.m_level_data.m_player_spawn_points[idx];
            var closest = float.MaxValue;
            var leastLoS = float.MaxValue;
            var scale = 1f;
            for (int i = 0; i < count; i++) {
                var dist = Math.Abs(distances[idx][i]);
                if (team != playerTeams[i] || team == MpTeam.ANARCHY) {
                    var angle = Math.Min(Vector3.Angle(playerPositions[i] - spawnPoint.position, spawnPoint.orientation * Vector3.forward), Vector3.Angle(spawnPoint.position - playerPositions[i], m_player_rot[i] * Vector3.forward));

                    closest = Math.Min(closest, dist);
                    leastLoS = Math.Min(leastLoS, angle);
                }

                if (dist < 10f) {
                    scale = Math.Min(scale, dist / 10f);
                }
            }

            // Score the spawn point with a +/- 5% random factor.
            var score = Math.Min(closest / Math.Max(max, 1f), 1f) * Math.Min(leastLoS / 15f, 1f) * scale * UnityEngine.Random.Range(0.95f, 1.05f);

            // Avoid respawning two ships on the same respawn point within a short amount of time.
            if (lastRespawn.ContainsKey(idx) && lastRespawn[idx] > DateTime.Now.AddSeconds(-2)) {
                score -= (float)(lastRespawn[idx] - DateTime.Now.AddSeconds(-2)).TotalSeconds;
            }

            return score;
        }
    }
}
