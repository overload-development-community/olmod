﻿using GameMod.Objects;
using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    /// <summary>
    ///  Goal:    making it easier to tell apart players in anarchy games
    ///             Adjustments:
    ///                 1. Bigger names
    ///                 2. Colored names
    ///                 
    ///  Author:  luponix 
    ///  Created: 2021-09-28
    /// </summary>
    class MPColoredPlayerNames
    {
        public static bool isActive = false;
        public const float player_name_scale = 1.1f;

        [HarmonyPatch(typeof(UIManager), "DrawMpPlayerName")]
        class MPPlayerDistinction_UIManager_DrawMpPlayerName
        {

            static bool Prefix(Player player, ref Vector2 offset)
            {
                if (NetworkMatch.GetMode() == MatchMode.ANARCHY && isActive)
                {
                    Color c = GetPlayerColor(player);
                    c.a = player.m_mp_data.vis_fade;
                    offset.y -= 2.2f;
                    UIManager.DrawStringAlignCenter(player.m_mp_name, offset, player_name_scale, c, -1f);
                    if (UIManager.ShouldDrawPlatformId(player.m_mp_platform))
                    {
                        Vector2 position = offset;
                        position.x += UIManager.GetStringWidth(player.m_mp_name, 0.8f, 0, -1) * 0.5f + 1f;
                        UIManager.DrawSpriteUI(position, 0.011f, 0.011f, c, player.m_mp_data.vis_fade, (int)(226 + player.m_mp_platform));
                    }
                    if (player.m_mp_status != string.Empty)
                    {
                        offset.y -= 2.2f;
                        UIManager.DrawStringAlignCenter("[" + Loc.LS(player.m_mp_status) + "]", offset, 0.8f, c, -1f);
                    }
                    return false;
                }
                return true;
            }
        }

        // color the player names in the killfeed
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(UIElement), "DrawRecentKillsMP")]
        class MPPlayerDistinction_UIElement_DrawRecentKillsMP
        {
            public static Color MaybeReplaceVictimColor(Color original, int i)
            {
                return !(isActive && NetworkMatch.GetMode() == MatchMode.ANARCHY) ? original : GetPlayerColorByName(GameplayManager.RecentKillNames[i]) * 0.75f;
            }

            public static Color MaybeReplaceKillerColor(Color original, int i)
            {
                return !(isActive && NetworkMatch.GetMode() == MatchMode.ANARCHY) ? original : GetPlayerColorByName(GameplayManager.RecentKillerNames[i]) * 0.75f;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int count = 0;
                foreach (var code in instructions)
                {
                    yield return code;
                    if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Teams), "GetMessageColor"))//AccessTools.Method(typeof(UIElement), AccessTools.Method(typeof(UIElement), "GetMessageColor"))
                    {
                        if(count == 0)
                        {
                            yield return new CodeInstruction(OpCodes.Ldloc_S, 5);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPPlayerDistinction_UIElement_DrawRecentKillsMP), "MaybeReplaceVictimColor"));
                        }
                        if(count == 1)
                        {
                            yield return new CodeInstruction(OpCodes.Ldloc_S, 5);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPPlayerDistinction_UIElement_DrawRecentKillsMP), "MaybeReplaceKillerColor"));
                        }
                        count++;
                    }
                }
            }
        }


        /*
        [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
        class MPPlayerDistinction_UIElement_DrawMpMiniScoreboard
        {
            public static void DrawStringSmallProxy(UIElement uie, string player_name,  Vector2 pos, float scale, StringOffset so, Color color, float num2, float num3)//, Player player)
            {
                if (NetworkMatch.GetMode() == MatchMode.ANARCHY && PilotManager.ActivePilot.ToUpper().Equals("[DIM] BEN"))
                    color = Color.white;//GetPlayerColor(player);
                uConsole.Log("we got executed");
                uie.DrawStringSmall(player_name, pos, scale, so, Color.white, num2, num3);
            }
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool found = false;
                foreach (var code in instructions)
                {
                    if (!found && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawStringSmall"))
                    {
                        found = true;
                        //yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPPlayerDistinction_UIElement_DrawMpMiniScoreboard), "DrawStringSmallProxy"));
                        continue;
                    }
                    yield return code;
                }
            }
        }*/


        public static Color[] m_colors =
        {
            new Color(133f,0f,0f)/255,
            new Color(255f,215f,0f)/255,
            new Color(0f,0f,255f)/255,
            new Color(255f,69f,0f)/255,

            new Color(37f,220f,252f)/255,
            new Color(255f,0f,255f)/255,
            new Color(255f,0f,0f)/255,
            new Color(240f,230f,140f)/255,

            new Color(65f,105f,225f)/255,
            new Color(255f,165f,0f)/255,
            new Color(0f,100f,0f)/255,
            new Color(50f,205f,50f)/255,

            new Color(233f,56f,109f)/255,
            new Color(105f,105f,105f)/255,
            new Color(0f,128f,128f)/255,
            new Color(255f,255f,255f)/255
        };


        public static string[] color_lookup = new string[16];

        public static Color GetPlayerColor(Player player)
        {
            return GetPlayerColorByName(player.m_mp_name);
        }

        public static Color GetPlayerColorByName(string player_name)
        {
            if (player_name.StartsWith("*"))
            {
                player_name = player_name.Substring(1);
            }
            for (int i = 0; i < 16; i++)
            {
                if (color_lookup[i] != null && color_lookup[i].Equals(player_name))
                    return m_colors[i];
            }
            // add the player to the color_lookup array
            uint pos = ((uint)player_name.GetHashCode()) % 16;
            int count = 0;
            while (color_lookup[pos] != null && count <= 16)
            {
                pos = (pos + 1) % 16;
                count++;
            }
            color_lookup[pos] = player_name;
            return m_colors[pos];
        }

        [HarmonyPatch(typeof(GameplayManager), "LoadLevel")]
        class MPPlayerDistinction_GameplayManager_LoadLevel
        {
            public static void Postfix()
            {
                color_lookup = new string[16];
            }
        }

        /* // Descents approach of assigning by order of entering the game
         *  public static Color GetPlayerColor(Player player)
        {
            int index = NetworkManager.m_Players.IndexOf(player);
            if (index != -1 && index < 16) return m_colors[index];
            else
            {
                uConsole.Log("GetPlayerColor: invalid index: " + index);
                return m_colors[16];
            }
        }
         */
    }
}
