using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    class MPSuddenDeath
    {
        public static bool SuddenDeathMenuEnabled = false;
        public static bool SuddenDeathMatchEnabled = false;
        public static bool InOvertime = false;

        public static int GetTimer()
        {
            if (NetworkMatch.m_match_time_limit_seconds == int.MaxValue || !SuddenDeathMatchEnabled)
            {
                return NetworkMatch.m_match_time_remaining;
            }

            return Math.Abs(NetworkMatch.m_match_time_limit_seconds - (int)NetworkMatch.m_match_elapsed_seconds);
        }
    }

    public class SuddenDeathCustomMsg
    {
        public const short MsgSuddenDeath = 126;
    }

    public class SuddenDeathMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(m_message);
        }
        public override void Deserialize(NetworkReader reader)
        {
            m_message = reader.ReadString();
        }

        public string m_message;
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class SuddenDeathInitBeforeEachMatch
    {
        private static void Postfix()
        {
            MPSuddenDeath.InOvertime = false;
        }
    }
    
    [HarmonyPatch(typeof(NetworkMatch), "MaybeEndTimer")]
    class MaybeEndTimer
    {
        public static bool Prefix()
        {
            if (
                NetworkMatch.m_match_elapsed_seconds > NetworkMatch.m_match_time_limit_seconds
                && (NetworkMatch.GetMode() == MatchMode.MONSTERBALL || NetworkMatch.GetMode() == CTF.MatchModeCTF)
                && MPSuddenDeath.SuddenDeathMatchEnabled
                && NetworkMatch.m_team_scores[(int)MpTeam.TEAM0] == NetworkMatch.m_team_scores[(int)MpTeam.TEAM1]
            )
            {
                if (!MPSuddenDeath.InOvertime)
                {
                    Debug.Log("Sudden Death!");
                    MPSuddenDeath.InOvertime = true;
                    MPHUDMessage.SendToAll("Sudden Death!");

                }
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUDScoreInfo")]
    class DrawHUDScoreInfo
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var code in instructions)
            {
                if (!found && code.opcode == OpCodes.Ldloc_S && ((LocalBuilder)code.operand).LocalIndex == 5)
                {
                    found = true;

                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSuddenDeath), "GetTimer"));

                    continue;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
    class DrawMpMiniScoreboard
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var code in instructions)
            {
                if (!found && code.opcode == OpCodes.Ldloc_1)
                {
                    found = true;

                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSuddenDeath), "GetTimer"));

                    continue;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPSuddenDeath_DrawMpMatchSetup
    {
        private static string GetMMSSuddenDeath()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(MPSuddenDeath.SuddenDeathMenuEnabled));
        }

        private static void DrawSuddenDeathToggle(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawStringOptionItem(Loc.LS("SUDDEN DEATH OVERTIME"), position, 10, GetMMSSuddenDeath(), string.Empty, 1f, MenuManager.mms_mode != MatchMode.NUM);
            position.y += 62f;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var powerupFound = false;
            foreach (var code in codes)
            {
                if (!powerupFound && code.opcode == OpCodes.Ldstr && (string)code.operand == "POWERUP SETTINGS")
                {
                    powerupFound = true;
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSuddenDeath_DrawMpMatchSetup), "DrawSuddenDeathToggle"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }

                yield return code;
            }
        }
    }

    // Process slider input
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPSuddenDeath_MpMatchSetup
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 3 &&
                UIManager.m_menu_selection == 10)
            {
                MPSuddenDeath.SuddenDeathMenuEnabled = !MPSuddenDeath.SuddenDeathMenuEnabled;
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }
        }
    }
}
