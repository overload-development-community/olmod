using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    public static class MPTags
    {
        public struct MultiplayerSpawnablePowerup
        {
            public int type;
            public float percent;
        }

        public enum PowerupType
        {
            HEALTH,
            ENERGY,
            AMMO,
            ALIENORB,
            NUM
        }

        public static List<MultiplayerSpawnablePowerup> m_multiplayer_spawnable_powerups = new List<MultiplayerSpawnablePowerup>();
        public static float m_multi_powerup_frequency = 0f;
    }

    [HarmonyPatch(typeof(RobotManager), "ClearMultiplayerModeLists")]
    class MPTags_RobotManager_ClearMultiplayerModeLists
    {
        static void Postfix()
        {
            MPTags.m_multiplayer_spawnable_powerups.Clear();
            MPTags.m_multi_powerup_frequency = 0f;
        }
    }

    [HarmonyPatch(typeof(RobotManager), "ParseTagMultiplayer")]
    class MPTags_RobotManager_ParseTagMultiplayer
    {

        static void ProcessExtendedMultiplayerTags(string[] words)
        {
            switch (words[0])
            {
                case "$basic":
                    int num2 = (int)AccessTools.Method(typeof(RobotManager), "ParseName").Invoke(null, new object[] { typeof(MPTags.PowerupType), words[1] });
                    if (num2 != -1)
                    {
                        MPTags.MultiplayerSpawnablePowerup item;
                        item.type = num2;
                        item.percent = 0.5f;
                        if (words.Length == 3)
                        {
                            item.percent = words[2].ToFloat();
                        }
                        else
                        {
                            Debug.Log("No percent set for powerup " + words[1] + ", setting to 50%");
                        }
                        MPTags.m_multiplayer_spawnable_powerups.Add(item);
                    }
                    return;
                case "$basic_frequency":
                    if (words.Length == 2)
                    {
                        MPTags.m_multi_powerup_frequency = words[1].ToFloat();
                    }
                    else
                    {
                        Debug.Log("Invalid number of arguments to powerup_frequency.  Must be 1, is " + (words.Length - 1));
                    }
                    return;
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int removeStart = -1;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "Unrecognized tag: ")
                    removeStart = i;
            }

            if (removeStart >= 0)
            {
                var lbls = codes[removeStart].labels;
                codes.RemoveRange(removeStart, codes.Count - removeStart - 1);
                codes.InsertRange(removeStart, new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_0) { labels = lbls },
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTags_RobotManager_ParseTagMultiplayer), "ProcessExtendedMultiplayerTags"))
                });
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "MaybeSpawnPowerup")]
    public class MPTags_NetworkMatch_MaybeSpawnPowerup
    {

        static ItemPrefab PowerupTypeToPrefab(MPTags.PowerupType pt)
        {
            switch (pt)
            {
                case MPTags.PowerupType.HEALTH:
                    return ItemPrefab.entity_item_shields;
                case MPTags.PowerupType.ENERGY:
                    return ItemPrefab.entity_item_energy;
                case MPTags.PowerupType.AMMO:
                    return ItemPrefab.entity_item_ammo;
                case MPTags.PowerupType.ALIENORB:
                    return ItemPrefab.entity_item_alien_orb;
                default:
                    return ItemPrefab.none;
            }
        }

        static ItemPrefab RandomBasicSpawn()
        {
            if (MPTags.m_multiplayer_spawnable_powerups.Count > 0)
            {
                // Somewhat ugly but consistent/based on NetworkMatch.RandomAllowedMissileSpawn()
                // New algorithm
                float[] array = new float[4];
                for (int i = 0; i < MPTags.m_multiplayer_spawnable_powerups.Count; i++)
                {
                    MPTags.PowerupType type = (MPTags.PowerupType)MPTags.m_multiplayer_spawnable_powerups[i].type;
                    array[(int)type] = MPTags.m_multiplayer_spawnable_powerups[i].percent;
                }
                float num = 0f;
                for (int j = 0; j < 4; j++)
                {
                    num += array[j];
                }

                if (num > 0f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        array[k] /= num;
                    }
                    float num2 = UnityEngine.Random.Range(0f, 1f);
                    float num3 = 0f;
                    for (int l = 0; l < 4; l++)
                    {
                        if (num2 < num3 + array[l])
                        {
                            return PowerupTypeToPrefab((MPTags.PowerupType)l);
                        }
                        num3 += array[l];
                    }
                    Debug.Log("We had valid powerups, but couldn't choose one when spawning it");
                    return ItemPrefab.num;
                }
            }
            else
            {
                // Original algorithm
                int num = UnityEngine.Random.Range(0, 4);
                if (NetworkMatch.AnyPlayersHaveAmmoWeapons())
                {
                    num = UnityEngine.Random.Range(0, 5);
                }
                switch (num)
                {
                    case 1:
                    case 2:
                        return ItemPrefab.entity_item_energy;
                    case 3:
                    case 4:
                        return ItemPrefab.entity_item_ammo;
                    default:
                        return ItemPrefab.entity_item_shields;
                }
            }

            return ItemPrefab.num;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int m_spawn_basic_timer_index = -1;
            int removeStart = -1;
            int removeEnd = -1;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].operand == AccessTools.Field(typeof(NetworkMatch), "m_spawn_basic_timer"))
                    m_spawn_basic_timer_index++;

                if (codes[i].opcode == OpCodes.Ldc_I4_0 && m_spawn_basic_timer_index == 3)
                {
                    removeStart = i;
                    m_spawn_basic_timer_index = -3;
                }

                if (codes[i].operand == AccessTools.Method(typeof(NetworkMatch), "SetSpawnBasicTimer"))
                {
                    removeEnd = i;
                }
            }

            if (removeStart >= 0 && removeEnd >= 0)
            {
                codes.RemoveRange(removeStart, removeEnd - removeStart - 1);
                codes.InsertRange(removeStart, new List<CodeInstruction>() {
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTags_NetworkMatch_MaybeSpawnPowerup), "RandomBasicSpawn")),
                    new CodeInstruction(OpCodes.Stloc_2),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldc_I4_0)
                });
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SetSpawnBasicTimer")]
    class MPTags_NetworkMatch_SetSpawnBasicTimer
    {
        static bool Prefix()
        {
            if (MPTags.m_multi_powerup_frequency <= 0f)
                return true;

            float num = Mathf.Max(1f, MPTags.m_multi_powerup_frequency);
            int count = NetworkMatch.m_players.Count;
            num *= NetworkMatch.GetNumPlayerSpawnModifier(count);
            int count2 = RobotManager.m_master_item_list.Count;
            if (count2 > 10)
            {
                num += (float)(count2 - 10) * 0.5f;
            }
            num *= NetworkMatch.GetSpawnMultiplier();
            AccessTools.Field(typeof(NetworkMatch), "m_spawn_basic_timer").SetValue(null, num);
            return false;
        }
    }
}
