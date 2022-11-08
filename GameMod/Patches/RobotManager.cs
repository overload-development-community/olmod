using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches {
    /// <summary>
    /// Resets the basic powerup spawn frequency table.
    /// </summary>
    [Mod(Mods.BasicPowerupSpawns)]
    [HarmonyPatch(typeof(RobotManager), "ClearMultiplayerModeLists")]
    public static class RobotManager_ClearMultiplayerModeLists {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Postfix() {
            BasicPowerupSpawns.m_multiplayer_spawnable_powerups.Clear();
            BasicPowerupSpawns.m_multi_powerup_frequency = 0f;
        }
    }

    /// <summary>
    /// Adds the $basic and $basic_frequency tags, and set LancerPlayers if it's included in the multiplayer mode file.
    /// </summary>
    /// <remarks>
    /// $basic tags include HEALTH, AMMO, ENERGY, and ALIENORB.  The third value indicates the probability that the basic powerup you indicate will spawn when a basic powerup should spawn.
    /// $basic_frequency indicates the base time in seconds that a basic powerup will spawn.  This base time is affected by the game's set spawn frequency and if there are more than 10 powerups currently spawned in.
    /// $lancer_players declares the minimum number of players required to spawn the lancer in a multiplayer game.
    /// </remarks>
    /// <example>
    /// $basic;HEALTH;0.1
    /// $basic;AMMO;0.2
    /// $basic;ENERGY;0.3
    /// $basic;ALIENORB;0.4
    /// $basic_frequency;15
    /// $lancer_players;4
    /// </example>
    [Mod(new Mods[] { Mods.BasicPowerupSpawns, Mods.PrimarySpawns })]
    [HarmonyPatch(typeof(RobotManager), "ParseTagMultiplayer")]
    public static class RobotManager_ParseTagMultiplayer {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        [Mod(new Mods[] { Mods.BasicPowerupSpawns, Mods.PrimarySpawns })]
        public static void ProcessExtendedMultiplayerTags(string[] words) {
            switch (words[0]) {
                case "$basic":
                    int num2 = (int)AccessTools.Method(typeof(RobotManager), "ParseName").Invoke(null, new object[] { typeof(BasicPowerupSpawns.PowerupType), words[1] });
                    if (num2 != -1) {
                        BasicPowerupSpawns.MultiplayerSpawnablePowerup item;
                        item.type = num2;
                        item.percent = 0.5f;
                        if (words.Length == 3) {
                            item.percent = words[2].ToFloat();
                        } else {
                            Debug.Log("No percent set for powerup " + words[1] + ", setting to 50%");
                        }
                        BasicPowerupSpawns.m_multiplayer_spawnable_powerups.Add(item);
                    }
                    return;
                case "$basic_frequency":
                    if (words.Length == 2) {
                        BasicPowerupSpawns.m_multi_powerup_frequency = words[1].ToFloat();
                    } else {
                        Debug.Log("Invalid number of arguments to powerup_frequency.  Must be 1, is " + (words.Length - 1));
                    }
                    return;
                case "$lancer_players":
                    try {
                        if (words.Length == 2) {
                            PrimarySpawns.LancerPlayers = (int)words[1].ToFloat();
                        } else {
                            Debug.Log("No count set for lancer players, ignoring.  Format is, for example, \"$lancer_players;4\"");
                        }
                    } catch (Exception) {
                        Debug.Log("Error setting $lancer_players.  Format is, for example, \"$lancer_players;4\"");
                    }
                    break;
            }
        }

        [Mod(Mods.BasicPowerupSpawns)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = instructions.ToList();
            int removeStart = -1;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "Unrecognized tag: ")
                    removeStart = i;
            }

            if (removeStart >= 0) {
                var lbls = codes[removeStart].labels;
                codes.RemoveRange(removeStart, codes.Count - removeStart - 1);
                codes.InsertRange(removeStart, new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_0) { labels = lbls },
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RobotManager_ParseTagMultiplayer), "ProcessExtendedMultiplayerTags"))
                });
            }

            return codes;
        }
    }

    /// <summary>
    /// Initialize LancerPlayers to 0 before reading the multiplayer mode file.
    /// </summary>
    [Mod(Mods.PrimarySpawns)]
    [HarmonyPatch(typeof(RobotManager), "ReadMultiplayerModeFile")]
    public static class RobotManager_ReadMultiplayerModeFile {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Prefix() {
            PrimarySpawns.LancerPlayers = 0;
        }
    }

    /// <summary>
    /// Reads the robotdata if it exists.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(RobotManager), "ReadPresetData")]
    public static class RobotManager_ReadPresetData {
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
    public static class RobotManager_TriggerInRelevantSegment {
        public static void Postfix(ref bool __result) {
            if (GameplayManager.IsMultiplayerActive) {
                __result = true;
            }
        }
    }
}
