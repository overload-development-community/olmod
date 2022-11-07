using System.Collections.Generic;
using System.Reflection;
using GameMod.Metadata;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Scoreboards {
    [Mod(Mods.Scoreboards)]
    public static class AnarchyScoreboard {
        private static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false) {
            float m_alpha = uie.m_alpha;

            uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            if (!NetworkMatch.m_head_to_head && score)
                uie.DrawStringSmall(Loc.LS("SCORE"), pos + Vector2.right * (col2 - 100f), 0.4f, StringOffset.CENTER, UIManager.m_col_hi0, 1f, 90f);
            uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            if (!NetworkMatch.m_head_to_head && MPModPrivateData.AssistScoring)
                uie.DrawStringSmall(Loc.LS("ASSISTS"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            UIManager.DrawSpriteUI(pos + Vector2.right * col5, 0.13f, 0.13f, UIManager.m_col_ui0, m_alpha, 204);
        }

        private static void DrawScoresWithoutTeams(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false) {
            float m_alpha = uie.m_alpha;

            List<Player> players = NetworkManager.m_PlayersForScoreboard;
            List<int> list = new List<int>();
            for (int i = 0; i < players.Count; i++) {
                if (!players[i].m_spectator) {
                    list.Add(i);
                }
            }
            list.Sort((int a, int b) => ((players[a].m_kills * 3 + players[a].m_assists).CompareTo(players[b].m_kills * 3 + players[b].m_assists) != 0) ? (players[a].m_kills * 3 + players[a].m_assists).CompareTo(players[b].m_kills * 3 + players[b].m_assists) : players[b].m_deaths.CompareTo(players[a].m_deaths));
            list.Reverse();
            for (int j = 0; j < list.Count; j++) {
                Player player = players[list[j]];
                if (player && !player.m_spectator) {
                    float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    if (j % 2 == 0) {
                        UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                    }
                    Color c;
                    Color c2;
                    if (player.isLocalPlayer) {
                        UIManager.DrawQuadUI(pos, 410f, 12f, UIManager.m_col_ui0, m_alpha * num * UnityEngine.Random.Range(0.2f, 0.22f), 20);
                        c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f));
                        c2 = UIManager.m_col_hi5;
                        UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                        UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                    } else {
                        c = UIManager.m_col_ui1;
                        c2 = UIManager.m_col_hi1;
                    }
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, c, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, c, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                    float max_width = col2 - col1 - (float)((!NetworkMatch.m_head_to_head) ? 130 : 10);
                    uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, MPColoredPlayerNames.isActive ? MPColoredPlayerNames.GetPlayerColor(player) * 0.7f : c, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, max_width);
                    if (!NetworkMatch.m_head_to_head) {
                        if (score)
                            uie.DrawDigitsVariable(pos + Vector2.right * (col2 - 100f), player.m_kills * (MPModPrivateData.AssistScoring ? 3 : 1) + (MPModPrivateData.AssistScoring ? player.m_assists : 0), 0.65f, StringOffset.CENTER, c2, m_alpha * num);
                        if (MPModPrivateData.AssistScoring) {
                            uie.DrawDigitsVariable(pos + Vector2.right * col3, player.m_assists, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                        }
                    }
                    uie.DrawDigitsVariable(pos + Vector2.right * col2, player.m_kills, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col4, player.m_deaths, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    c = uie.GetPingColor(player.m_avg_ping_ms);
                    uie.DrawDigitsVariable(pos + Vector2.right * col5, player.m_avg_ping_ms, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    pos.y += 25f;
                }
            }
        }

        public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos) {
            float col1 = -330f; // Player
            float col2 = 100f; // Score/Kills
            float col3 = 190f; // Assists
            float col4 = (!NetworkMatch.m_head_to_head) ? 280f : 220f; // Deaths
            float col5 = 350f; // Ping
            DrawScoreHeader(uie, pos, col1, col2, col3, col4, col5, true);
            pos.y += 15f;
            uie.DrawVariableSeparator(pos, 350f);
            pos.y += 20f;
            DrawScoresWithoutTeams(uie, pos, col1, col2, col3, col4, col5, true);
        }
    }
}
