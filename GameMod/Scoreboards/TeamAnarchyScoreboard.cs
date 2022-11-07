using System.Collections.Generic;
using System.Reflection;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Scoreboards {
    [Mod(Mods.Scoreboards)]
    public static class TeamAnarchyScoreboard {

        private static readonly FieldInfo m_alpha_Field = AccessTools.Field(typeof(UIElement), "m_alpha");

        private static void DrawTeamScore(UIElement uie, Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false) {
            float m_alpha = (float)m_alpha_Field.GetValue(uie);
            Color c = Teams.TeamColor(team, my_team ? 2 : 0);
            Color color = Teams.TeamColor(team, my_team ? 4 : 2);
            c.a = m_alpha;
            if (my_team)
                UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
            UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
            uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, m_alpha);
            uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
        }

        private static int DrawScoresForTeam(UIElement uie, MpTeam team, Vector2 pos, float col1, float col2, float col3, float col4, float col5) {
            float m_alpha = (float)m_alpha_Field.GetValue(uie);
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
            Color color = Teams.TeamColor(team, 4);
            Color color2 = Teams.TeamColor(team, 1);
            for (int j = 0; j < list.Count; j++) {
                Player player = NetworkManager.m_PlayersForScoreboard[list[j]];
                if (player && !player.m_spectator) {
                    float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    if (j % 2 == 0) {
                        UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                    }
                    Color c;
                    if (player.isLocalPlayer) {
                        UIManager.DrawQuadUI(pos, 410f, 12f, color, m_alpha * num * 0.15f, 20);
                        c = color2;
                        UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                        UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                    } else {
                        c = color;
                    }
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, c, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, c, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                    uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, c, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, col2 - col1 - 10f);
                    uie.DrawDigitsVariable(pos + Vector2.right * col2, player.m_kills, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    if (MPModPrivateData.AssistScoring)
                        uie.DrawDigitsVariable(pos + Vector2.right * col3, player.m_assists, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col4, player.m_deaths, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    c = uie.GetPingColor(player.m_avg_ping_ms);
                    uie.DrawDigitsVariable(pos + Vector2.right * col5, player.m_avg_ping_ms, 0.65f, StringOffset.CENTER, c, m_alpha * num);
                    pos.y += 25f;
                }
            }
            return list.Count;
        }

        private static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false) {
            float m_alpha = (float)m_alpha_Field.GetValue(uie);
            uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            if (score) {
                uie.DrawStringSmall(Loc.LS("SCORE"), pos + Vector2.right * (col2 - 100f), 0.4f, StringOffset.CENTER, UIManager.m_col_hi0, 1f, 90f);
            }
            uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            if (MPModPrivateData.AssistScoring)
                uie.DrawStringSmall(Loc.LS("ASSISTS"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            UIManager.DrawSpriteUI(pos + Vector2.right * col5, 0.13f, 0.13f, UIManager.m_col_ui0, m_alpha, 204);
        }

        public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos) {
            float col1 = -330f; // Player
            float col2 = 100f;  // Score/Kills
            float col3 = 190f;  // Assists
            float col4 = 280f;  // Deaths
            float col5 = 350f;  // Ping
            MpTeam myTeam = GameManager.m_local_player.m_mp_team;

            foreach (MpTeam team in Teams.TeamsByScore) {
                DrawTeamScore(uie, pos, team, NetworkMatch.GetTeamScore(team), 350f, GameManager.m_local_player.m_mp_team == myTeam);
                pos.y += 35f;
                DrawScoreHeader(uie, pos, col1, col2, col3, col4, col5);
                pos.y += 15f;
                uie.DrawVariableSeparator(pos, 350f);
                pos.y += 20f;
                int num = DrawScoresForTeam(uie, team, pos, col1, col2, col3, col4, col5);
                pos.y += (float)num * 25f + 50f;
            }
        }

        public static bool DrawHUDScoreInfo(UIElement uie, Vector2 pos) {
            if (!GameplayManager.IsMultiplayerActive || NetworkMatch.GetMode() == MatchMode.ANARCHY || Teams.NetworkMatchTeamCount == 2)
                return true;

            pos.x -= 4f;
            pos.y -= 5f;
            Vector2 temp_pos;
            temp_pos.y = pos.y;
            temp_pos.x = pos.x - 100f;
            uie.DrawStringSmall(NetworkMatch.GetModeString(MatchMode.NUM), temp_pos, 0.4f, StringOffset.LEFT, UIManager.m_col_ub0, 1f, 130f);
            temp_pos.x = pos.x + 95f;
            int match_time_remaining = NetworkMatch.m_match_time_remaining;
            int num3 = (int)NetworkMatch.m_match_elapsed_seconds;
            uie.DrawDigitsTime(temp_pos, (float)match_time_remaining, 0.45f,
                (num3 <= 10 || match_time_remaining >= 10) ? UIManager.m_col_ui2 : UIManager.m_col_em5, uie.m_alpha, false);
            temp_pos.x = pos.x - 100f;
            temp_pos.y -= 20f;
            uie.DrawPing(temp_pos);
            pos.y += 24f;

            MpTeam myTeam = GameManager.m_local_player.m_mp_team;
            foreach (var team in Teams.TeamsByScore) {
                Teams.DrawTeamScoreSmall(uie, pos, team, NetworkMatch.GetTeamScore(team), 98f, team == myTeam);
                pos.y += 28f;
            }
            pos.y += 6f - 28f;
            pos.y += 22f;
            pos.x += 100f;
            uie.DrawRecentKillsMP(pos);
            if (GameManager.m_player_ship.m_wheel_select_state == WheelSelectState.QUICK_CHAT) {
                pos.y = UIManager.UI_TOP + 128f;
                pos.x = -448f;
                uie.DrawQuickChatWheel(pos);
            } else {
                pos.y = UIManager.UI_TOP + 60f;
                pos.x = UIManager.UI_LEFT + 5f;
                uie.DrawQuickChatMP(pos);
            }

            return false;
        }
    }
}
