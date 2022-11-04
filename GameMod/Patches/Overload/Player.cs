using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Increases the loop wait time for the energy center sounds. There is a noticeable performance hit when charging.
    /// </summary>
    [Mod(Mods.EnergyCenterPerformance)]
    [HarmonyPatch(typeof(Player), "AddEnergyDefault")]
    public static class Player_AddEnergyDefault {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            bool state = false;
            foreach (var code in codes) {
                if (!state && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 0.1f) {
                    code.operand = 0.5f;
                    state = true;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Sets the minimium XP required for modifiers to 0.
    /// </summary>
    [Mod(Mods.UnlockModifiers)]
    [HarmonyPatch(typeof(Player), "GetModifierMinXP")]
    public static class MPUnlockAllModifiers {
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
    public static class Player_OnKilledByPlayer {
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

    /// <summary>
    /// Does a better job of initializing playership state at spawn, resetting the flak/cyclone fire counter, the thunderbolt power level, and clearing the boost overheat.
    /// </summary>
    [Mod(Mods.SpawnInitialization)]
    [HarmonyPatch(typeof(Player), "RestorePlayerShipDataAfterRespawn")]
    public static class Player_RestorePlayerShipDataAfterRespawn {
        private static readonly FieldInfo _PlayerShip_flak_fire_count_Field = typeof(PlayerShip).GetField("flak_fire_count", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Prefix(Player __instance) {
            _PlayerShip_flak_fire_count_Field.SetValue(__instance.c_player_ship, 0);
            __instance.c_player_ship.m_thunder_power = 0;
            __instance.c_player_ship.m_boost_heat = 0;
            __instance.c_player_ship.m_boost_overheat_timer = 0f;
        }
    }

    /// <summary>
    /// Skip the portion in Player.UpdateInvul() where ship movement reduces your invuln time. 
    /// </summary>
    [Mod(Mods.SpawnInvulnerability)]
    [HarmonyPatch(typeof(Player), "UpdateInvul")]
    public static class Player_UpdateInvul {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Stloc_1) {
                    state++;
                    if (state == 2)
                        code.opcode = OpCodes.Pop;
                }

                yield return code;
            }
        }
    }
}
