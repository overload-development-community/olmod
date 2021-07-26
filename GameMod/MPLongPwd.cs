using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;

// by Tobias
namespace GameMod
{
    [HarmonyPatch(typeof(MenuManager), "ProcessInputField")]
    class MPLongPwdProcessInputField
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 16)
                {
                    code.operand = 127;
                }
                yield return code;
            }
        }
    }
}
