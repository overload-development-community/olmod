using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using HarmonyLib;

namespace GameMod.Patches {
    /// <summary>
    /// Uses an unused bool field "m_one_time" in TriggerEnergy to cause it to only activate every second frame with a double
    /// energy payload from stock. Shouldn't strictly be necessary but apparently the energy center causes a lot of activity
    /// simultaneously and this *should* cut that in half with no gameplay side-effect. This could have been done faster (but a
    /// little less efficiently) with a prefix probably.
    /// </summary>
    [Mod(Mods.EnergyCenterPerformance)]
    [HarmonyPatch(typeof(TriggerEnergy), "OnTriggerStay")]
    public static class TriggerEnergy_OnTriggerStay {
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes) {
            bool state = false;
            Label a = ilGen.DefineLabel();

            foreach (var code in codes) {
                if (!state) {
                    state = true; // only run once
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerEnergy), "m_one_time"));
                    yield return new CodeInstruction(OpCodes.Brtrue, a); // if true, run the original actual method
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(TriggerEnergy), "m_one_time"));
                    yield return new CodeInstruction(OpCodes.Ret); // if false above, this exits the method early after setting m_one_time to true
                    CodeInstruction after = new CodeInstruction(OpCodes.Ldarg_0);
                    after.labels.Add(a);
                    yield return after; // "brtrue" jumps here if m_one_time is true, sets it to false, and continues the method
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(TriggerEnergy), "m_one_time"));
                }

                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 15f) {
                    code.operand = 30f; // double the energy recharge payout since we halved the active recharge frames
                }

                yield return code;
            }
        }
    }
}
