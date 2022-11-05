using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Client handle Full Chat message in custom colors 
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMessageManager), "AddFullChatMessage")]
    public static class NetworkMessageManager_AddFullChatMessage {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldc_I4_3) {
                    code.opcode = OpCodes.Ldarg_2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Teams), "TeamMessageColor"));
                    continue;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Client handle kill feed in custom colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMessageManager), "AddKillMessage")]
    public static class NetworkMessageManager_AddKillMessage {
        public static MatchMode IsExtMatchModeAnarchy() {
            if (NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                return MatchMode.TEAM_ANARCHY;

            return MatchMode.ANARCHY;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkMatch), "GetMode"))
                    code.operand = AccessTools.Method(typeof(NetworkMessageManager_AddKillMessage), "IsExtMatchModeAnarchy");

                if (code.opcode == OpCodes.Ldc_I4_3) {
                    state++;
                    code.opcode = OpCodes.Ldarg_S;
                    code.operand = state == 1 ? 5 : 2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Teams), "TeamMessageColor"));
                    continue;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Client handle Quick Chat in custom colors
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMessageManager), "AddQuickChatMessage")]
    public static class NetworkMessageManager_AddQuickChatMessage {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldc_I4_3) {
                    code.opcode = OpCodes.Ldarg_2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Teams), "TeamMessageColor"));
                    continue;
                }
                yield return code;
            }
        }
    }
}
