using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameMod
{
    /// <summary>
    /// Matcens get extra HP on later levels in a single player campaign.
    /// This patch disables this effect if a certain property is set. A corresponding editor change will setup said property appropriately.
    /// </summary>
    [HarmonyPatch(typeof(RobotMatcen), "Start")]
    public class Matcen_Start_FixedHp
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            // replace instances of
            //   stloc.0 (i.e. setting a certain flag)
            // with
            //   ldarg.0
            //   ldfld m_destroyable
            // +-brnull
            // | ldarg.0
            // | ldfld m_destroyable
            // | ldfld m_special_index
            // | ldc.i4 2
            // | ceq
            // | or
            // +>stloc.0
            //
            // In C#:
            // flag = this.m_destroyable == null ? flag : (flag | this.m_destroyable.m_special_index == 2);

            foreach (var i in code)
            {
                if (i.opcode == OpCodes.Stloc_0)
                {
                    var skipLabel = new Label();

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RobotMatcen), "m_destroyable"));
                    yield return new CodeInstruction(OpCodes.Brfalse, skipLabel);
                    { // (if not null)
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RobotMatcen), "m_destroyable"));
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Destroyable), "m_special_index"));
                        yield return new CodeInstruction(OpCodes.Ldc_I4_2);
                        yield return new CodeInstruction(OpCodes.Ceq);
                        yield return new CodeInstruction(OpCodes.Or);
                    }
                    var end = new CodeInstruction(OpCodes.Stloc_0);
                    end.labels.Add(skipLabel);
                    yield return end;
                } 
                else
                {
                    yield return i;
                }
            }
        }
    }
}
