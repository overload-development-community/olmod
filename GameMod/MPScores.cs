using Harmony;
using Overload;
using System.Collections.Generic;
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
    }
}
