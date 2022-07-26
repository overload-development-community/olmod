using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameMod
{
    // Matcens get more HP as a single player mission progresses.
    // This patch adds a cap to this effect so that matcens don't get too bulky.
    [HarmonyPatch(typeof(RobotMatcen), "Start")]
    public class HpCap_RobotMatcen_Start
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            // Level numbers start at 0. The 12th level is number 11.
            const int MaxLevel = 11;
            
            foreach(var i in code)
            {
                // replace instances of
                //   LevelInfo.Level.LevelNum
                // with
                //   Min(LevelInfo.Level.LevelNum, MaxLevel)
                // by appending some instructions
                yield return i;
                if (i.opcode == OpCodes.Callvirt
                    && ((MethodInfo)i.operand).DeclaringType == typeof(Overload.LevelInfo)
                    && ((MethodInfo)i.operand).Name == "get_LevelNum")
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, MaxLevel);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Mathf), "Min", new Type[] { typeof(int), typeof(int) }));
                }
            }
        }
    }
}
