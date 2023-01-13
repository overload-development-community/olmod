using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    static class MPTeams
    {
        public static int NetworkMatchTeamCount;
        public static int MenuManagerTeamCount;
        public static readonly int Min = 2;
        public static readonly int Max = 8;
        private static readonly float[] colors = { 0.08f, 0.16f, 0.32f, 0.51f, 0.62f, 0.71f, 0.91f, 0.002f, 0.6f };
        private static readonly int[] colorIdx = { 4, 0, 2, 3, 5, 6, 7, 8 };
        public static readonly MpTeam[] AllTeams = { MpTeam.TEAM0, MpTeam.TEAM1,
            MpTeam.NUM_TEAMS, MpTeam.NUM_TEAMS + 1, MpTeam.NUM_TEAMS + 2, MpTeam.NUM_TEAMS + 3,
            MpTeam.NUM_TEAMS + 4, MpTeam.NUM_TEAMS + 5 };
        public static readonly MpTeam MPTEAM_NUM = MpTeam.NUM_TEAMS + 6;
        private static readonly int[] teamIndexList = { 0, 1, -1, 2, 3, 4, 5, 6, 7 };
        private static readonly int[] teamMessageColorIndexList = { 2, 3, 5, 6, 7, 8, 9, 10 };

        // This processes when team != TEAM0
        public static int TeamMessageColor(MpTeam team)
        {
            return teamMessageColorIndexList[(int)team > 1 ? (int)team - 1 : (int)team];
        }

        public static MpTeam GetMpTeamFromMessageColor(int messageColorIndex)
        {
            return AllTeams[Array.IndexOf(teamMessageColorIndexList, messageColorIndex)];
        }

        public static int TeamNum(MpTeam team)
        {
            return teamIndexList[(int)team];
        }

        public static IEnumerable<MpTeam> Teams
        {
            get
            {
                return AllTeams.Take(NetworkMatchTeamCount);
            }
        }

        public static IEnumerable<MpTeam> ActiveTeams
        {
            get
            {
                int[] team_counts = new int[(int)MPTeams.MPTEAM_NUM];
                foreach (var player in Overload.NetworkManager.m_PlayersForScoreboard)
                    if (!player.m_spectator)
                        team_counts[(int)player.m_mp_team]++;
                foreach (var team in AllTeams)
                {
                    var idx = teamIndexList[(int)team];
                    if (team_counts[(int)team] > 0 ||
                        (idx < NetworkMatch.m_team_scores.Length && NetworkMatch.m_team_scores[idx] != 0))
                        yield return team;
                }
            }
        }

        public static MpTeam[] TeamsByScore
        {
            get
            {
                var teams = Teams.ToArray();
                Array.Sort<MpTeam>(teams, new Comparison<MpTeam>((i1, i2) =>
                {
                    var n = NetworkMatch.m_team_scores[(int)i2].CompareTo(NetworkMatch.m_team_scores[(int)i1]);
                    return n == 0 ? i1.CompareTo(i2) : n;
                }));
                return teams;
            }
        }

        public static MpTeam NextTeam(MpTeam team)
        {
            team = team + 1;
            if (team == MpTeam.ANARCHY)
                team = team + 1;
            return team == MPTEAM_NUM || AllTeams.IndexOf(x => x == team) >= NetworkMatchTeamCount ? MpTeam.TEAM0 : team;
        }

        public static bool IsMyTeam(MpTeam team)
        {
            if (MenuManager.m_menu_state == MenuState.MP_PRE_MATCH_MENU && NetworkMatch.m_players.ContainsKey((int)GameManager.m_local_player.netId.Value))
                return team == NetworkMatch.m_players[(int)GameManager.m_local_player.netId.Value].m_team;

            return team == GameManager.m_local_player.m_mp_team;
        }

        public static string TeamName(MpTeam team)
        {
            var c = MenuManager.mpc_decal_color;
            var cIdx = colorIdx[TeamNum(team)];
            if (MPTeams.NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default)
            {
                cIdx = IsMyTeam(team) ? Menus.mms_team_color_self : Menus.mms_team_color_enemy;
            }
            MenuManager.mpc_decal_color = cIdx;
            var ret = MenuManager.GetMpDecalColor();
            MenuManager.mpc_decal_color = c;
            return ret;
        }

        public static string TeamNameNotLocalized(MpTeam team)
        {
            var c = MenuManager.mpc_decal_color;
            var cIdx = colorIdx[TeamNum(team)];
            if (MPTeams.NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default)
            {
                cIdx = IsMyTeam(team) ? Menus.mms_team_color_self : Menus.mms_team_color_enemy;
            }

            switch (cIdx)
            {
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

        public static string ColorName(int index)
        {
            var c = MenuManager.mpc_decal_color;
            MenuManager.mpc_decal_color = index;
            var ret = MenuManager.GetMpDecalColor();
            MenuManager.mpc_decal_color = c;
            return ret;
        }

        public static int TeamColorIdx(MpTeam team)
        {
            if (MPTeams.NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default)
                return IsMyTeam(team) ? Menus.mms_team_color_self : Menus.mms_team_color_enemy;

            return colorIdx[TeamNum(team)];
        }

        public static Color TeamColor(MpTeam team, int mod)
        {
            return TeamColorByIndex(TeamColorIdx(team), mod);
        }

        public static Color TeamColorByIndex(int cIdx, int mod)
        {
            float sat = cIdx == 8 ? 0.01f : cIdx == 4 && mod == 5 ? 0.6f : 0.95f - mod * 0.05f;
            float bright = mod == 5 ? 0.95f : 0.5f + mod * 0.1f;
            Color c = HSBColor.ConvertToColor(colors[cIdx], sat, bright);
            return c;
        }

        public static void DrawTeamHeader(UIElement uie, Vector2 pos, MpTeam team, float w = 255f)
        {
            Color c = TeamColor(team, 1);
            Color c2 = TeamColor(team, 4);
            c.a = uie.m_alpha;
            string teamName = NetworkMatch.GetTeamName(team);
            if (MPTeams.NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS && !Menus.mms_team_color_default)
            {
                c = c2 = UIManager.m_col_ui0;
                teamName = $"{Loc.LS("TEAM")} {(int)team + 1}";
            }

            UIManager.DrawQuadBarHorizontal(pos, 13f, 13f, w * 2f, c, 7);
            uie.DrawStringSmall(teamName, pos, 0.6f, StringOffset.CENTER, c2, 1f, -1f);
        }

        public static void DrawLobby(UIElement uie, Vector2 pos)
        {
            float name_offset = -250f;
            float highlight_width = 285f;
            float org_x = pos.x;
            int max_row_count = NetworkMatch.GetMaxPlayersForMatch() + MPTeams.NetworkMatchTeamCount;
            int cur_row_count = NetworkMatch.m_players.Count() + MPTeams.NetworkMatchTeamCount;
            bool split = max_row_count > 10;
            if (split)
            {
                pos.x -= 300f;
                pos.y += 50f + 24f;
            }
            float org_y = pos.y;
            float first_y = org_y;
            int rows_per_col = split ? (cur_row_count + 1) / 2 : cur_row_count;
            int row_num = 0;
            foreach (var team in Teams)
            {
                if (row_num >= rows_per_col)
                {
                    first_y = pos.y;
                    pos.x += 300f * 2;
                    pos.y = org_y;
                    rows_per_col = cur_row_count; // no more split
                    row_num = 0;
                }
                DrawTeamHeader(uie, pos, team, 255f);
                pos.y += 24f;
                row_num++;
                int num = 0;
                foreach (var value in NetworkMatch.m_players.Values)
                {
                    if (value.m_team == team)
                    {
                        uie.DrawPlayerName(pos, value, num % 2 == 0, highlight_width, name_offset, -1f);
                        pos.y += 20f;
                        num++;
                        row_num++;
                    }
                }
                pos.y += 10f;
            }
            pos.y = Mathf.Max(first_y, pos.y) + 10f;
            pos.x = org_x;
            if (MenuManager.m_menu_micro_state != 2 && MenuManager.m_mp_private_match)
            {
                float alpha_mod = (MenuManager.m_mp_cst_timer > 0f) ? 0.2f : 1f;
                uie.DrawStringSmall(ScriptTutorialMessage.ControlString(CCInput.MENU_DELETE) + " - " + Loc.LS("CHANGE TEAMS"), pos, 0.45f, StringOffset.CENTER, UIManager.m_col_ui0, alpha_mod, -1f);
            }
        }

        public static void DrawTeamScoreSmall(UIElement uie, Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false)
        {
            Color c = TeamColor(team, my_team ? 2 : 0);
            Color color = TeamColor(team, my_team ? 4 : 2);
            c.a = uie.m_alpha;
            if (my_team)
                UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
            UIManager.DrawQuadBarHorizontal(pos, 12f, 12f, w * 2f, c, 7);
            uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.55f, StringOffset.RIGHT, color, uie.m_alpha);
            uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.5f, StringOffset.LEFT, color, 1f, -1f);
        }

        public static void DrawTeamScore(UIElement uie, Vector2 pos, MpTeam team, int score, float w = 350f, bool my_team = false)
        {
            Color c = TeamColor(team, my_team ? 2 : 0);
            Color color = TeamColor(team, my_team ? 4 : 2);
            c.a = uie.m_alpha;
            if (my_team)
                UIManager.DrawQuadBarHorizontal(pos, 18f, 18f, w * 2f, c, 7);
            UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, w * 2f, c, 7);
            uie.DrawDigitsVariable(pos + Vector2.right * w, score, 0.7f, StringOffset.RIGHT, color, uie.m_alpha);
            uie.DrawStringSmall(NetworkMatch.GetTeamName(team), pos - Vector2.right * (w + 9f), 0.6f, StringOffset.LEFT, color, 1f, -1f);
        }

        private static MethodInfo _UIElement_DrawScoresForTeam_Method = AccessTools.Method(typeof(UIElement), "DrawScoresForTeam");
        public static int DrawScoresForTeam(UIElement uie, MpTeam team, Vector2 pos, float col1, float col2, float col3, float col4, float col5)
        {
            return (int)_UIElement_DrawScoresForTeam_Method.Invoke(uie,
                new object[] { team, pos, col1, col2, col3, col4, col5 });
        }

        private static MethodInfo _UIElement_DrawScoreHeader_Method = AccessTools.Method(typeof(UIElement), "DrawScoreHeader");
        public static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, bool score = false)
        {
            _UIElement_DrawScoreHeader_Method.Invoke(uie, new object[] { pos, col1, col2, col3, col4, col5, score });
            return;
        }

        private static MethodInfo _UIElement_DrawMpScoreboardRaw_Method = typeof(UIElement).GetMethod("DrawMpScoreboardRaw", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void DrawPostgame(UIElement uie)
        {
            Vector2 pos = Vector2.zero;
            pos.y = -300f;
            Color c = UIManager.m_col_ui5;
            string s = Loc.LS("MATCH OVER!");
            var teams = TeamsByScore;
            if (teams.Length >= 2 && NetworkMatch.m_team_scores[(int)teams[0]] != NetworkMatch.m_team_scores[(int)teams[1]])
            {
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
            if (GameManager.m_player_ship.m_wheel_select_state == WheelSelectState.QUICK_CHAT)
            {
                pos.y = UIManager.UI_TOP + 158f;
                pos.x = -418f;
                uie.DrawQuickChatWheel(pos);
            }
            else
            {
                pos.y = UIManager.UI_TOP + 90f;
                pos.x = UIManager.UI_LEFT + 35f;
                uie.DrawQuickChatMP(pos);
            }
        }

        public static int HighestScore()
        {
            int max_score = 0;
            foreach (var team in Teams)
            {
                int score = NetworkMatch.GetTeamScore(team);
                if (score > max_score)
                    max_score = score;
            }
            return max_score;
        }

        public static void SetPlayerGlow(PlayerShip ship, MpTeam team)
        {
            if (GameplayManager.IsMultiplayerActive && !GameplayManager.IsDedicatedServer() && NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
            {
                var teamcolor = UIManager.ChooseMpColor(team);
                foreach (var mat in ship.m_materials)
                {
                    // Main damage color
                    if (mat.shader != null)
                    {
                        if ((Color)mat.GetVector("_color_burn") == teamcolor)
                        {
                            return;
                        }
                        mat.SetVector("_color_burn", teamcolor);
                    }

                    // Light color (e.g. TB overcharge)
                    ship.c_lights[4].color = teamcolor;
                }
            }
        }

        // Support function for DrawMpPostgame transpiles
        private static void ShowTeamWinner(ref Color c, ref string s)
        {
            var teams = MPTeams.TeamsByScore;
            if (teams.Length >= 2 && NetworkMatch.m_team_scores[(int)teams[0]] != NetworkMatch.m_team_scores[(int)teams[1]])
            {
                c = MPTeams.TeamColor(teams[0], 4);
                s = MPTeams.TeamName(teams[0]) + " WINS!";
            }
        }

        public class ChangeTeamMessage : MessageBase
        {
            public NetworkInstanceId netId;
            public MpTeam newTeam;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(netId);
                writer.WritePackedUInt32((uint)newTeam);
            }
            public override void Deserialize(NetworkReader reader)
            {
                netId = reader.ReadNetworkId();
                newTeam = (MpTeam)reader.ReadPackedUInt32();
            }
        }

        public static void UpdateClientColors()
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                // Update ship colors
                foreach (var ps in UnityEngine.Object.FindObjectsOfType<PlayerShip>())
                {
                    ps.UpdateShipColors(ps.c_player.m_mp_team, -1, -1, -1);
                    ps.UpdateRimColor(true);
                }

                // Update CTF flag/carrier colors
                if (CTF.IsActive)
                {
                    for (int i = 0; i < CTF.FlagObjs.Count; i++)
                    {
                        CTF.UpdateFlagColor(CTF.FlagObjs[i], i);
                    }
                    foreach (var player in Overload.NetworkManager.m_Players)
                    {
                        CTF.UpdateShipEffects(player);
                    }
                }
            }
            UIManager.InitMpNames();
        }

    }

    [HarmonyPatch(typeof(UIElement), "MaybeDrawPlayerList")]
    static class MPTeamsDraw
    {
        static bool Prefix(UIElement __instance, Vector2 pos)
        {
            if (!MenuManager.mp_display_player_list ||
                ((!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) || MPTeams.NetworkMatchTeamCount == 2))) // &&
                //NetworkMatch.GetMaxPlayersForMatch() <= 8))
                return true;
            MPTeams.DrawLobby(__instance, pos);
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpScoreboardRaw")]
    static class MPTeamsScore
    {
        static bool Prefix(UIElement __instance, ref Vector2 pos)
        {
            var mode = NetworkMatch.GetMode();
            var fitSingle = MPTeams.NetworkMatchTeamCount == 2 && NetworkMatch.m_players.Count <= 8;
            if (MPModPrivateData.MatchMode == ExtMatchMode.RACE)
                return true;
            if (mode == MatchMode.ANARCHY || ((mode == MatchMode.TEAM_ANARCHY || mode == MatchMode.MONSTERBALL) && fitSingle))
                return true;

            float colReduce = fitSingle ? 0 : 50f;
            float col1 = fitSingle ? -330f : -250f;
            float col2 = 100f - colReduce;
            float col3 = 190f - colReduce;
            float col4 = 280f - colReduce;
            float col5 = 350f - colReduce;

            MpTeam myTeam = GameManager.m_local_player.m_mp_team;
            int col = 0;
            float x = pos.x;
            float y = pos.y;
            float[] ys = new float[2] { pos.y, pos.y };
            foreach (var team in MPTeams.TeamsByScore)
            {
                pos.x = x + (fitSingle ? 0 : col == 0 ? -325f : 325f);
                pos.y = ys[col];
                MPTeams.DrawTeamScore(__instance, pos, team, NetworkMatch.GetTeamScore(team), col5, team == myTeam);
                pos.y += 35f;
                if (ys[col] == y || fitSingle) // only draw header for first team in column
                {
                    MPTeams.DrawScoreHeader(__instance, pos, col1, col2, col3, col4, col5, false);
                    pos.y += 15f;
                    __instance.DrawVariableSeparator(pos, 350f);
                    pos.y += 20f;
                }
                int num = MPTeams.DrawScoresForTeam(__instance, team, pos, col1, col2, col3, col4, col5);
                pos.y += (float)num * 25f + 35f;
                ys[col] = pos.y;
                if (!fitSingle)
                    col = 1 - col;
            }
            pos.y = Mathf.Max(ys[0], ys[1]);
            return false;
        }
    }

    // shown on death
    [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
    static class MPTeamsMiniScore
    {
        static bool Prefix(UIElement __instance, ref Vector2 pos)
        {
            if (MPModPrivateData.MatchMode == ExtMatchMode.RACE)
            {
                Race.DrawMpMiniScoreboard(ref pos, __instance);
                return false;
            }

            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || MPTeams.NetworkMatchTeamCount == 2)
                return true;

            int match_time_remaining = NetworkMatch.m_match_time_remaining;
            int match_time = (int)NetworkMatch.m_match_elapsed_seconds;
            pos.y -= 15f;
            __instance.DrawDigitsTime(pos + Vector2.right * 95f, (float)match_time_remaining, 0.45f,
                (match_time <= 10 || match_time_remaining >= 10) ? UIManager.m_col_ui2 : UIManager.m_col_em5,
                __instance.m_alpha, false);
            pos.y -= 3f;

            MpTeam myTeam = GameManager.m_local_player.m_mp_team;
            foreach (var team in MPTeams.TeamsByScore)
            {
                if (!Overload.NetworkManager.m_PlayersForScoreboard.Any(x => x.m_mp_team == team))
                    continue;

                pos.y += 28f;
                int score = NetworkMatch.GetTeamScore(team);
                MPTeams.DrawTeamScoreSmall(__instance, pos, team, score, 98f, team == myTeam);
            }
            pos.y += 6f;
            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "GetTeamName")]
    static class MPTeamsName
    {
        static bool Prefix(MpTeam team, ref string __result)
        {
            __result = MPTeams.TeamName(team);
            return false;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "GetMpTeamName")]
    static class MPTeamsNameMenu
    {
        static bool Prefix(MpTeam team, ref string __result)
        {
            if (!GameplayManager.IsMultiplayerActive)
            {
                switch (team)
                {
                    case MpTeam.TEAM0:
                        __result = Loc.LS("BLUE TEAM / TEAM 1");
                        break;
                    case MpTeam.TEAM1:
                        __result = Loc.LS("ORANGE TEAM / TEAM 2");
                        break;
                    case MpTeam.ANARCHY:
                        __result = Loc.LS("ANARCHY");
                        break;
                    default:
                        __result = Loc.LS("UNKNOWN");
                        break;
                }
            }
            else
            {
                __result = MPTeams.TeamName(team) + " TEAM";
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "Init")]
    static class MPTeamsInit
    {
        static void Prefix()
        {
            NetworkMatch.m_team_scores = new int[(int)MPTeams.MPTEAM_NUM];
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    static class MPTeamsStartPlaying
    {
        static void Prefix()
        {
            for (int i = 0, l = NetworkMatch.m_team_scores.Length; i < l; i++)
                NetworkMatch.m_team_scores[i] = 0;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    static class MPTeamsInitBeforeEachMatch
    {
        static void Prefix()
        {
            for (int i = 0, l = NetworkMatch.m_team_scores.Length; i < l; i++)
                NetworkMatch.m_team_scores[i] = 0;
        }
    }

    // team balancing for new player
    [HarmonyPatch(typeof(NetworkMatch), "NetSystemGetTeamForPlayer")]
    static class MPTeamsForPlayer
    {
        static bool Prefix(ref MpTeam __result, int connection_id)
        {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
            {
                __result = MpTeam.ANARCHY;
                return false;
            }
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || (MPTeams.NetworkMatchTeamCount == 2 &&
                !MPJoinInProgress.NetworkMatchEnabled)) // use this simple balancing method for JIP to hopefully solve JIP team imbalances
                return true;
            if (NetworkMatch.m_players.TryGetValue(connection_id, out var connPlayer)) // keep team if player already exists (when called from OnUpdateGameSession)
            {
                __result = connPlayer.m_team;
                return false;
            }
            int[] team_counts = new int[(int)MPTeams.MPTEAM_NUM];
            foreach (var player in NetworkMatch.m_players.Values)
                team_counts[(int)player.m_team]++;
            MpTeam min_team = MpTeam.TEAM0;
            foreach (var team in MPTeams.Teams)
                if (team_counts[(int)team] < team_counts[(int)min_team] ||
                    (team_counts[(int)team] == team_counts[(int)min_team] &&
                        NetworkMatch.m_team_scores[(int)team] < NetworkMatch.m_team_scores[(int)min_team]))
                    min_team = team;
            __result = min_team;
            Debug.LogFormat("GetTeamForPlayer: result {0}, conn {1}, counts {2}, scores {3}", (int)min_team, connection_id,
                team_counts.Join(), NetworkMatch.m_team_scores.Join());
            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScoreTeamAnarchy")]
    static class MPTeamsHighest
    {
        static bool Prefix(ref int __result)
        {
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || MPTeams.NetworkMatchTeamCount == 2)
                return true;
            __result = MPTeams.HighestScore();
            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScorePowercore")]
    static class MPTeamsHighestMB
    {
        static bool Prefix(ref int __result)
        {
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || MPTeams.NetworkMatchTeamCount == 2)
                return true;
            __result = MPTeams.HighestScore();
            return false;
        }
    }


    [HarmonyPatch]
    static class MPTeamsCanStartNow
    {
        static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetMethod("CanStartNow", BindingFlags.Public | BindingFlags.Instance);
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs)
        {
            foreach (var c in cs)
            {
                if (c.opcode == OpCodes.Ldsfld && ((FieldInfo)c.operand).Name == "m_match_mode")
                {
                    var c2 = new CodeInstruction(OpCodes.Ldc_I4_1) { labels = c.labels };
                    yield return c2;
                    yield return new CodeInstruction(OpCodes.Ret);
                    c.labels = null;
                }
                yield return c;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
    class MPTeamsLobbyPos
    {
        static bool DrawLast(UIElement uie)
        {
            if (MenuManager.m_menu_micro_state != 0)
                return MenuManager.m_menu_micro_state == 1 ? DrawLastQuit(uie) : true;
            Vector2 position;
            position.x = 0f;
            position.y = 170f + 62f * 2;
            //uie.DrawMenuSeparator(position - Vector2.up * 40f);
            bool flag = NetworkMatch.m_last_lobby_status != null && NetworkMatch.m_last_lobby_status.m_can_start_now && MPModifiers.PlayerModifiersValid(Player.Mp_modifier1, Player.Mp_modifier2);
            uie.SelectAndDrawCheckboxItem(Loc.LS("START MATCH NOW"), position - Vector2.right * 250f, 0, MenuManager.m_mp_ready_to_start && flag,
                !flag || MenuManager.m_mp_ready_vote_timer > 0f, 0.75f, -1);
            //position.y += 62f;
            uie.SelectAndDrawItem(Loc.LS("CUSTOMIZE"), position + Vector2.right * 250f, 1, false, 0.75f, 0.75f);
            position.y += 62f;
            uie.SelectAndDrawItem(Loc.LS("OPTIONS"), position - Vector2.right * 250f, 2, false, 0.75f, 0.75f);
            //position.y += 62f;
            uie.SelectAndDrawItem(Loc.LS("MULTIPLAYER MENU"), position + Vector2.right * 250f, 100, false, 0.75f, 0.75f);
            return false;
        }

        static bool DrawLastQuit(UIElement uie)
        {
            Vector2 position;
            position.x = 0f;
            position.y = 170f + 62f * 2;
            uie.SelectAndDrawItem(Loc.LS("QUIT"), position, 0, false, 1f, 0.75f);
            position.y += 62f;
            uie.SelectAndDrawItem(Loc.LS("CANCEL"), position, 100, false, 1f, 0.75f);
            return false;
        }

        static void DrawDigitsLikeOne(UIElement uie, Vector2 pos, int value, float scl, Color c, float a)
        {
            uie.DrawStringSmall(value.ToString(), pos + Vector2.right * 15f, scl, StringOffset.RIGHT, c, a);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var mpTeamsLobbyPos_DrawLast_Method = AccessTools.Method(typeof(MPTeamsLobbyPos), "DrawLast");
            var mpTeamsLobbyPos_DrawDigitsLikeOne_Method = AccessTools.Method(typeof(MPTeamsLobbyPos), "DrawDigitsLikeOne");

            int state = 0; // 0 = before switch, 1 = after switch
            int oneSixtyCount = 0;
            for (var codes = instructions.GetEnumerator(); codes.MoveNext();)
            {
                var code = codes.Current;
                // add call before switch m_menu_micro_state
                if (state == 0 && code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "m_menu_micro_state")
                {
                    yield return code;
                    codes.MoveNext();
                    code = codes.Current;
                    yield return code;
                    if (code.opcode != OpCodes.Stloc_S)
                        continue;
                    var buf = new List<CodeInstruction>();
                    // find br to end of switch just after switch instruction
                    while (codes.MoveNext() && (code = codes.Current).opcode != OpCodes.Br)
                        buf.Add(code);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, mpTeamsLobbyPos_DrawLast_Method);
                    yield return new CodeInstruction(OpCodes.Brfalse, code.operand); // returns false? skip to end of switch
                    // preserve switch jump
                    foreach (var bcode in buf)
                        yield return bcode;
                    state = 1;
                }
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "DrawDigitsOne") // allow >9 with same positioning
                    code.operand = mpTeamsLobbyPos_DrawDigitsLikeOne_Method;
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 160f)
                {
                    oneSixtyCount++;
                    if (oneSixtyCount == 3)
                    {
                        code.operand = 300f;
                    }
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpPreMatchMenuUpdate")]
    class MPTeamsSwitch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var mpTeams_NextTeam_Method = AccessTools.Method(typeof(MPTeams), "NextTeam");

            for (var codes = instructions.GetEnumerator(); codes.MoveNext();)
            {
                var code = codes.Current;
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 3f) // reduce team switch wait time
                    code.operand = 0.2f;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_team")
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, mpTeams_NextTeam_Method);
                    // skip until RequestSwitchTeam call
                    while (codes.MoveNext() && codes.Current.opcode != OpCodes.Call)
                        ;
                    code = codes.Current;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUDArmor")]
    class MPTeamsHUDArmor
    {
        public static IEnumerable<CodeInstruction> ChangeTeamColorLoad(IEnumerator<CodeInstruction> codes, OpCode mod)
        {
            // current team already loaded
            yield return new CodeInstruction(mod);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams), "TeamColor"));
            // skip until store
            var labels = new List<Label>();
            while (codes.MoveNext() && codes.Current.opcode != OpCodes.Stloc_S && codes.Current.opcode != OpCodes.Stloc_0)
                if (codes.Current.labels.Count() != 0)
                {
                    //var ncode = new CodeInstruction(OpCodes.Nop);
                    labels.AddRange(codes.Current.labels);
                    //yield return ncode;
                }
            // do store color
            codes.Current.labels.AddRange(labels);
            yield return codes.Current;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.GetEnumerator();
            while (codes.MoveNext())
            {
                var code = codes.Current;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_mp_team")
                {
                    yield return code;
                    for (int i = 0; i < 2; i++)
                    {
                        // pass on until color init
                        while (codes.MoveNext() &&
                            (codes.Current.opcode != OpCodes.Ldfld || ((FieldInfo)codes.Current.operand).Name != "m_mp_team"))
                            yield return codes.Current;
                        yield return codes.Current;
                        foreach (var c in MPTeamsHUDArmor.ChangeTeamColorLoad(codes, i == 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_3))
                            yield return c;
                    }
                    break;
                }
                yield return code;
            }
            while (codes.MoveNext())
                yield return codes.Current;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawScoresForTeam")]
    class MPTeamsScoresForTeam
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.GetEnumerator();
            int cnt = 0;
            while (codes.MoveNext())
            {
                var code = codes.Current;
                yield return code;
                if (code.opcode == OpCodes.Ldarg_1 && (++cnt == 2 || cnt == 3))
                    foreach (var c in MPTeamsHUDArmor.ChangeTeamColorLoad(codes, cnt == 2 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_4))
                        yield return c;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMPWeaponOutline")]
    class MPTeamsWeaponOutline
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.GetEnumerator();
            int cnt = 0;
            while (codes.MoveNext())
            {
                var code = codes.Current;
                yield return code;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_mp_team" && ++cnt == 1)
                    foreach (var c in MPTeamsHUDArmor.ChangeTeamColorLoad(codes, OpCodes.Ldc_I4_1))
                        yield return c;
            }
        }
    }

    [HarmonyPatch(typeof(Overload.UIElement), "DrawMpPostgame")]
    class MPTeamsPostgamePatch
    {
        static bool Prefix(UIElement __instance)
        {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                return true;
            if (GameManager.m_local_player.m_hitpoints >= 0f)
                MPTeams.DrawPostgame(__instance);
            return false;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkMatch), "GetTeamScore"))
                {
                    state++;
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloca, 1);
                    yield return new CodeInstruction(OpCodes.Ldloca, 2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams), "ShowTeamWinner"));
                }

                if (state > 0 && state < 3 && code.opcode == OpCodes.Br)
                {
                    state++;
                }

                if (state > 0 && state < 3)
                    continue;

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Overload.UIElement), "DrawMpPostgameOverlay")]
    class MPTeamsPostgameOverlayPatch
    {
        static bool Prefix(UIElement __instance)
        {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) || (MPTeams.NetworkMatchTeamCount == 2 && NetworkMatch.m_players.Count <= 8))
                return true;
            if (GameManager.m_local_player.m_hitpoints < 0f)
                MPTeams.DrawPostgame(__instance);
            return false;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkMatch), "GetTeamScore"))
                {
                    state++;
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloca, 1);
                    yield return new CodeInstruction(OpCodes.Ldloca, 2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams), "ShowTeamWinner"));
                }

                if (state > 0 && state < 3 && code.opcode == OpCodes.Br)
                {
                    state++;
                }

                if (state > 0 && state < 3)
                    continue;

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Overload.UIElement), "DrawHUD")]
    class MPTeamsHUDPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs)
        {
            foreach (var c in cs)
            {
                if (c.opcode == OpCodes.Ldc_R4 && (float)c.operand == -240f)
                    c.operand = -350f;
                yield return c;
            }
        }
    }

    [HarmonyPatch(typeof(Overload.UIManager), "ChooseMpColor")]
    class MPTeamsMpColor
    {
        static bool Prefix(MpTeam team, ref Color __result)
        {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                return true;

            if (MPTeams.NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS)
            {
                if (Menus.mms_team_color_default)
                    return true;

                bool my_team = team == GameManager.m_local_player.m_mp_team;
                if (my_team)
                {
                    __result = MPTeams.TeamColorByIndex(Menus.mms_team_color_self, 2);
                }
                else
                {
                    __result = MPTeams.TeamColorByIndex(Menus.mms_team_color_enemy, 0);
                }

            }
            else
            {
                __result = MPTeams.TeamColor(team, 2);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPTeamsMenuDraw
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs)
        {
            var vector2_y_Field = AccessTools.Field(typeof(Vector2), "y");

            int lastAdv = 0;
            foreach (var c in cs)
            {
                if (lastAdv == 0 && c.opcode == OpCodes.Ldstr && (string)c.operand == "ADVANCED SETTINGS")
                {
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldfld, vector2_y_Field);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 62f);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stfld, vector2_y_Field);
                    lastAdv = 1;
                }
                else if ((lastAdv == 1 || lastAdv == 2) && c.opcode == OpCodes.Call)
                {
                    lastAdv++;
                }
                else if (lastAdv == 3)
                {
                    if (c.opcode != OpCodes.Ldloca_S)
                        continue;
                    lastAdv = 4;
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldfld, vector2_y_Field);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 62f - 93f);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stfld, vector2_y_Field);
                }
                yield return c;
            }
        }

    }

    [HarmonyPatch(typeof(PlayerShip), "UpdateShipColors")]
    class MPTeamsShipColors
    {
        static void Prefix(ref MpTeam team, ref int glow_color, ref int decal_color)
        {
            if (team == MpTeam.ANARCHY)
                return;

            glow_color = decal_color = MPTeams.TeamColorIdx(team);
            team = MpTeam.ANARCHY; // prevent original team color assignment
        }
    }

    /// <summary>
    // If ScaleRespawnTime is set, automatically set respawn timer = player's team count
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "DyingUpdate")]
    class MPTeams_PlayerShip_DyingUpdate
    {
        static void MaybeUpdateDeadTimer(PlayerShip playerShip)
        {
            if (MPModPrivateData.ScaleRespawnTime)
            {
                playerShip.m_dead_timer = Overload.NetworkManager.m_Players.Count(x => x.m_mp_team == playerShip.c_player.m_mp_team && !x.m_spectator);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var playerShip_m_dead_timer_Field = AccessTools.Field(typeof(PlayerShip), "m_dead_timer");
            var mpTeams_PlayerShip_DyingUpdate_Method = AccessTools.Method(typeof(MPTeams_PlayerShip_DyingUpdate), "MaybeUpdateDeadTimer");

            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Stfld && code.operand == playerShip_m_dead_timer_Field)
                {
                    state = 1;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, mpTeams_PlayerShip_DyingUpdate_Method);
                    continue;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Sort players in Team Anarchy scoreboard by Kills, Assists, then Deaths instead of Anarchy scoring
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawScoresForTeam")]
    class MPTeams_UIElement_DrawScoresForTeams
    {
        static List<int> SortTeamScores(List<int> list)
        {
            List<Player> players = Overload.NetworkManager.m_PlayersForScoreboard;
            list.Sort((int a, int b) =>
                players[a].m_kills != players[b].m_kills
                    ? players[b].m_kills.CompareTo(players[a].m_kills)
                    : (players[a].m_assists != players[b].m_assists ? players[b].m_assists.CompareTo(players[a].m_assists) : players[a].m_deaths.CompareTo(players[b].m_deaths))
            );
            return list;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var list_int_Reverse_Method = AccessTools.Method(typeof(List<int>), "Reverse");
            var mpTeams_UIElement_DrawScoresForTeams_SortTeamScores_Method = AccessTools.Method(typeof(MPTeams_UIElement_DrawScoresForTeams), "SortTeamScores");

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt && code.operand == list_int_Reverse_Method)
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, mpTeams_UIElement_DrawScoresForTeams_SortTeamScores_Method);
                    yield return new CodeInstruction(OpCodes.Stloc_2);
                    continue;
                }
                yield return code;
            }
        }
    }

    // Edge color effect needs changed for Team matches, otherwise leave as global m_damage_material (red).  This is primarily noticeable as fully charged TB glow
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class MPTeams_PlayerShip_Update
    {
        static Material LoadDamageMaterial(PlayerShip player_ship)
        {
            if (GameplayManager.IsMultiplayerActive && !GameplayManager.IsDedicatedServer() && NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
            {
                Material m = new Material(UIManager.gm.m_damage_material);
                var teamcolor = UIManager.ChooseMpColor(player_ship.c_player.m_mp_team);
                m.SetColor("_EdgeColor", teamcolor);
                return m;
            }
            else
            {
                return UIManager.gm.m_damage_material;
            }
        }

        // Damage glow in team color
        static void Postfix(PlayerShip __instance)
        {
            MPTeams.SetPlayerGlow(__instance, __instance.c_player.m_mp_team);
        }

        // UIManager.gm.m_damage_material is a global client field for heavy incurred damage/TB overcharge, patch to call our LoadDamageMaterial() instead
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(GameManager), "m_damage_material"))
                {
                    yield return new CodeInstruction(OpCodes.Pop); // Remove previous ldsfld    class Overload.GameManager Overload.UIManager::gm, cheap enough to keep transpiler simpler
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams_PlayerShip_Update), "LoadDamageMaterial"));
                    continue;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawTeamHeader")]
    class MPTeams_UIElement_DrawTeamHeader
    {
        static bool Prefix(UIElement __instance, Vector2 pos, MpTeam team, float w)
        {
            MPTeams.DrawTeamHeader(__instance, pos, team, w);
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawTeamScore")]
    class MPTeams_UIElement_DrawTeamScore
    {
        static bool Prefix(UIElement __instance, Vector2 pos, MpTeam team, int score, float w, bool my_team)
        {
            MPTeams.DrawTeamScore(__instance, pos, team, score, w, my_team);
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawTeamScoreSmall")]
    class MPTeams_UIElement_DrawTeamScoreSmall
    {
        static bool Prefix(UIElement __instance, Vector2 pos, MpTeam team, int score, float w, bool my_team)
        {
            MPTeams.DrawTeamScoreSmall(__instance, pos, team, score, w, my_team);
            return false;
        }
    }

    // Rim color is Team0/Team1/Anarchy dependent
    [HarmonyPatch(typeof(PlayerShip), "UpdateRimColor")]
    class MPTeams_PlayerShip_UpdateRimColor
    {
        static bool Prefix(PlayerShip __instance, bool mp)
        {
            Color c = UIManager.ChooseMpColor(__instance.c_player.m_mp_team);
            __instance.m_mp_rim_color.r = c.r * 0.25f;
            __instance.m_mp_rim_color.g = c.g * 0.25f;
            __instance.m_mp_rim_color.b = c.b * 0.25f;
            __instance.m_update_mp_color = true;
            return false;
        }
    }

    // Colors shown on kill feed
    // Transpiled due to odd results when modifying struct return val
    [HarmonyPatch(typeof(UIElement), "DrawRecentKillsMP")]
    class MPTeams_UIElement_DrawRecentKillsMP
    {
        static Color GetMessageColor(UIElement uie, MpMessageColor mpmc, float flash)
        {
            switch (mpmc)
            {
                case MpMessageColor.LOCAL:
                    return Color.Lerp(UIManager.m_col_hi2, UIManager.m_col_hi7, flash);
                case MpMessageColor.ANARCHY:
                case MpMessageColor.NONE:
                    return Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_ui3, flash);
                default:
                    Color c1 = MPTeams.TeamColor(MPTeams.GetMpTeamFromMessageColor((int)mpmc), 0);
                    Color c2 = MPTeams.TeamColor(MPTeams.GetMpTeamFromMessageColor((int)mpmc), 0);
                    return Color.Lerp(c1, c2, flash);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return codes.MethodReplacer(AccessTools.Method(typeof(UIElement), "GetMessageColor"), AccessTools.Method(typeof(MPTeams_UIElement_DrawRecentKillsMP), "GetMessageColor"));
        }
    }

    // Client handle kill feed in custom colors
    [HarmonyPatch(typeof(NetworkMessageManager), "AddKillMessage")]
    class MPTeams_NetworkMessageManager_AddKillMessage
    {
        static int GetMessageColorIndex(MpTeam team)
        {
            return MPTeams.TeamMessageColor(team);
        }

        static MatchMode IsExtMatchModeAnarchy()
        {
            if (NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                return MatchMode.TEAM_ANARCHY;

            return MatchMode.ANARCHY;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkMatch), "GetMode"))
                    code.operand = AccessTools.Method(typeof(MPTeams_NetworkMessageManager_AddKillMessage), "IsExtMatchModeAnarchy");

                if (code.opcode == OpCodes.Ldc_I4_3)
                {
                    state++;
                    code.opcode = OpCodes.Ldarg_S;
                    code.operand = state == 1 ? 5 : 2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams_NetworkMessageManager_AddKillMessage), "GetMessageColorIndex"));
                    continue;
                }
                yield return code;
            }
        }
    }

    // Client handle Full Chat message in custom colors
    [HarmonyPatch(typeof(NetworkMessageManager), "AddFullChatMessage")]
    class MPTeams_NetworkMessageManager_AddFullChatMessage
    {
        static int GetMessageColorIndex(MpTeam team)
        {
            return MPTeams.TeamMessageColor(team);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_3)
                {
                    code.opcode = OpCodes.Ldarg_2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams_NetworkMessageManager_AddFullChatMessage), "GetMessageColorIndex"));
                    continue;
                }
                yield return code;
            }
        }
    }

    // Client handle Quick Chat in custom colors
    [HarmonyPatch(typeof(NetworkMessageManager), "AddQuickChatMessage")]
    class MPTeams_NetworkMessageManager_AddQuickChatMessage
    {
        static int GetMessageColorIndex(MpTeam team)
        {
            return MPTeams.TeamMessageColor(team);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_3)
                {
                    code.opcode = OpCodes.Ldarg_2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams_NetworkMessageManager_AddQuickChatMessage), "GetMessageColorIndex"));
                    continue;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawQuickChatMP")]
    class MPTeams_UIElement_DrawQuickChatMP
    {
        static Color GetTeamColor(MpMessageColor mpmc)
        {
            if (mpmc == MpMessageColor.ANARCHY)
                return UIManager.m_col_ui3;

            return MPTeams.TeamColor(MPTeams.GetMpTeamFromMessageColor((int)mpmc), 0);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && (code.operand == AccessTools.Field(typeof(UIManager), "m_col_ui3") || code.operand == AccessTools.Field(typeof(UIManager), "m_col_mpa3") || code.operand == AccessTools.Field(typeof(UIManager), "m_col_mpb3")))
                {
                    code.opcode = OpCodes.Ldloc_3;
                    code.operand = null;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPTeams_UIElement_DrawQuickChatMP), "GetTeamColor"));
                    continue;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    class MPTeams_Server_RegisterHandlers
    {
        public static void DoChangeTeam(MPTeams.ChangeTeamMessage msg)
        {
            var targetPlayer = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.netId == msg.netId);

            ServerStatLog.AddTeamChange(targetPlayer, msg.newTeam);
            targetPlayer.Networkm_mp_team = msg.newTeam;

            // Also need to set the Lobby data as it gets used for things like tracker stats
            var targetLobbyData = NetworkMatch.m_players.FirstOrDefault(x => x.Value.m_name == targetPlayer.m_mp_name).Value;
            targetLobbyData.m_team = msg.newTeam;

            // CTF behavior, need to account for flag carrier switching
            if (CTF.IsActiveServer)
            {
                if (CTF.PlayerHasFlag.ContainsKey(targetPlayer.netId) && CTF.PlayerHasFlag.TryGetValue(targetPlayer.netId, out int flag))
                {
                    CTF.SendCTFLose(-1, targetPlayer.netId, flag, FlagState.HOME, true);

                    if (!CTF.CarrierBoostEnabled)
                    {
                        targetPlayer.c_player_ship.m_boost_overheat_timer = 0;
                        targetPlayer.c_player_ship.m_boost_heat = 0;
                    }

                    CTF.NotifyAll(CTFEvent.RETURN, $"{MPTeams.TeamName(targetPlayer.m_mp_team)} FLAG RETURNED AFTER {targetPlayer.m_mp_name} CHANGED TEAMS",
                        targetPlayer, flag);
                }
            }

            foreach (var player in Overload.NetworkManager.m_Players.Where(x => x.connectionToClient.connectionId > 0))
            {
                // Send message to clients with 'changeteam' support to give them HUD message
                if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "changeteam"))
                    NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgChangeTeam, msg);
            }
        }

        private static void OnChangeTeam(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPTeams.ChangeTeamMessage>();
            DoChangeTeam(msg);
        }

        static void Postfix()
        {
            if (GameplayManager.IsDedicatedServer())
            {
                NetworkServer.RegisterHandler(MessageTypes.MsgChangeTeam, OnChangeTeam);
            }
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    public class MPTeams_Client_RegisterHandlers
    {
        private static void OnChangeTeam(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPTeams.ChangeTeamMessage>();
            var player = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.netId == msg.netId);

            if (player != null && msg.newTeam != player.m_mp_team)
            {
                player.m_mp_team = msg.newTeam;
                MPTeams.UpdateClientColors();

                GameplayManager.AddHUDMessage($"{player.m_mp_name} changed teams", -1, true);
                SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
            }
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgChangeTeam, OnChangeTeam);
        }
    }

    // Team-colored creepers in team games
    [HarmonyPatch(typeof(Projectile), "Fire")]
    class MPTeams_Projectile_Fire
    {
        // stock Colors for restoring the correct particle colors in Anarchy
        // since Unity actively attempts to outsmart me by reusing previous
        // ParticleSystems and getting leftover TA colors despite my efforts.

        public static Color s_glow = new Color(1f, 0.706f, 0.265f, 0.234f);
        public static Color s_trail = new Color(1f, 0.419f, 0.074f, 0.853f);
        public static Color s_ring = new Color(1f, 0.173f, 0.039f, 0.392f);

        static void Postfix(Projectile __instance)
        {
            if (__instance.m_type == ProjPrefab.missile_creeper && !GameplayManager.IsDedicatedServer())
            {
                if (GameplayManager.IsMultiplayerActive && NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) && Menus.mms_creeper_colors)
                {
                    var teamcolor = UIManager.ChooseMpColor(__instance.m_mp_team);
                    //var teamcolor = Color.Lerp(UIManager.ChooseMpColor(__instance.m_mp_team), Color.white, 0.1f); // brightens things slightly

                    __instance.c_go.GetComponent<Light>().color = teamcolor;

                    foreach (var rend in __instance.c_go.GetComponentsInChildren<Renderer>(includeInactive: true))
                    {
                        if (rend.name == "_glow" || rend.name == "extra_glow")
                        {
                            foreach (var mat in rend.materials)
                            {
                                if (mat.name == "_glow_superbright1_yellow" || mat.name == "enemy_creeper1")
                                {
                                    mat.color = teamcolor;
                                    mat.SetColor("_EmissionColor", teamcolor);
                                }
                            }
                        }
                    }
                    foreach (var x in __instance.c_go.GetComponentsInChildren<ParticleSystem>())
                    {
                        var m = x.main;
                        m.startColor = teamcolor;
                    }
                }
                else
                {
                    foreach (var x in __instance.c_go.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
                    {
                        var m = x.main;
                        switch (x.name)
                        {
                            case "_glow":
                                m.startColor = s_glow;
                                break;
                            case "partilce_ring": // No, I didn't misspell that. Revival did.
                                m.startColor = s_ring;
                                break;
                            case "trail_creeper(Clone)":
                                m.startColor = s_trail;
                                break;
                        }
                    }
                }
            }
        }
    }

    // if team-coloured creepers are on and friendly fire isn't, causes the player's creepers to blink periodically
    [HarmonyPatch(typeof(Projectile), "FixedUpdateDynamic")]
    static class MPTeams_Projectile_FixedUpdateDynamic
    {
        const float offTime = 0.2f;
        const float cycleTime = 1.2f;

        static bool glowOn = false;
        static float nextOff = offTime;
        static float nextTime = 0f;
        static float tempNext = 0f;

        static void Postfix(Projectile __instance)
        {
            if (GameplayManager.IsMultiplayerActive && Menus.mms_creeper_colors && MenuManager.mms_friendly_fire != 1 && __instance.m_type == ProjPrefab.missile_creeper && __instance.m_owner_player.isLocalPlayer)
            {
                if (!__instance.m_robot_only_extra_mesh.activeSelf && nextTime <= Time.time)
                {
                    if (!glowOn)
                    {
                        nextOff = Time.time + offTime;
                        tempNext = Time.time + cycleTime;
                        glowOn = true;
                    }
                    __instance.m_robot_only_extra_mesh.SetActive(true);
                }
                if (__instance.m_robot_only_extra_mesh.activeSelf && nextOff <= Time.time)
                {
                    if (glowOn)
                    {
                        nextTime = tempNext;
                        glowOn = false;
                    }
                    __instance.m_robot_only_extra_mesh.SetActive(false);
                }
            }
        }
    }

    // resets the projectile list between rounds
    [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
    class MPTeams_GameplayManager_StartLevel
    {
        static void Postfix()
        {
            for (int i = 1; i < 29; i++) // Whyyyyyyyyy
            {
                //Debug.Log("CCC nuking proj list");
                /*foreach (ProjElement p in ProjectileManager.proj_list[i])
                {
                    Debug.Log("CCC destroying projectile");
                    UnityEngine.Object.Destroy(p.c_proj);
                    UnityEngine.Object.Destroy(p.c_go);
                    //p.Destroy();
                }*/
                ProjectileManager.proj_list[i].Clear();
            }
            UpdateDynamicManager.m_proj_count = 0;
        }
    }
}