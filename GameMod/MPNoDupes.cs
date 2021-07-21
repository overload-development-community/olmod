using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;

namespace GameMod {
    /// <summary>
    /// Prevents stock items from being picked up by multiple pilots in the same frame.
    /// </summary>
    [HarmonyPatch(typeof(Item), "OnTriggerEnter")]
    class MPNoDupes_OnTriggerEnter {
        public static bool Prefix(Item __instance) {
            return __instance.m_type != ItemType.NONE;
        }

        public static void SetItemTypeToNoneAndDestroy(Item item) {
            item.m_type = ItemType.NONE;
            UnityEngine.Object.Destroy(item.c_go);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            var ldfldCount = 0;

            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldfld) {
                    ldfldCount++;

                    if (ldfldCount == 39) {
                        continue;
                    }
                }

                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "Destroy") {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPNoDupes_OnTriggerEnter), "SetItemTypeToNoneAndDestroy"));
                    continue;
                }

                yield return code;
            }
        }
    }
}
