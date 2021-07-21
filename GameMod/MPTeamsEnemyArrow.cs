using System;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(UIManager), "DrawMpPlayerArrow")]
    internal partial class MPTeamsEnemyArrow
    {
        //In team games, display a red arrow below enemy names.
        private static void Postfix(Player player, Vector2 offset)
        {
            float a = (player.m_mp_team != GameManager.m_local_player.m_mp_team) ? (1f * player.m_mp_data.vis_fade) : 1f;
            float rotation = 4.712389f;

            if (player.m_mp_team != GameManager.m_local_player.m_mp_team)
            {
                Vector2 redOffset = new Vector2(0f, -0.8f);
                offset += redOffset;
                UIManager.DrawSpriteUIRotated(offset, 0.015f, 0.015f, rotation, Color.red, a, 81);
            }
        }
    }
}
