using HarmonyLib;
using Overload;
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
}
