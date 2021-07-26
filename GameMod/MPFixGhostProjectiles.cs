using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(ChunkManager), "ActivateChunks")]
    class MPFixGhostProjectiles_ChunkManager_ActivateChunks
    {
        static bool Prefix(GameObject[] ___m_master_chunk_array, int ___m_num_chunks_in_level)
        {
            int numSegments = GameManager.m_level_data.NumSegments;
            int segmentIndex = GameManager.m_player_ship.SegmentIndex;
            if (segmentIndex == -1)
            {
                return false;
            }

            if (GameplayManager.IsMultiplayer && Server.IsActive())
            {
                return false;
            }

            if (!GameplayManager.m_use_segment_visibility)
            {
                int num = RobotManager.m_active_robot_segments.Length;
                for (int i = 0; i < num; i++)
                {
                    var mr = ___m_master_chunk_array[ChunkManager.Segments[i].ChunkNum].GetComponent<Renderer>();
                    if (mr != null)
                        mr.enabled = true;
                    ___m_master_chunk_array[ChunkManager.Segments[i].ChunkNum].SetActive(true);
                }
                return false;
            }

            bool[] array = new bool[___m_num_chunks_in_level];
            for (int j = 0; j < ___m_num_chunks_in_level; j++)
            {
                array[j] = false;
            }

            for (int k = 0; k < numSegments; k++)
            {
                if (GameManager.m_level_data.m_segment_visibility[segmentIndex, k] > 0)
                {
                    array[ChunkManager.Segments[k].ChunkNum] = true;
                }
            }

            for (int l = 0; l < ___m_num_chunks_in_level; l++)
            {
                if (___m_master_chunk_array[l].activeSelf != array[l])
                {
                    var mr = ___m_master_chunk_array[ChunkManager.Segments[l].ChunkNum].GetComponent<Renderer>();
                    if (mr != null)
                        mr.enabled = array[l];
                    if (array[l])
                    {
                        ___m_master_chunk_array[ChunkManager.Segments[l].ChunkNum].SetActive(array[l]);
                    }
                }
            }

            int num2 = RobotManager.m_active_robot_segments.Length;
            for (int m = 0; m < num2; m++)
            {
                var mr = ___m_master_chunk_array[ChunkManager.Segments[m].ChunkNum].GetComponent<Renderer>();
                if (mr != null)
                    mr.enabled = true;
                ___m_master_chunk_array[ChunkManager.Segments[m].ChunkNum].SetActive(true);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ChunkManager), "ForceActivateAll")]
    class MPFixGhostProjectiles_ChunkManager_ForceActivateAll
    {
        static void SetEnabledTrue(GameObject go)
        {
            var mr = go.GetComponent<Renderer>();
            if (mr != null)
                mr.enabled = true;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(ChunkManager), "m_master_chunk_array"))
                    state++;

                if (state == 2 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(GameObject), "SetActive"))
                {
                    state = 3;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ChunkManager), "m_master_chunk_array"));
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Ldelem_Ref);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPFixGhostProjectiles_ChunkManager_ForceActivateAll), "SetEnabledTrue"));
                    continue;
                }
                yield return code;
            }
        }
    }
}
