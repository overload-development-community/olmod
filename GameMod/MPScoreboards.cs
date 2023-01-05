using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace GameMod
{
    class MPScoreboards
    {
        public class Anarchy
        {
            static FieldInfo m_alpha_Field = AccessTools.Field(typeof(UIElement), "m_alpha");

            public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos)
            {
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

            static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false)
            {
                float m_alpha = (float)m_alpha_Field.GetValue(uie);
                uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
                if (!NetworkMatch.m_head_to_head && score)
                    uie.DrawStringSmall(Loc.LS("SCORE"), pos + Vector2.right * (col2 - 100f), 0.4f, StringOffset.CENTER, UIManager.m_col_hi0, 1f, 90f);
                uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                if (!NetworkMatch.m_head_to_head && MPModPrivateData.AssistScoring)
                    uie.DrawStringSmall(Loc.LS("ASSISTS"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                UIManager.DrawSpriteUI(pos + Vector2.right * col5, 0.13f, 0.13f, UIManager.m_col_ui0, m_alpha, 204);
            }

            static void DrawScoresWithoutTeams(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false)
            {
                float m_alpha = (float)m_alpha_Field.GetValue(uie);
                List<Player> players = NetworkManager.m_PlayersForScoreboard;
                List<int> list = new List<int>();
                for (int i = 0; i < players.Count; i++)
                {
                    if (!players[i].m_spectator)
                    {
                        list.Add(i);
                    }
                }
                list.Sort((int a, int b) => ((players[a].m_kills * 3 + players[a].m_assists).CompareTo(players[b].m_kills * 3 + players[b].m_assists) != 0) ? (players[a].m_kills * 3 + players[a].m_assists).CompareTo(players[b].m_kills * 3 + players[b].m_assists) : players[b].m_deaths.CompareTo(players[a].m_deaths));
                list.Reverse();
                for (int j = 0; j < list.Count; j++)
                {
                    Player player = players[list[j]];
                    if (player && !player.m_spectator)
                    {
                        float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                        if (j % 2 == 0)
                        {
                            UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                        }
                        Color c;
                        Color c2;
                        if (player.isLocalPlayer)
                        {
                            UIManager.DrawQuadUI(pos, 410f, 12f, UIManager.m_col_ui0, m_alpha * num * UnityEngine.Random.Range(0.2f, 0.22f), 20);
                            c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f));
                            c2 = UIManager.m_col_hi5;
                            UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                            UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                        }
                        else
                        {
                            c = UIManager.m_col_ui1;
                            c2 = UIManager.m_col_hi1;
                        }
                        UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, c, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                        UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, c, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                        float max_width = col2 - col1 - (float)((!NetworkMatch.m_head_to_head) ? 130 : 10);
                        uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, MPColoredPlayerNames.isActive ? MPColoredPlayerNames.GetPlayerColor(player) * 0.7f : c, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, max_width);
                        if (!NetworkMatch.m_head_to_head)
                        {
                            if (score)
                                uie.DrawDigitsVariable(pos + Vector2.right * (col2 - 100f), player.m_kills * (MPModPrivateData.AssistScoring ? 3 : 1) + (MPModPrivateData.AssistScoring ? player.m_assists : 0), 0.65f, StringOffset.CENTER, c2, m_alpha * num);
                            if (MPModPrivateData.AssistScoring)
                            {
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
        }

        public class TeamAnarchy
        {

            static FieldInfo m_alpha_Field = AccessTools.Field(typeof(UIElement), "m_alpha");

            public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos)
            {
                float col1 = -330f; // Player
                float col2 = 100f;  // Score/Kills
                float col3 = 190f;  // Assists
                float col4 = 280f;  // Deaths
                float col5 = 350f;  // Ping
                MpTeam myTeam = GameManager.m_local_player.m_mp_team;

                foreach (MpTeam team in MPTeams.TeamsByScore)
                {
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

            static void DrawTeamScore(UIElement uie, Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false)
            {
                float m_alpha = (float)m_alpha_Field.GetValue(uie);
                Color c = MPTeams.TeamColor(team, my_team ? 2 : 0);
                Color color = MPTeams.TeamColor(team, my_team ? 4 : 2);
                c.a = m_alpha;
                if (my_team)
                    UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
                UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
                uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, m_alpha);
                uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
            }

            static int DrawScoresForTeam(UIElement uie, MpTeam team, Vector2 pos, float col1, float col2, float col3, float col4, float col5)
            {
                float m_alpha = (float)m_alpha_Field.GetValue(uie);
                List<Player> players = NetworkManager.m_PlayersForScoreboard;
                List<int> list = new List<int>();
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].m_mp_team == team && !players[i].m_spectator)
                    {
                        list.Add(i);
                    }
                }
                list.Sort((int a, int b) =>
                players[a].m_kills != players[b].m_kills
                    ? players[b].m_kills.CompareTo(players[a].m_kills)
                    : (players[a].m_assists != players[b].m_assists ? players[b].m_assists.CompareTo(players[a].m_assists) : players[a].m_deaths.CompareTo(players[b].m_deaths))
                );
                Color color = MPTeams.TeamColor(team, 4);
                Color color2 = MPTeams.TeamColor(team, 1);
                for (int j = 0; j < list.Count; j++)
                {
                    Player player = NetworkManager.m_PlayersForScoreboard[list[j]];
                    if (player && !player.m_spectator)
                    {
                        float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                        if (j % 2 == 0)
                        {
                            UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                        }
                        Color c;
                        if (player.isLocalPlayer)
                        {
                            UIManager.DrawQuadUI(pos, 410f, 12f, color, m_alpha * num * 0.15f, 20);
                            c = color2;
                            UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                            UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, m_alpha * num * 0.5f, 4);
                        }
                        else
                        {
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

            static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false)
            {
                float m_alpha = (float)m_alpha_Field.GetValue(uie);
                uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
                if (score)
                {
                    uie.DrawStringSmall(Loc.LS("SCORE"), pos + Vector2.right * (col2 - 100f), 0.4f, StringOffset.CENTER, UIManager.m_col_hi0, 1f, 90f);
                }
                uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                if (MPModPrivateData.AssistScoring)
                    uie.DrawStringSmall(Loc.LS("ASSISTS"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                UIManager.DrawSpriteUI(pos + Vector2.right * col5, 0.13f, 0.13f, UIManager.m_col_ui0, m_alpha, 204);
            }

            public static bool DrawHUDScoreInfo(UIElement uie, Vector2 pos)
            {
                if (!GameplayManager.IsMultiplayerActive || NetworkMatch.GetMode() == MatchMode.ANARCHY || MPTeams.NetworkMatchTeamCount == 2)
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
                temp_pos.y = temp_pos.y - 20f;
                uie.DrawPing(temp_pos);
                pos.y += 24f;

                MpTeam myTeam = GameManager.m_local_player.m_mp_team;
                foreach (var team in MPTeams.TeamsByScore)
                {
                    MPTeams.DrawTeamScoreSmall(uie, pos, team, NetworkMatch.GetTeamScore(team), 98f, team == myTeam);
                    pos.y += 28f;
                }
                pos.y += 6f - 28f;
                pos.y += 22f;
                pos.x += 100f;
                uie.DrawRecentKillsMP(pos);
                if (GameManager.m_player_ship.m_wheel_select_state == WheelSelectState.QUICK_CHAT)
                {
                    pos.y = UIManager.UI_TOP + 128f;
                    pos.x = -448f;
                    uie.DrawQuickChatWheel(pos);
                }
                else
                {
                    pos.y = UIManager.UI_TOP + 60f;
                    pos.x = UIManager.UI_LEFT + 5f;
                    uie.DrawQuickChatMP(pos);
                }

                return false;
            }
        }

        public class CTF
        {
            static float col1 = -330f;
            static float col2 = -80f;
            static float col3 = -20f;
            static float col4 = 40f;
            static float col5 = 100f;
            static float col6 = 160f;
            static float col7 = 220f;
            static float col8 = 280f;
            static float col9 = 350f;

            public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos)
            {
                int i = 0;
                foreach (var team in MPTeams.TeamsByScore)
                {
                    DrawTeamScore(uie, ref pos, team, NetworkMatch.GetTeamScore(team), 350f, team == GameManager.m_local_player.m_mp_team);
                    pos.y += 35f;
                    // Only draw header for first team in column
                    if (i == 0)
                    {
                        DrawScoreHeader(uie, ref pos, false);
                        pos.y += 15f;
                        uie.DrawVariableSeparator(pos, 350f);
                        pos.y += 20f;
                    }

                    DrawScoresForTeam(uie, team, ref pos);
                    pos.y += 35f;
                    i++;
                }
            }

            static void DrawTeamScore(UIElement uie, ref Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false)
            {
                Color c = MPTeams.TeamColor(team, my_team ? 2 : 0);
                Color color = MPTeams.TeamColor(team, my_team ? 4 : 2);
                c.a = uie.m_alpha;
                if (my_team)
                    UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
                UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
                uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, uie.m_alpha);
                uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
            }

            static void DrawScoreHeader(UIElement uie, ref Vector2 pos, bool score = false)
            {
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

            static int DrawScoresForTeam(UIElement uie, MpTeam team, ref Vector2 pos)
            {
                float m_alpha = (float)AccessTools.Field(typeof(UIElement), "m_alpha").GetValue(uie);
                List<Player> players = NetworkManager.m_PlayersForScoreboard;
                List<int> list = new List<int>();
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].m_mp_team == team && !players[i].m_spectator)
                    {
                        list.Add(i);
                    }
                }
                list.Sort((int a, int b) =>
                    players[a].m_kills != players[b].m_kills
                        ? players[b].m_kills.CompareTo(players[a].m_kills)
                        : (players[a].m_assists != players[b].m_assists ? players[b].m_assists.CompareTo(players[a].m_assists) : players[a].m_deaths.CompareTo(players[b].m_deaths))
                );
                Color color = MPTeams.TeamColor(team, team == GameManager.m_local_player.m_mp_team ? 2 : 0);
                for (int j = 0; j < list.Count; j++)
                {
                    Player player = NetworkManager.m_PlayersForScoreboard[list[j]];

                    GameMod.CTF.CTFStats ctfStats = new GameMod.CTF.CTFStats();

                    if (player && !player.m_spectator)
                    {
                        float num;
                        //float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                        if (player.gameObject.activeInHierarchy)
                        {
                            num = 1f;
                            if (GameMod.CTF.PlayerStats.ContainsKey(player.netId))
                                ctfStats = GameMod.CTF.PlayerStats[player.netId];
                        }
                        else
                        {
                            num = 0.3f;
                            if (GameMod.CTF.NameToID.ContainsKey(player.m_mp_name))
                                ctfStats = GameMod.CTF.PlayerStats[GameMod.CTF.NameToID[player.m_mp_name]];
                        }
                        if (j % 2 == 0)
                        {
                            UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                        }
                        if (player.isLocalPlayer)
                        {
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

            public static bool DrawHUDScoreInfo(UIElement uie, Vector2 pos, float m_alpha)
            {
                pos.x -= 4f;
                pos.y -= 5f;
                pos.x -= 100f;
                pos.y -= 20f;

                pos.x -= 110f;
                pos.y += 20f;

                GameMod.CTF.DrawFlags(uie, pos, m_alpha);

                return true;
            }

        }

        public class Race
        {
            public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos)
            {
                float col = -380f;
                float col2 = -40f;
                float col3 = 100f;
                float col4 = 240f;
                float col5 = 330f;
                float col6 = 400f;
                float col7 = 470f;
                DrawScoreHeader(uie, pos, col, col2, col3, col4, col5, col6, col7, true);
                pos.y += 15f;
                uie.DrawVariableSeparator(pos, 450f);
                pos.y += 20f;
                DrawScoresWithoutTeams(uie, pos, col, col2, col3, col4, col5, col6, col7);
            }

            static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, float col6, float col7, bool score = false)
            {
                uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
                uie.DrawStringSmall(Loc.LS("TOTAL"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall(Loc.LS("BEST"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall(Loc.LS("LAPS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col5, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col6, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                UIManager.DrawSpriteUI(pos + Vector2.right * col7, 0.13f, 0.13f, UIManager.m_col_ui0, uie.m_alpha, 204);
            }

            static void DrawScoresWithoutTeams(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, float col6, float col7)
            {
                for (int j = 0; j < GameMod.Race.Players.Count; j++)
                {
                    Player player = GameMod.Race.Players[j].player;
                    if (player && (!player.m_spectator || GameMod.Race.Players[j].isFinished))
                    {
                        float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                        if (j % 2 == 0)
                        {
                            UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, uie.m_alpha * num * 0.1f, 13);
                        }
                        Color c;
                        Color c2;
                        if (player.isLocalPlayer)
                        {
                            UIManager.DrawQuadUI(pos, 510f, 12f, UIManager.m_col_ui0, uie.m_alpha * num * UnityEngine.Random.Range(0.2f, 0.22f), 20);
                            c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f));
                            c2 = UIManager.m_col_hi5;
                            UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, uie.m_alpha * num * 0.5f, 4);
                            UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, uie.m_alpha * num * 0.5f, 4);
                        }
                        else
                        {
                            c = UIManager.m_col_ui1;
                            c2 = UIManager.m_col_hi1;
                        }
                        UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, c, uie.m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                        UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, c, uie.m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                        float max_width = col2 - col1 - (float)((!NetworkMatch.m_head_to_head) ? 130 : 10);
                        uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, c, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, max_width);

                        var total = TimeSpan.Zero;
                        if (j == 0)
                        {
                            total = TimeSpan.FromSeconds(GameMod.Race.Players[j].Laps.Sum(x => x.Time));
                        }
                        uie.DrawStringSmall(j == 0 ? $"{total.Minutes:0}:{total.Seconds:00}.{total.Milliseconds:000}" : "", pos + Vector2.right * col2, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);

                        var best = TimeSpan.Zero;
                        if (GameMod.Race.Players[j].Laps.Count > 0)
                        {
                            best = TimeSpan.FromSeconds(GameMod.Race.Players[j].Laps.Min(x => x.Time));
                        }
                        uie.DrawStringSmall(GameMod.Race.Players[j].Laps.Count > 0 ? $"{best.Minutes:0}:{best.Seconds:00}.{best.Milliseconds:000}" : "", pos + Vector2.right * col3, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);

                        uie.DrawDigitsVariable(pos + Vector2.right * col4, GameMod.Race.Players[j].Laps.Count(), 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                        uie.DrawDigitsVariable(pos + Vector2.right * col5, player.m_kills, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                        uie.DrawDigitsVariable(pos + Vector2.right * col6, player.m_deaths, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                        c = uie.GetPingColor(player.m_avg_ping_ms);
                        uie.DrawDigitsVariable(pos + Vector2.right * col7, player.m_avg_ping_ms, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                        pos.y += 25f;
                    }
                }
            }

            public static bool DrawHUDScoreInfo(UIElement uie, Vector2 pos, Vector2 temp_pos)
            {
                pos.x -= 4f;
                pos.y -= 5f;
                temp_pos.y = pos.y;
                temp_pos.x = pos.x - 100f;
                uie.DrawStringSmall(NetworkMatch.GetModeString(MatchMode.NUM), temp_pos, 0.4f, StringOffset.LEFT, UIManager.m_col_ub0, 1f, 130f);
                temp_pos.x = pos.x + 95f;
                int match_time_remaining = NetworkMatch.m_match_time_remaining;
                int num3 = (int)NetworkMatch.m_match_elapsed_seconds;
                uie.DrawDigitsTime(temp_pos, (float)match_time_remaining, 0.45f, (num3 <= 10 || match_time_remaining >= 10) ? UIManager.m_col_ui2 : UIManager.m_col_em5, uie.m_alpha, false);
                temp_pos.x = pos.x - 100f;
                temp_pos.y = temp_pos.y - 20f;
                uie.DrawPing(temp_pos);
                pos.y += 24f;

                pos.y -= 12f;
                pos.x += 6f;
                UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ub1, uie.m_alpha, 21);
                pos.y += 10f;
                temp_pos.x = pos.x;
                temp_pos.x = temp_pos.x + 90f;
                for (int i = 0; i < GameMod.Race.Players.Count; i++)
                {
                    temp_pos.y = pos.y;
                    Player player = GameMod.Race.Players[i].player;
                    if (player && (!player.m_spectator || GameMod.Race.Players[i].isFinished))
                    {
                        var rp = GameMod.Race.Players[i];
                        Color color3 = (!player.isLocalPlayer) ? UIManager.m_col_ui1 : UIManager.m_col_hi3;
                        float num4 = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                        uie.DrawDigitsVariable(temp_pos, rp.Laps.Count(), 0.4f, StringOffset.RIGHT, color3, uie.m_alpha * num4);
                        temp_pos.x = temp_pos.x - 35f;
                        uie.DrawStringSmall(player.m_mp_name, temp_pos, 0.35f, StringOffset.RIGHT, color3, num4, -1f);
                        temp_pos.x = temp_pos.x + 8f;
                        if (UIManager.ShouldDrawPlatformId(player.m_mp_platform))
                        {
                            UIManager.DrawSpriteUI(temp_pos, 0.1f, 0.1f, color3, num4 * 0.6f, (int)(226 + player.m_mp_platform));
                        }
                        temp_pos.x = temp_pos.x + 27f;
                        pos.y += 16f;
                    }
                }
                pos.y -= 6f;
                UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ub1, uie.m_alpha, 21);
                pos.x -= 6f;
                pos.y -= 6f;

                pos.y += 22f;
                pos.x += 100f;
                uie.DrawRecentKillsMP(pos);
                if (GameManager.m_player_ship.m_wheel_select_state == WheelSelectState.QUICK_CHAT)
                {
                    pos.y = UIManager.UI_TOP + 128f;
                    pos.x = -448f;
                    uie.DrawQuickChatWheel(pos);
                }
                else
                {
                    pos.y = UIManager.UI_TOP + 60f;
                    pos.x = UIManager.UI_LEFT + 5f;
                    uie.DrawQuickChatMP(pos);
                }

                return false;
            }
        }

        public class Monsterball
        {
            static float col1 = -330f;
            static float col2 = -80f;
            static float col3 = 0f;
            static float col4 = 80f;
            static float col5 = 160f;
            static float col6 = 220f;
            static float col7 = 280f;
            static float col8 = 350f;

            public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos)
            {
                int i = 0;
                foreach (var team in MPTeams.TeamsByScore)
                {
                    DrawTeamScore(uie, ref pos, team, NetworkMatch.GetTeamScore(team), 350f, team == GameManager.m_local_player.m_mp_team);
                    pos.y += 35f;
                    // Only draw header for first team in column
                    if (i == 0)
                    {
                        DrawScoreHeader(uie, ref pos, false);
                        pos.y += 15f;
                        uie.DrawVariableSeparator(pos, 350f);
                        pos.y += 20f;
                    }

                    DrawScoresForTeam(uie, team, ref pos);
                    pos.y += 35f;
                    i++;
                }
            }

            static void DrawTeamScore(UIElement uie, ref Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false)
            {
                Color c = MPTeams.TeamColor(team, my_team ? 2 : 0);
                Color color = MPTeams.TeamColor(team, my_team ? 4 : 2);
                c.a = uie.m_alpha;
                if (my_team)
                    UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
                UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
                uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, uie.m_alpha);
                uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
            }

            static void DrawScoreHeader(UIElement uie, ref Vector2 pos, bool score = false)
            {
                uie.DrawStringSmall("PLAYER", pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
                uie.DrawStringSmall("GOALS", pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                if (MPModPrivateData.AssistScoring)
                    uie.DrawStringSmall("ASSISTS", pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall("BLUNDERS", pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall("K", pos + Vector2.right * col5, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                if (MPModPrivateData.AssistScoring)
                    uie.DrawStringSmall("A", pos + Vector2.right * col6, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                uie.DrawStringSmall("D", pos + Vector2.right * col7, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
                UIManager.DrawSpriteUI(pos + Vector2.right * col8, 0.13f, 0.13f, UIManager.m_col_ui0, uie.m_alpha, 204);
            }

            static int DrawScoresForTeam(UIElement uie, MpTeam team, ref Vector2 pos)
            {
                float m_alpha = (float)AccessTools.Field(typeof(UIElement), "m_alpha").GetValue(uie);
                List<Player> players = NetworkManager.m_PlayersForScoreboard;
                List<int> list = new List<int>();
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].m_mp_team == team && !players[i].m_spectator)
                    {
                        list.Add(i);
                    }
                }
                list.Sort((int a, int b) =>
                    players[a].m_kills != players[b].m_kills
                        ? players[b].m_kills.CompareTo(players[a].m_kills)
                        : (players[a].m_assists != players[b].m_assists ? players[b].m_assists.CompareTo(players[a].m_assists) : players[a].m_deaths.CompareTo(players[b].m_deaths))
                );
                Color color = MPTeams.TeamColor(team, team == GameManager.m_local_player.m_mp_team ? 2 : 0);
                for (int j = 0; j < list.Count; j++)
                {
                    Player player = NetworkManager.m_PlayersForScoreboard[list[j]];
                    var stats = MonsterballAddon.PlayerStats[player.netId];
                    if (player && !player.m_spectator)
                    {
                        float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                        if (j % 2 == 0)
                        {
                            UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, m_alpha * num * 0.1f, 13);
                        }
                        if (player.isLocalPlayer)
                        {
                            UIManager.DrawQuadUI(pos, 410f, 12f, color, m_alpha * num * 0.15f, 20);
                            UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, color, m_alpha * num * 0.5f, 4);
                            UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, color, m_alpha * num * 0.5f, 4);
                        }

                        UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, color, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                        UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, color, m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                        uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, color, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, col2 - col1 - 10f);
                        uie.DrawDigitsVariable(pos + Vector2.right * col2, stats.Goals, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                        if (MPModPrivateData.AssistScoring)
                            uie.DrawDigitsVariable(pos + Vector2.right * col3, stats.GoalAssists, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                        uie.DrawDigitsVariable(pos + Vector2.right * col4, stats.Blunders, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                        uie.DrawDigitsVariable(pos + Vector2.right * col5, player.m_kills, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                        if (MPModPrivateData.AssistScoring)
                            uie.DrawDigitsVariable(pos + Vector2.right * col6, player.m_assists, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                        uie.DrawDigitsVariable(pos + Vector2.right * col7, player.m_deaths, 0.65f, StringOffset.CENTER, color, m_alpha * num);
                        uie.DrawDigitsVariable(pos + Vector2.right * col8, player.m_avg_ping_ms, 0.65f, StringOffset.CENTER, uie.GetPingColor(player.m_avg_ping_ms), m_alpha * num);
                        pos.y += 25f;
                    }
                }
                return list.Count;
            }
        }
    }

    /// <summary>
    /// We no longer care about ranks or game platform
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawPlayerNameBasic")]
    class MPScoreboards_UIElement_DrawPlayerNameBasic
    {
        static bool Prefix(UIElement __instance, Vector2 pos, string s, Color c, int true_rank, float scl = 0.5f, float alpha_scale = 1f, PlayerPlatform pp = PlayerPlatform.PC, float max_width = -1f)
        {
            float m_alpha = (float)AccessTools.Field(typeof(UIElement), "m_alpha").GetValue(__instance);
            float x = pos.x + 14f;
            if (max_width > -1f)
            {
                max_width -= pos.x - x;
            }
            __instance.DrawStringSmall(s, pos, scl, StringOffset.LEFT, c, m_alpha * alpha_scale, max_width);
            float num = UIManager.GetStringWidth(s, scl * 20f, 0, -1);
            if (max_width > -1f)
            {
                num = Mathf.Min(num, max_width);
            }

            return false;
        }
    }

    /// <summary>
    /// By Sirius
    /// </summary>
    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScoreAnarchy")]
    class MPScoreboards_NetworkMatch_GetHighestScoreAnarchy_AssistSwitch
    {
        private static int Postfix(int result)
        {
            // Adjust highest score so kill goals work as expected in no-assist games
            return MPModPrivateData.AssistScoring ? result : (result / 3);
        }
    }

    /// <summary>
    /// By Sirius
    /// </summary>
    [HarmonyPatch(typeof(Player), "RpcAddAssist")]
    class MPScoreboards_Player_RpcAddAssist
    {
        private static bool Prefix()
        {
            // Only track assists if assist scoring is enabled for this game
            return MPModPrivateData.AssistScoring;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpScoreboardRaw")]
    class MPScoreboards_UIElement_DrawMpScoreboardRaw
    {
        static bool Prefix(UIElement __instance, Vector2 pos)
        {
            switch (MPModPrivateData.MatchMode)
            {
                case ExtMatchMode.CTF:
                    MPScoreboards.CTF.DrawMpScoreboardRaw(__instance, ref pos);
                    break;
                case ExtMatchMode.TEAM_ANARCHY:
                    MPScoreboards.TeamAnarchy.DrawMpScoreboardRaw(__instance, ref pos);
                    break;
                case ExtMatchMode.ANARCHY:
                    MPScoreboards.Anarchy.DrawMpScoreboardRaw(__instance, ref pos);
                    break;
                case ExtMatchMode.MONSTERBALL:
                    MPScoreboards.Monsterball.DrawMpScoreboardRaw(__instance, ref pos);
                    break;
                case ExtMatchMode.RACE:
                    MPScoreboards.Race.DrawMpScoreboardRaw(__instance, ref pos);
                    break;
                default:
                    return true;
            }

            return false;
        }
    }


    [HarmonyPatch(typeof(UIElement), "DrawHUDScoreInfo")]
    class MatchModeRace_UIElement_DrawHUDScoreInfo
    {
        static bool Prefix(Vector2 pos, UIElement __instance, Vector2 ___temp_pos, float ___m_alpha)
        {
            switch (MPModPrivateData.MatchMode)
            {
                case ExtMatchMode.CTF:
                    // CTF HUD score only prefix to original
                    return MPScoreboards.CTF.DrawHUDScoreInfo(__instance, pos, ___m_alpha);
                case ExtMatchMode.TEAM_ANARCHY:
                    return MPScoreboards.TeamAnarchy.DrawHUDScoreInfo(__instance, pos);
                case ExtMatchMode.ANARCHY:
                    return true;
                case ExtMatchMode.MONSTERBALL:
                    return true;
                case ExtMatchMode.RACE:
                    return MPScoreboards.Race.DrawHUDScoreInfo(__instance, pos, ___temp_pos);
                default:
                    return true;
            }
        }
    }
}
