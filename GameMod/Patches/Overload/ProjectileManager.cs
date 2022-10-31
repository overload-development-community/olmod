using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Reads the projdata if it exists.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(ProjectileManager), "ReadProjPresetData")]
    public class ProjectileManager_ReadProjPresetData {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var dataReader_GetProjData_Method = typeof(PresetData).GetMethod("GetProjData");
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                    yield return new CodeInstruction(OpCodes.Call, dataReader_GetProjData_Method);
                else
                    yield return code;
        }
    }
}
