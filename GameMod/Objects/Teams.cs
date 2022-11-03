using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    /// <summary>
    /// Handles team games with more than 2 teams and handles the team colors.
    /// </summary>
    [Mod(Mods.Teams)]
    public static class Teams {
        public static int NetworkMatchTeamCount;
        public static int MenuManagerTeamCount;

        public const int Min = 2;
        public const int Max = 8;

        private static readonly float[] colors = { 0.08f, 0.16f, 0.32f, 0.51f, 0.62f, 0.71f, 0.91f, 0.002f, 0.6f };
        private static readonly int[] colorIdx = { 4, 0, 2, 3, 5, 6, 7, 8 };
        public static readonly MpTeam[] AllTeams = {
            MpTeam.TEAM0,
            MpTeam.TEAM1,
            MpTeam.NUM_TEAMS,
            MpTeam.NUM_TEAMS + 1,
            MpTeam.NUM_TEAMS + 2,
            MpTeam.NUM_TEAMS + 3,
            MpTeam.NUM_TEAMS + 4,
            MpTeam.NUM_TEAMS + 5
        };
        public static readonly MpTeam MPTEAM_NUM = MpTeam.NUM_TEAMS + 6;
        private static readonly int[] teamIndexList = { 0, 1, -1, 2, 3, 4, 5, 6, 7 };
        private static readonly int[] teamMessageColorIndexList = { 2, 3, 5, 6, 7, 8, 9, 10 };

        private static bool IsMyTeam(MpTeam team) {
            if (MenuManager.m_menu_state == MenuState.MP_PRE_MATCH_MENU && NetworkMatch.m_players.ContainsKey((int)GameManager.m_local_player.netId.Value))
                return team == NetworkMatch.m_players[(int)GameManager.m_local_player.netId.Value].m_team;

            return team == GameManager.m_local_player.m_mp_team;
        }

        // This processes when team != TEAM0
        public static int TeamMessageColor(MpTeam team) {
            return teamMessageColorIndexList[(int)team > 1 ? (int)team - 1 : (int)team];
        }

        public static MpTeam GetMpTeamFromMessageColor(int messageColorIndex) {
            return AllTeams[Array.IndexOf(teamMessageColorIndexList, messageColorIndex)];
        }

        public static Color GetMessageColor(MpMessageColor mpmc, float flash) {
            switch (mpmc) {
                case MpMessageColor.LOCAL:
                    return Color.Lerp(UIManager.m_col_hi2, UIManager.m_col_hi7, flash);
                case MpMessageColor.ANARCHY:
                case MpMessageColor.NONE:
                    return Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_ui3, flash);
                default:
                    Color c1 = TeamColor(GetMpTeamFromMessageColor((int)mpmc), 0);
                    Color c2 = TeamColor(GetMpTeamFromMessageColor((int)mpmc), 0);
                    return Color.Lerp(c1, c2, flash);
            }
        }

        public static int TeamNum(MpTeam team) {
            return teamIndexList[(int)team];
        }

        public static IEnumerable<MpTeam> GetTeams {
            get {
                return AllTeams.Take(NetworkMatchTeamCount);
            }
        }

        public static IEnumerable<MpTeam> ActiveTeams {
            get {
                int[] team_counts = new int[(int)MPTEAM_NUM];
                foreach (var player in Overload.NetworkManager.m_PlayersForScoreboard)
                    if (!player.m_spectator)
                        team_counts[(int)player.m_mp_team]++;
                foreach (var team in AllTeams) {
                    var idx = teamIndexList[(int)team];
                    if (team_counts[(int)team] > 0 ||
                        (idx < NetworkMatch.m_team_scores.Length && NetworkMatch.m_team_scores[idx] != 0))
                        yield return team;
                }
            }
        }

        public static MpTeam[] TeamsByScore {
            get {
                var teams = GetTeams.ToArray();
                Array.Sort<MpTeam>(teams, new Comparison<MpTeam>((i1, i2) => {
                    var n = NetworkMatch.m_team_scores[(int)i2].CompareTo(NetworkMatch.m_team_scores[(int)i1]);
                    return n == 0 ? i1.CompareTo(i2) : n;
                }));
                return teams;
            }
        }

        public static MpTeam NextTeam(MpTeam team) {
            team++;
            if (team == MpTeam.ANARCHY)
                team++;
            return team == MPTEAM_NUM || AllTeams.IndexOf(x => x == team) >= NetworkMatchTeamCount ? MpTeam.TEAM0 : team;
        }

        public static string TeamName(MpTeam team) {
            var c = MenuManager.mpc_decal_color;
            var cIdx = colorIdx[TeamNum(team)];
            if (NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default) {
                cIdx = IsMyTeam(team) ? Menus.mms_team_color_self : Menus.mms_team_color_enemy;
            }
            MenuManager.mpc_decal_color = cIdx;
            var ret = MenuManager.GetMpDecalColor();
            MenuManager.mpc_decal_color = c;
            return ret;
        }

        public static string TeamNameNotLocalized(MpTeam team) {
            var cIdx = colorIdx[TeamNum(team)];
            if (NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default) {
                cIdx = IsMyTeam(team) ? Menus.mms_team_color_self : Menus.mms_team_color_enemy;
            }

            switch (cIdx) {
                case 0:
                    return "ORANGE";
                case 1:
                    return "YELLOW";
                case 2:
                    return "GREEN";
                case 3:
                    return "AQUA";
                case 4:
                    return "BLUE";
                case 5:
                    return "PURPLE";
                case 6:
                    return "PINK";
                case 7:
                    return "RED";
                case 8:
                    return "WHITE";
                case 9:
                    return "BLACK";
                case 10:
                    return "GRAY";
                default:
                    return "UNKNOWN";
            };
        }

        public static string ColorName(int index) {
            var c = MenuManager.mpc_decal_color;
            MenuManager.mpc_decal_color = index;
            var ret = MenuManager.GetMpDecalColor();
            MenuManager.mpc_decal_color = c;
            return ret;
        }

        public static int TeamColorIdx(MpTeam team) {
            if (NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default)
                return IsMyTeam(team) ? Menus.mms_team_color_self : Menus.mms_team_color_enemy;

            return colorIdx[TeamNum(team)];
        }

        public static Color TeamColor(MpTeam team, int mod) {
            return TeamColorByIndex(TeamColorIdx(team), mod);
        }

        public static Color TeamColorByIndex(int cIdx, int mod) {
            float sat = cIdx == 8 ? 0.01f : cIdx == 4 && mod == 5 ? 0.6f : 0.95f - mod * 0.05f;
            float bright = mod == 5 ? 0.95f : 0.5f + mod * 0.1f;
            Color c = HSBColor.ConvertToColor(colors[cIdx], sat, bright);
            return c;
        }

        public static void DrawTeamHeader(UIElement uie, Vector2 pos, MpTeam team, float w = 255f) {
            Color c = TeamColor(team, 1);
            Color c2 = TeamColor(team, 4);
            c.a = uie.m_alpha;
            string teamName = NetworkMatch.GetTeamName(team);
            if (NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default) {
                c = c2 = UIManager.m_col_ui0;
                teamName = $"{Loc.LS("TEAM")} {(int)team + 1}";
            }

            UIManager.DrawQuadBarHorizontal(pos, 13f, 13f, w * 2f, c, 7);
            uie.DrawStringSmall(teamName, pos, 0.6f, StringOffset.CENTER, c2, 1f, -1f);
        }

        public static void DrawTeamScoreSmall(UIElement uie, Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false) {
            Color c = TeamColor(team, my_team ? 2 : 0);
            Color color = TeamColor(team, my_team ? 4 : 2);
            c.a = uie.m_alpha;
            if (my_team)
                UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
            UIManager.DrawQuadBarHorizontal(pos, 12f, 12f, w * 2f, c, 7);
            uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.55f, StringOffset.RIGHT, color, uie.m_alpha);
            uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.5f, StringOffset.LEFT, color, 1f, -1f);
        }

        public static void DrawTeamScore(UIElement uie, Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false) {
            Color c = TeamColor(team, my_team ? 2 : 0);
            Color color = TeamColor(team, my_team ? 4 : 2);
            c.a = uie.m_alpha;
            if (my_team)
                UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
            UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
            uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, uie.m_alpha);
            uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
        }

        private static readonly MethodInfo _UIElement_DrawMpScoreboardRaw_Method = typeof(UIElement).GetMethod("DrawMpScoreboardRaw", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void DrawPostgame(UIElement uie) {
            Vector2 pos = Vector2.zero;
            pos.y = -300f;
            Color c = UIManager.m_col_ui5;
            string s = Loc.LS("MATCH OVER!");
            var teams = TeamsByScore;
            if (teams.Length >= 2 && NetworkMatch.m_team_scores[(int)teams[0]] != NetworkMatch.m_team_scores[(int)teams[1]]) {
                c = TeamColor(teams[0], 4);
                s = TeamName(teams[0]) + " WINS!";
            }
            float a = uie.m_alpha * uie.m_alpha * uie.m_alpha;
            uie.DrawWideBox(pos, 300f, 29f, c, a, 7);
            uie.DrawWideBox(pos, 300f, 25f, c, a, 11);
            uie.DrawStringSmall(s, pos, 1.35f, StringOffset.CENTER, UIManager.m_col_ub3, uie.m_alpha, -1f);
            pos.y = -200f;
            _UIElement_DrawMpScoreboardRaw_Method.Invoke(uie, new object[] { pos });
            pos.y = -290f;
            pos.x = 610f;
            uie.DrawStringSmall(NetworkMatch.GetModeString(MatchMode.NUM), pos, 0.75f, StringOffset.RIGHT, UIManager.m_col_ui5, 1f, -1f);
            pos.y += 25f;
            uie.DrawStringSmall(GameplayManager.Level.DisplayName, pos, 0.5f, StringOffset.RIGHT, UIManager.m_col_ui1, 1f, -1f);
            if (GameManager.m_player_ship.m_wheel_select_state == WheelSelectState.QUICK_CHAT) {
                pos.y = UIManager.UI_TOP + 158f;
                pos.x = -418f;
                uie.DrawQuickChatWheel(pos);
            } else {
                pos.y = UIManager.UI_TOP + 90f;
                pos.x = UIManager.UI_LEFT + 35f;
                uie.DrawQuickChatMP(pos);
            }
        }

        public static int HighestScore() {
            int max_score = 0;
            foreach (var team in GetTeams) {
                int score = NetworkMatch.GetTeamScore(team);
                if (score > max_score)
                    max_score = score;
            }
            return max_score;
        }

        // Support function for DrawMpPostgame transpiles
        public static void ShowTeamWinner(ref Color c, ref string s) {
            var teams = TeamsByScore;
            if (teams.Length >= 2 && NetworkMatch.m_team_scores[(int)teams[0]] != NetworkMatch.m_team_scores[(int)teams[1]]) {
                c = TeamColor(teams[0], 4);
                s = TeamName(teams[0]) + " WINS!";
            }
        }

        public static void UpdateClientColors() {
            if (GameplayManager.IsMultiplayerActive) {
                // Update ship colors
                foreach (var ps in UnityEngine.Object.FindObjectsOfType<PlayerShip>()) {
                    ps.UpdateShipColors(ps.c_player.m_mp_team, -1, -1, -1);
                    ps.UpdateRimColor(true);
                }

                // Update CTF flag/carrier colors
                if (CTF.IsActive) {
                    for (int i = 0; i < CTF.FlagObjs.Count; i++) {
                        CTF.UpdateFlagColor(CTF.FlagObjs[i], i);
                    }
                    foreach (var player in Overload.NetworkManager.m_Players) {
                        CTF.UpdateShipEffects(player);
                    }
                }
            }
            UIManager.InitMpNames();
        }

        public static IEnumerable<CodeInstruction> ChangeTeamColorLoad(IEnumerator<CodeInstruction> codes, OpCode mod) {
            // current team already loaded
            yield return new CodeInstruction(mod);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Teams), "TeamColor"));
            // skip until store
            var labels = new List<Label>();
            while (codes.MoveNext() && codes.Current.opcode != OpCodes.Stloc_S && codes.Current.opcode != OpCodes.Stloc_0)
                if (codes.Current.labels.Count() != 0) {
                    //var ncode = new CodeInstruction(OpCodes.Nop);
                    labels.AddRange(codes.Current.labels);
                    //yield return ncode;
                }
            // do store color
            codes.Current.labels.AddRange(labels);
            yield return codes.Current;
        }
    }
}
