using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GameMod
{
    [HarmonyPatch(typeof(GameManager), "FixCurrentDateFormat")]
    class DisableTimeCheat
    {
        static void Prefix()
        {
            GameManager.m_gm.DisableTimeCheatingDetector();
            GameManager.m_cheating_type = "olmod";
        }
    }

    [HarmonyPatch(typeof(PilotManager), "Save")]
    class PilotSave
    {
        public static bool OtherDetected()
        {
            return GameManager.m_cheating_detected && GameManager.m_cheating_type != "olmod";
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs)
        {
            foreach (var c in cs)
            {
                if (c.opcode == OpCodes.Ldsfld && ((FieldInfo)c.operand).Name == "m_cheating_detected")
                {
                    yield return new CodeInstruction(OpCodes.Call,
                        typeof(PilotSave).GetMethod("OtherDetected", BindingFlags.Public | BindingFlags.Static));
                    continue;
                }
                yield return c;
            }
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "DoneLevel")]
    internal class DisableTimeCheat_GameplayManager_DoneLevel
    {
        private static void Postfix()
        {

            if (!PilotSave.OtherDetected() && (GameplayManager.IsChallengeMode && GameplayManager.m_level_info.Mission.FileName != "_EDITOR" && ChallengeManager.ChallengeRobotsDestroyed > 0))
            {
                try {
                    Scores.UpdateChallengeScore(GameplayManager.m_level_info.LevelNum, GameplayManager.DifficultyLevel, ChallengeManager.CountdownMode, PilotManager.PilotName, ChallengeManager.ChallengeScore, ChallengeManager.ChallengeRobotsDestroyed, GameplayManager.MostDamagingWeapon(), GameplayManager.AliveTime);
                }
                catch (Exception ex) {
                    uConsole.Log(ex.Message);
                }
            }
        }
    }
}
