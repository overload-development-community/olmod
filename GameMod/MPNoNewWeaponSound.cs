using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    [HarmonyPatch(typeof(Player), "UnlockWeaponClient")]
    class MPNoNewWeaponSound
    {
        private static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            Label lbl = ilGen.DefineLabel();
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4 && (int)code.operand == (int)SoundEffect.hud_notify_message1)
                {
                    var c = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GameplayManager), "IsMultiplayerActive"));
                    c.labels.AddRange(code.labels);
                    code.labels.Clear();
                    yield return c;
                    yield return new CodeInstruction(OpCodes.Brtrue, lbl);
                    state = 1;
                }
                else if (state == 1 && code.opcode == OpCodes.Call)
                {
                    state = 2;
                }
                else if (state == 2)
                {
                    code.labels.Add(lbl);
                    state = 3;
                }
                yield return code;
            }
        }
    }
}
