using System;
using System.Linq;
using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Scoreboards {
    [Mod(Mods.Scoreboards)]
    public class RaceScoreboard {
        private const float col1 = -380f;
        private const float col2 = -40f;
        private const float col3 = 100f;
        private const float col4 = 240f;
        private const float col5 = 330f;
        private const float col6 = 400f;
        private const float col7 = 470f;

        private static void DrawScoreHeader(UIElement uie, Vector2 pos) {
            uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            uie.DrawStringSmall(Loc.LS("TOTAL"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("BEST"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("LAPS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col5, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col6, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            UIManager.DrawSpriteUI(pos + Vector2.right * col7, 0.13f, 0.13f, UIManager.m_col_ui0, uie.m_alpha, 204);
        }

        private static void DrawScoresWithoutTeams(UIElement uie, Vector2 pos) {
            for (int j = 0; j < GameMod.Race.Players.Count; j++) {
                Player player = GameMod.Race.Players[j].player;
                if (player && (!player.m_spectator || GameMod.Race.Players[j].isFinished)) {
                    float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    if (j % 2 == 0) {
                        UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, uie.m_alpha * num * 0.1f, 13);
                    }
                    Color c;
                    Color c2;
                    if (player.isLocalPlayer) {
                        UIManager.DrawQuadUI(pos, 510f, 12f, UIManager.m_col_ui0, uie.m_alpha * num * UnityEngine.Random.Range(0.2f, 0.22f), 20);
                        c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f));
                        c2 = UIManager.m_col_hi5;
                        UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, uie.m_alpha * num * 0.5f, 4);
                        UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, uie.m_alpha * num * 0.5f, 4);
                    } else {
                        c = UIManager.m_col_ui1;
                        c2 = UIManager.m_col_hi1;
                    }
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, c, uie.m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, c, uie.m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                    float max_width = col2 - col1 - (float)((!NetworkMatch.m_head_to_head) ? 130 : 10);
                    uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, c, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, max_width);

                    var total = TimeSpan.Zero;
                    if (j == 0) {
                        total = TimeSpan.FromSeconds(GameMod.Race.Players[j].Laps.Sum(x => x.Time));
                    }
                    uie.DrawStringSmall(j == 0 ? $"{total.Minutes:0}:{total.Seconds:00}.{total.Milliseconds:000}" : "", pos + Vector2.right * col2, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);

                    var best = TimeSpan.Zero;
                    if (GameMod.Race.Players[j].Laps.Count > 0) {
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

        public static void DrawMpScoreboardRaw(UIElement uie, ref Vector2 pos) {
            DrawScoreHeader(uie, pos);
            pos.y += 15f;
            uie.DrawVariableSeparator(pos, 450f);
            pos.y += 20f;
            DrawScoresWithoutTeams(uie, pos);
        }

        public static bool DrawHUDScoreInfo(UIElement uie, Vector2 pos, Vector2 temp_pos) {
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
            temp_pos.y -= 20f;
            uie.DrawPing(temp_pos);
            pos.y += 24f;

            pos.y -= 12f;
            pos.x += 6f;
            UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ub1, uie.m_alpha, 21);
            pos.y += 10f;
            temp_pos.x = pos.x;
            temp_pos.x += 90f;
            for (int i = 0; i < GameMod.Race.Players.Count; i++) {
                temp_pos.y = pos.y;
                Player player = GameMod.Race.Players[i].player;
                if (player && (!player.m_spectator || GameMod.Race.Players[i].isFinished)) {
                    var rp = GameMod.Race.Players[i];
                    Color color3 = (!player.isLocalPlayer) ? UIManager.m_col_ui1 : UIManager.m_col_hi3;
                    float num4 = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    uie.DrawDigitsVariable(temp_pos, rp.Laps.Count(), 0.4f, StringOffset.RIGHT, color3, uie.m_alpha * num4);
                    temp_pos.x -= 35f;
                    uie.DrawStringSmall(player.m_mp_name, temp_pos, 0.35f, StringOffset.RIGHT, color3, num4, -1f);
                    temp_pos.x += 8f;
                    if (UIManager.ShouldDrawPlatformId(player.m_mp_platform)) {
                        UIManager.DrawSpriteUI(temp_pos, 0.1f, 0.1f, color3, num4 * 0.6f, (int)(226 + player.m_mp_platform));
                    }
                    temp_pos.x += 27f;
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
