using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class Debugging {
        public static bool Enabled = false;

        private static MethodInfo _UIManager_VisibilityRaycast_Method = AccessTools.Method(typeof(UIManager), "VisibilityRaycast");

        static void DrawSegmentNums() {
            if (!Debugging.Enabled) {
                return;
            }

            Vector3 shipPos = GameManager.m_player_ship.c_transform_position;
            Vector3 forward = GameManager.m_player_ship.c_camera_transform.forward;
            Quaternion shipQuat = GameManager.m_player_ship.c_transform.localRotation;

            for (int i = 0; i < GameManager.m_level_data.Segments.Length; i++) {
                var seg = GameManager.m_level_data.Segments[i];
                Vector3 segCenter = seg.Center;
                Vector3 vector;
                vector.x = segCenter.x - shipPos.x;
                vector.y = segCenter.y - shipPos.y;
                vector.z = segCenter.z - shipPos.z;
                float dist = Mathf.Max(0.1f, vector.magnitude);
                vector.x /= dist;
                vector.y /= dist;
                vector.z /= dist;
                if ((bool)_UIManager_VisibilityRaycast_Method.Invoke(null, new object[] { shipPos, vector, dist })) {
                    int quad_index = UIManager.m_quad_index;
                    Vector2 offset = Vector2.zero;
                    offset.y = -80f / dist;
                    UIManager.DrawStringAlignCenter($"{i}", offset, 1f, UIManager.m_col_white2, -1f);
                    WorldText.PreviousQuadsTransformText(segCenter, shipQuat, dist, quad_index);
                }
            }
        }

        static void DrawSpawnPoints() {
            if (!Debugging.Enabled) {
                return;
            }

            Vector3 shipPos = GameManager.m_player_ship.c_transform_position;
            Vector3 forward = GameManager.m_player_ship.c_camera_transform.forward;
            Quaternion shipQuat = GameManager.m_player_ship.c_transform.localRotation;

            for (int i = 0; i < GameManager.m_level_data.m_player_spawn_points.Length; i++) {
                LevelData.SpawnPoint sp = GameManager.m_level_data.m_player_spawn_points[i];
                Vector3 spawnPos = sp.position;
                Vector3 vector;
                vector.x = spawnPos.x - shipPos.x;
                vector.y = spawnPos.y - shipPos.y;
                vector.z = spawnPos.z - shipPos.z;
                float dist = Mathf.Max(0.1f, vector.magnitude);

                int quad_index = UIManager.m_quad_index;
                Vector2 offset = Vector2.zero;
                offset.y = -80f / dist;
                UIManager.DrawStringAlignCenter($"Spawn {i}", offset, 1f, UIManager.m_col_red, -1f);
                if (spawn_scores.ContainsKey(i)) {
                    offset.y += 2f;
                    UIManager.DrawStringAlignCenter($"{spawn_scores[i]:N5}", offset, 1f, UIManager.m_col_red, -1f);
                }
                WorldText.PreviousQuadsTransformText(spawnPos, shipQuat, dist, quad_index);
            }
        }

        private static FieldInfo _NetworkSpawnPoints_m_player_pos_Field = typeof(NetworkSpawnPoints).GetField("m_player_pos", BindingFlags.NonPublic | BindingFlags.Static);
        private static MethodInfo _NetworkSpawnPoints_GetRespawnPointCandidates_Method = typeof(NetworkSpawnPoints).GetMethod("GetRespawnPointCandidates", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _NetworkSpawnPoints_m_player_team_Field = typeof(NetworkSpawnPoints).GetField("m_player_team", BindingFlags.NonPublic | BindingFlags.Static);

        private static Dictionary<int, float> spawn_scores = new Dictionary<int, float>();
        public static int BestSpawn;

        static void CalculateSpawnScores() {
            if (!Debugging.Enabled) {
                return;
            }

            MPRespawn_PickGoodRespawnPointForTeam.m_player_rot.Clear();
            spawn_scores.Clear();

            var candidates = (List<int>)_NetworkSpawnPoints_GetRespawnPointCandidates_Method.Invoke(null, new object[] { GameManager.m_local_player.m_mp_team });
            if (candidates.Count == 0) {
                return;
            }

            int count = NetworkManager.m_Players.Count;
            ((List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null)).Clear();
            ((List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null)).Clear();
            for (int i = 0; i < count; i++) {
                if ((float)NetworkManager.m_Players[i].m_hitpoints > 0f && !NetworkManager.m_Players[i].m_spectator) {
                    ((List<Vector3>)_NetworkSpawnPoints_m_player_pos_Field.GetValue(null)).Add(NetworkManager.m_Players[i].c_player_ship.c_transform.position);
                    ((List<MpTeam>)_NetworkSpawnPoints_m_player_team_Field.GetValue(null)).Add(NetworkManager.m_Players[i].m_mp_team);
                }
            }

            for (int i = 0; i < NetworkManager.m_Players.Count; i++) {
                if ((float)NetworkManager.m_Players[i].m_hitpoints > 0f && !NetworkManager.m_Players[i].m_spectator) {
                    MPRespawn_PickGoodRespawnPointForTeam.m_player_rot.Add(NetworkManager.m_Players[i].c_player_ship.c_transform.rotation);
                }
            }

            var mode = NetworkMatch.GetMode();

            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return;
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

                    var distance = MPRespawn_PickGoodRespawnPointForTeam.FindShortestPath(segnum, playerSegnum, -1, 9999f);
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
                float respawnPointScore = MPRespawn_PickGoodRespawnPointForTeam.GetRespawnPointScore(GameManager.m_local_player.m_mp_team, candidates[i], distances, max);

                spawn_scores.Add(candidates[i], respawnPointScore);

                if (respawnPointScore > num) {
                    num = respawnPointScore;
                    result = i;
                }
            }

            // Get the spawn index.
            BestSpawn = candidates[result];
        }
    }

    /// <summary>
    /// Riff on UIManager.PreviousQuadsTransformPlayer
    /// </summary>
    class WorldText {
        public static void PreviousQuadsTransformText(Vector3 worldPos, Quaternion localOrient, float dist, int old_index) {
            float num = 0.0125f * dist;
            for (int i = UIManager.m_quad_index - 1; i >= old_index; i--) {
                int num2 = i * 4;
                for (int j = 0; j < 4; j++) {
                    Vector3[] vertices = UIManager.m_vertices;
                    int num3 = num2 + j;
                    vertices[num3].x = vertices[num3].x * num;
                    Vector3[] vertices2 = UIManager.m_vertices;
                    int num4 = num2 + j;
                    vertices2[num4].y = vertices2[num4].y * num;
                    Vector3[] vertices3 = UIManager.m_vertices;
                    int num5 = num2 + j;
                    vertices3[num5].z = vertices3[num5].z * num;
                    UIManager.m_vertices[num2 + j] = localOrient * UIManager.m_vertices[num2 + j];
                    UIManager.m_vertices[num2 + j].x = UIManager.m_vertices[num2 + j].x + worldPos.x;
                    UIManager.m_vertices[num2 + j].y = UIManager.m_vertices[num2 + j].y + worldPos.y;
                    UIManager.m_vertices[num2 + j].z = UIManager.m_vertices[num2 + j].z + worldPos.z;
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIManager), "Draw")]
    class Debugging_UIManager_Draw {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIManager), "DrawMultiplayerNames"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIManager), "EndDrawing")) {
                    state = 2;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Debugging), "CalculateSpawnScores"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Debugging), "DrawSegmentNums")) { labels = code.labels };
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Debugging), "DrawSpawnPoints"));
                    code.labels = null;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class Debugging_UIElement_DrawHUD {
        static void Prefix(UIElement __instance) {
            if (!Debugging.Enabled) {
                return;
            }

            int curSeg = GameManager.m_player_ship.GetMovingObject().CurrentSegmentIndex;
            __instance.DrawStringSmall($"Cur Seg: {curSeg}", new Vector2(UIManager.UI_LEFT + 5f, UIManager.UI_TOP + 75f), 0.5f, StringOffset.LEFT, UIManager.m_col_white2, 0.5f, -1f);
            __instance.DrawStringSmall($"Best Spawn: {Debugging.BestSpawn}", new Vector2(UIManager.UI_LEFT + 5f, UIManager.UI_TOP + 90f), 0.5f, StringOffset.LEFT, UIManager.m_col_white2, 0.5f, -1f);
        }
    }
}
