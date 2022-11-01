using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches {
    /// <summary>
    /// Reads the robotdata if it exists.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(RobotManager), "ReadPresetData")]
    public class RobotManager_ReadPresetData {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var dataReader_GetRobotData_Method = typeof(PresetData).GetMethod("GetRobotData");
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                    yield return new CodeInstruction(OpCodes.Call, dataReader_GetRobotData_Method);
                else
                    yield return code;
        }
    }

    /// <summary>
    /// Prevent RobotManager from removing triggers in multiplayer.
    /// </summary>
    [Mod(Mods.Triggers)]
    [HarmonyPatch(typeof(RobotManager), "TriggerInRelevantSegment")]
    public class RobotManager_TriggerInRelevantSegment {
        public static void Postfix(ref bool __result) {
            if (GameplayManager.IsMultiplayerActive) {
                __result = true;
            }
        }
    }
}
