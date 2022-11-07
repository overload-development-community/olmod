using System.Collections.Generic;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Scoreboards {
    [Mod(Mods.Scoreboards)]
    public static class CTFScoreboard {
        private const float col1 = -330f;
        private const float col2 = -80f;
        private const float col3 = -20f;
        private const float col4 = 40f;
        private const float col5 = 100f;
        private const float col6 = 160f;
        private const float col7 = 220f;
        private const float col8 = 280f;
        private const float col9 = 350f;

        private static void DrawTeamScore(UIElement uie, ref Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false) {
            Color c = Teams.TeamColor(team, my_team ? 2 : 0);
            Color color = Teams.TeamColor(team, my_team ? 4 : 2);
            c.a = uie.m_alpha;
            if (my_team)
                UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
            UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
            uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, uie.m_alpha);
            uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
        }

        private static void DrawScoreHeader(UIElement uie, ref Vector2 pos) {
            uie.DrawStringSmall("PLAYER", pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            uie.DrawStringSmall("CAPT", pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall("PU", pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall("CK", pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall("RET", pos + Vector2.right * col5, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall("K", pos + Vector2.right * col6, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            if (MPModPrivateData.AssistScoring)
                uie.DrawStringSmall("A", pos + Vector2.right * col7, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall("D", pos + Vector2.right * col8, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            UIManager.DrawSpriteUI(pos + Vector2.right * col9, 0.13f, 0.13f, UIManager.m_col_ui0, uie.m_alpha, 204);
        }

        private static int DrawScoresForTeam(UIElement uie, MpTeam team, ref Vector2 pos) {
            float m_alpha = (float)AccessTools.Field(typeof(UIElement), "m_alpha").GetValue(uie);
            List<Player> players = NetworkManager.m_PlayersForScoreboard;
            List<int> list = new List<int>();
            for (int i = 0; i < players.Count; i++) {
                if (players[i].m_mp_team == team && !players[i].m_spectator) {
                    list.Add(i);
                }
            }
            list.Sort((int a, int b) =>
                players[a].m_kills != players[b].m_kills
                    ? players[b].m_kills.CompareTo(players[a].m_kills)
                    : (players[a].m_assists != players[b].m_assists ? players[b].m_assists.CompareTo(players[a].m_assists) : players[a].m_deaths.CompareTo(players[b].m_deaths))
            );
            Color color = Teams.TeamColor(team, team == GameManager.m_local_player.m_mp_team ? 2 : 0);
            for (int j = 0; j < list.Count; j++) {
                Player player = NetworkManager.m_PlayersForScoreboard[list[j]];

                GameMod.CTF.CTFStats ctfStats = new GameMod.CTF.CTFStats();
                if (GameMod.CTF.PlayerStats.ContainsKey(player.netId))
                    ctfStats = GameMod.CTF.PlayerStats[player.netId];

                if (player && !player.m_spectator) {
                    float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    if (j % 2 == 0) {
                        UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                    }
                    if (player.isLocalPlayer) {
                        UIManager.DrawQuadUI(pos, 410f, 12f, color, m_alpha * num * 0.15f, 20);
                        UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, color, m_alpha * num * 0.5f, 4);
                        UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, color, m_alpha * num * 0.5f, 4);
                    }

                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, color, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, color, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                    uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, color, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, col2 - col1 - 10f);
                    uie.DrawDigitsVariable(pos + Vector2.right * col2, ctfStats.Captures, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col3, ctfStats.Pickups, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col4, ctfStats.CarrierKills, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col5, ctfStats.Returns, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col6, player.m_kills, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    if (MPModPrivateData.AssistScoring)
                        uie.DrawDigitsVariable(pos + Vector2.right * col7, player.m_assists, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col8, player.m_deaths, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col9, player.m_avg_ping_ms, 0.65f, StringOffset.CENTER, uie.GetPingColor(player.m_avg_ping_ms), m_alpha * num);
                    pos.y += 25f;
                }
            }
            return list.Count;
        }

        public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos) {
            int i = 0;
            foreach (var team in Teams.TeamsByScore) {
                DrawTeamScore(uie, ref pos, team, NetworkMatch.GetTeamScore(team), 350f, team == GameManager.m_local_player.m_mp_team);
                pos.y += 35f;
                // Only draw header for first team in column
                if (i == 0) {
                    DrawScoreHeader(uie, ref pos);
                    pos.y += 15f;
                    uie.DrawVariableSeparator(pos, 350f);
                    pos.y += 20f;
                }

                DrawScoresForTeam(uie, team, ref pos);
                pos.y += 35f;
                i++;
            }
        }

        public static bool DrawHUDScoreInfo(UIElement uie, Vector2 pos, float m_alpha) {
            pos.x -= (4f + 100f + 110f);
            pos.y -= (5f + 20f + 20f);

            GameMod.CTF.DrawFlags(uie, pos, m_alpha);

            return true;
        }
    }
}
