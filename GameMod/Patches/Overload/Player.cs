using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Sets the minimium XP required for modifiers to 0.
    /// </summary>
    [Mod(Mods.UnlockModifiers)]
    [HarmonyPatch(typeof(Player), "GetModifierMinXP")]
    public class MPUnlockAllModifiers {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(ref int __result) {
            __result = 0;
            return false;
        }
    }

    /// <summary>
    /// Allows us to keep track of who killed/assisted/died for reporting to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
    public class Player_OnKilledByPlayer {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            bool lastTryGetValue = false;
            object lastLocVar = null;
            int setCount = 0;
            string[] setMethods = new[] { "SetDefender", "SetAttacker", "SetAssisted" };

            foreach (var code in instructions) {
                if (code.opcode == OpCodes.Ret && setCount > 0) {
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // damageInfo
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Tracker), "AddKill"));
                }
                yield return code;
                if (code.opcode == OpCodes.Brfalse && lastTryGetValue && setCount < setMethods.Length) {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_name"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_team"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Tracker), setMethods[setCount]));
                    setCount++;
                }
                if (code.opcode == OpCodes.Ldloca || code.opcode == OpCodes.Ldloca_S) {
                    lastLocVar = code.operand;
                }
                lastTryGetValue = code.opcode == OpCodes.Callvirt && ((MemberInfo)code.operand).Name == "TryGetValue";
            }
        }
    }
}
