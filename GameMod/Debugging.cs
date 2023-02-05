using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using System.Linq;
using System;

namespace GameMod {
    public class Debugging {
        public static bool Enabled = false;

        private static MethodInfo _UIManager_VisibilityRaycast_Method = AccessTools.Method(typeof(UIManager), "VisibilityRaycast");

        static void DrawSegmentNums() {
            if (!Debugging.Enabled) {
                return;
            }

            Vector3 shipPos = GameManager.m_player_ship.c_transform_position;
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
                    offset.y = 2f;
                    UIManager.DrawStringAlignCenter($"{i}", offset, 1f, UIManager.m_col_white2, -1f);
                    WorldText.PreviousQuadsTransformText(segCenter, shipQuat, dist, quad_index);
                }
            }
        }

        static void DrawSpawnPoints() {
            if (!Debugging.Enabled || spawn_scores.Count == 0) {
                return;
            }

            Vector3 shipPos = GameManager.m_player_ship.c_transform_position;
            Vector3 forward = GameManager.m_player_ship.c_camera_transform.forward;
            Quaternion shipQuat = GameManager.m_player_ship.c_transform.localRotation;

            var max = spawn_scores.Select(s => s.Value).Max();

            foreach (var score in spawn_scores) {
                Vector3 spawnPos = score.Key.position;
                Vector3 vector;
                vector.x = spawnPos.x - shipPos.x;
                vector.y = spawnPos.y - shipPos.y;
                vector.z = spawnPos.z - shipPos.z;
                float dist = Mathf.Max(0.1f, vector.magnitude);

                int quad_index = UIManager.m_quad_index;
                Vector2 offset = Vector2.zero;

                Color color = UIManager.m_col_red;

                // offset.y = -80f / dist;
                offset.y = 2f;
                UIManager.DrawStringAlignCenter($"Spawn", offset, 1f, color, -1f);
                WorldText.PreviousQuadsTransformText(spawnPos, shipQuat, dist, quad_index);
            }
        }

        private static FieldInfo _NetworkSpawnPoints_m_player_pos_Field = typeof(NetworkSpawnPoints).GetField("m_player_pos", BindingFlags.NonPublic | BindingFlags.Static);
        private static MethodInfo _NetworkSpawnPoints_GetRespawnPointCandidates_Method = typeof(NetworkSpawnPoints).GetMethod("GetRespawnPointCandidates", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _NetworkSpawnPoints_m_player_team_Field = typeof(NetworkSpawnPoints).GetField("m_player_team", BindingFlags.NonPublic | BindingFlags.Static);

        private static Dictionary<LevelData.SpawnPoint, float> spawn_scores = new Dictionary<LevelData.SpawnPoint, float>();
        static void CalculateSpawnScores() {
            if (!Debugging.Enabled) {
                return;
            }

            var respawnPointCandidates = MPRespawn_ChooseSpawnPoint.GetRespawnPointCandidates(GameManager.m_local_player.m_mp_team);
            spawn_scores = MPRespawn_ChooseSpawnPoint.GetRespawnPointScores(GameManager.m_local_player.m_mp_team, respawnPointCandidates);
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
        }
    }

    /*
    // CCF - This is now mandatory as part of the firepoint changes
    // Temporary debugging to find a bug with SwitchVisibleWeapon
    [HarmonyPatch(typeof(PlayerShip), "SwitchVisibleWeapon")]
    class Debugging_PlayerShip_SwitchVisibleWeapon
    {
        ...
    }
    */
}
