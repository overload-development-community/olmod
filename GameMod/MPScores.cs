using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GameMod
{
    /// <summary>
    /// Contains modifications to scoreboard display for various modes
    /// </summary>
    class MPScores
    {
        [HarmonyPatch(typeof(Player), "RpcAddAssist")]
        class MPScores_Player_RpcAddAssist
        {
            private static bool Prefix()
            {
                // Only track assists if assist scoring is enabled for this game
                return MPModPrivateData.AssistScoring;
            }
        }

        [HarmonyPatch(typeof(NetworkMatch), "GetHighestScoreAnarchy")]
        class MPScores_NetworkMatch_GetHighestScoreAnarchy_AssistSwitch
        {
            private static int Postfix(int result)
            {
                // Adjust highest score so kill goals work as expected in no-assist games
                return MPModPrivateData.AssistScoring ? result : (result / 3);
            }
        }

        static bool ShouldSuppressAssistScoring()
        {
            return NetworkMatch.m_head_to_head || !MPModPrivateData.AssistScoring;
        }

        // Change checks for NetworkMatch.m_head_to_head to also check for MPModPrivateData.AssistScoring
        static IEnumerable<CodeInstruction> AssistSwitchTranspiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(NetworkMatch), "m_head_to_head"))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPScores), "ShouldSuppressAssistScoring"));
                    continue;
                }

                yield return code;
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawHUDScoreInfo")]
        class MPScores_UIElement_DrawHUDScoreInfo_AssistSwitch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                return AssistSwitchTranspiler(codes);
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
        class MPScores_UIElement_DrawMpMiniScoreboard_AssistSwitch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                return AssistSwitchTranspiler(codes);
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawMpScoreboardRaw")]
        class MPScores_UIElement_DrawMpScoreboardRaw_AssistSwitch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                return AssistSwitchTranspiler(codes);
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawScoreHeader")]
        class MPScores_UIElement_DrawScoreHeader_AssistSwitch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                return AssistSwitchTranspiler(codes);
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawScoresWithoutTeams")]
        class MPScores_UIElement_DrawScoresWithoutTeams_AssistSwitch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                return AssistSwitchTranspiler(codes);
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawScoresForTeam")]
        class MPScores_UIElement_DrawScoresForTeam_AssistSwitch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                int stage = 0;
                var jumpLabel = new Label();
                foreach (var code in codes)
                {
                    if (stage == 0 && code.opcode == OpCodes.Ldarg_S && (byte)code.operand == 4)
                    {
                        stage = 1;
                    }
                    else if (stage == 1 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "DrawDigitsVariable")
                    {
                        yield return code;
                        // Now inject a branch before the assist column draw
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPScores), "ShouldSuppressAssistScoring"));
                        yield return new CodeInstruction(OpCodes.Brtrue, jumpLabel);
                        stage = 2;
                        continue;
                    }
                    else if (stage == 2 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "DrawDigitsVariable")
                    {
                        stage = 3;
                    }
                    else if (stage == 3)
                    {
                        code.labels.Add(jumpLabel);
                        stage = 4;
                    }

                    yield return code;
                }
            }
        }
    }
}
