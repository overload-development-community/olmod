using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Move the game type and level name to a better place, add annoying custom projdata HUD message when playing MP, and draw the rear view.
    /// </summary>
    [Mod(new Mods[] { Mods.PresetData, Mods.RearView, Mods.Teams })]
    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    public static class UIElement_DrawHUD {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        [Mod(Mods.Teams)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) {
            foreach (var c in cs) {
                if (c.opcode == OpCodes.Ldc_R4 && (float)c.operand == -240f)
                    c.operand = -350f;
                yield return c;
            }
        }

        [Mod(new Mods[] { Mods.PresetData, Mods.RearView })]
        public static void Postfix(UIElement __instance) {
            if (PresetData.ProjDataExists) {
                Vector2 vector = default;
                vector.x = UIManager.UI_LEFT + 110;
                vector.y = UIManager.UI_TOP + 120f;
                __instance.DrawStringSmall("Using custom projdata", vector, 0.5f, StringOffset.CENTER, UIManager.m_col_damage, 1f);

                if (!GameplayManager.ShowHud || !RearView.Enabled)
                    return;

                if (RearView.rearTex == null || RearView.rearCam == null || RearView.rearCam.gameObject == null)
                    RearView.Init();

                if (GameManager.m_local_player.m_hitpoints <= 0) {
                    RearView.Pause();
                    return;
                }

                RearView.rearCam.enabled = true;
                var pos = new Vector2(288f, 288f);
                var posTile = new Vector2(pos.x, pos.y - 0.01f);
                var size = 100f;
                UIManager.DrawQuadUI(pos, size, size, UIManager.m_col_ui0, 1, 11);
                UIManager.SetTexture(RearView.rearTex);
                UIManager.PauseMainDrawing();
                UIManager.StartDrawing(UIManager.url[1], false, 750f);
                UIManager.DrawTile(posTile, size * 0.93f, size * 0.93f, new Color(0.8f, 0.8f, 0.8f, 1.0f), 1, 0, 0, 1, 1);
                UIManager.ResumeMainDrawing();
            }
        }
    }

    /// <summary>
    /// Draws the HUD armor in your team color.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawHUDArmor")]
    public static class UIElement_DrawHUDArmor {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var codes = instructions.GetEnumerator();
            while (codes.MoveNext()) {
                var code = codes.Current;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_mp_team") {
                    yield return code;
                    for (int i = 0; i < 2; i++) {
                        // pass on until color init
                        while (codes.MoveNext() &&
                            (codes.Current.opcode != OpCodes.Ldfld || ((FieldInfo)codes.Current.operand).Name != "m_mp_team"))
                            yield return codes.Current;
                        yield return codes.Current;
                        foreach (var c in Teams.ChangeTeamColorLoad(codes, i == 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_3))
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

    /// <summary>
    /// Moves the advanced settings button.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    public static class UIElement_DrawMpMatchSetup {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) {
            var vector2_y_Field = AccessTools.Field(typeof(Vector2), "y");

            int lastAdv = 0;
            foreach (var c in cs) {
                if (lastAdv == 0 && c.opcode == OpCodes.Ldstr && (string)c.operand == "ADVANCED SETTINGS") {
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldfld, vector2_y_Field);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 62f);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stfld, vector2_y_Field);
                    lastAdv = 1;
                } else if ((lastAdv == 1 || lastAdv == 2) && c.opcode == OpCodes.Call) {
                    lastAdv++;
                } else if (lastAdv == 3) {
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

    /// <summary>
    /// Show mini scoreboard on death.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
    public static class UIElement_DrawMpMiniScoreboard {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance, ref Vector2 pos) {
            if (MPModPrivateData.MatchMode == ExtMatchMode.RACE) {
                Race.DrawMpMiniScoreboard(ref pos, __instance);
                return false;
            }

            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || Teams.NetworkMatchTeamCount == 2)
                return true;

            int match_time_remaining = NetworkMatch.m_match_time_remaining;
            int match_time = (int)NetworkMatch.m_match_elapsed_seconds;
            pos.y -= 15f;
            __instance.DrawDigitsTime(pos + Vector2.right * 95f, (float)match_time_remaining, 0.45f,
                (match_time <= 10 || match_time_remaining >= 10) ? UIManager.m_col_ui2 : UIManager.m_col_em5,
                __instance.m_alpha, false);
            pos.y -= 3f;

            MpTeam myTeam = GameManager.m_local_player.m_mp_team;
            foreach (var team in Teams.TeamsByScore) {
                if (!NetworkManager.m_PlayersForScoreboard.Any(x => x.m_mp_team == team))
                    continue;

                pos.y += 28f;
                int score = NetworkMatch.GetTeamScore(team);
                Teams.DrawTeamScoreSmall(__instance, pos, team, score, 98f, team == myTeam);
            }
            pos.y += 6f;
            return false;
        }
    }

    /// <summary>
    /// Shows the postgame with scores with the correct colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawMpPostgame")]
    public static class UIElement_DrawMpPostgame {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance) {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                return true;
            if (GameManager.m_local_player.m_hitpoints >= 0f)
                Teams.DrawPostgame(__instance);
            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkMatch), "GetTeamScore")) {
                    state++;
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloca, 1);
                    yield return new CodeInstruction(OpCodes.Ldloca, 2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Teams), "ShowTeamWinner"));
                }

                if (state > 0 && state < 3 && code.opcode == OpCodes.Br) {
                    state++;
                }

                if (state > 0 && state < 3)
                    continue;

                yield return code;
            }
        }
    }

    /// <summary>
    /// Shows the postgame overlay with scores with the correct colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawMpPostgameOverlay")]
    public static class UIElement_DrawMpPostgameOverlay {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance) {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) || (Teams.NetworkMatchTeamCount == 2 && NetworkMatch.m_players.Count <= 8))
                return true;
            if (GameManager.m_local_player.m_hitpoints < 0f)
                Teams.DrawPostgame(__instance);
            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkMatch), "GetTeamScore")) {
                    state++;
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloca, 1);
                    yield return new CodeInstruction(OpCodes.Ldloca, 2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Teams), "ShowTeamWinner"));
                }

                if (state > 0 && state < 3 && code.opcode == OpCodes.Br) {
                    state++;
                }

                if (state > 0 && state < 3)
                    continue;

                yield return code;
            }
        }
    }

    /// <summary>
    /// Update lobby status display, and show updated menu options.
    /// </summary>
    [Mod(new Mods[] { Mods.PresetData, Mods.Teams })]
    [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
    public static class UIElement_DrawMpPreMatchMenu {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        [Mod(Mods.Teams)]
        public static bool DrawLast(UIElement uie) {
            if (MenuManager.m_menu_micro_state != 0)
                return MenuManager.m_menu_micro_state != 1 || DrawLastQuit(uie);
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

        [Mod(Mods.Teams)]
        public static bool DrawLastQuit(UIElement uie) {
            Vector2 position;
            position.x = 0f;
            position.y = 170f + 62f * 2;
            uie.SelectAndDrawItem(Loc.LS("QUIT"), position, 0, false, 1f, 0.75f);
            position.y += 62f;
            uie.SelectAndDrawItem(Loc.LS("CANCEL"), position, 100, false, 1f, 0.75f);
            return false;
        }

        [Mod(Mods.PresetData)]
        public static void Prefix() {
            PresetData.UpdateLobbyStatus();
        }

        [Mod(Mods.Teams)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var UIElement_DrawMpPreMatchMenu_DrawLast_Method = AccessTools.Method(typeof(UIElement_DrawMpPreMatchMenu), "DrawLast");

            int state = 0; // 0 = before switch, 1 = after switch
            int oneSixtyCount = 0;
            for (var codes = instructions.GetEnumerator(); codes.MoveNext();) {
                var code = codes.Current;
                // add call before switch m_menu_micro_state
                if (state == 0 && code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "m_menu_micro_state") {
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
                    yield return new CodeInstruction(OpCodes.Call, UIElement_DrawMpPreMatchMenu_DrawLast_Method);
                    yield return new CodeInstruction(OpCodes.Brfalse, code.operand); // returns false? skip to end of switch
                    // preserve switch jump
                    foreach (var bcode in buf)
                        yield return bcode;
                    state = 1;
                }
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 160f) {
                    oneSixtyCount++;
                    if (oneSixtyCount == 3) {
                        code.operand = 300f;
                    }
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Draws the two-column team anarchy scoreboard.
    /// </summary>
    /// <remarks>
    /// This should get merged with the Scoreboard mod so that there's one central place to draw the raw scoreboard.
    /// </remarks>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawMpScoreboardRaw")]
    public static class UIElement_DrawMpScoreboardRaw {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        private static readonly MethodInfo _UIElement_DrawScoresForTeam_Method = AccessTools.Method(typeof(UIElement), "DrawScoresForTeam");
        private static readonly MethodInfo _UIElement_DrawScoreHeader_Method = AccessTools.Method(typeof(UIElement), "DrawScoreHeader");
        public static bool Prefix(UIElement __instance, ref Vector2 pos) {
            var mode = NetworkMatch.GetMode();
            var fitSingle = Teams.NetworkMatchTeamCount == 2 && NetworkMatch.m_players.Count <= 8;
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
            foreach (var team in Teams.TeamsByScore) {
                pos.x = x + (fitSingle ? 0 : col == 0 ? -325f : 325f);
                pos.y = ys[col];
                Teams.DrawTeamScore(__instance, pos, team, NetworkMatch.GetTeamScore(team), col5, team == myTeam);
                pos.y += 35f;
                if (ys[col] == y || fitSingle) // only draw header for first team in column
                {
                    _UIElement_DrawScoreHeader_Method.Invoke(__instance, new object[] { pos, col1, col2, col3, col4, col5, false });
                    pos.y += 15f;
                    __instance.DrawVariableSeparator(pos, 350f);
                    pos.y += 20f;
                }
                int num = (int)_UIElement_DrawScoresForTeam_Method.Invoke(__instance, new object[] { team, pos, col1, col2, col3, col4, col5 });

                pos.y += (float)num * 25f + 35f;
                ys[col] = pos.y;
                if (!fitSingle)
                    col = 1 - col;
            }
            pos.y = Mathf.Max(ys[0], ys[1]);
            return false;
        }
    }

    /// <summary>
    /// Draws the weapon outline in the team's color.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawMPWeaponOutline")]
    public static class UIElement_DrawMPWeaponOutline {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var codes = instructions.GetEnumerator();
            int cnt = 0;
            while (codes.MoveNext()) {
                var code = codes.Current;
                yield return code;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_mp_team" && ++cnt == 1)
                    foreach (var c in Teams.ChangeTeamColorLoad(codes, OpCodes.Ldc_I4_1))
                        yield return c;
            }
        }
    }

    /// <summary>
    /// Update the color of quick chat.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawQuickChatMP")]
    public static class UIElement_DrawQuickChatMP {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static Color GetTeamColor(MpMessageColor mpmc) {
            if (mpmc == MpMessageColor.ANARCHY)
                return UIManager.m_col_ui3;

            return Teams.TeamColor(Teams.GetMpTeamFromMessageColor((int)mpmc), 0);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldsfld && (code.operand == AccessTools.Field(typeof(UIManager), "m_col_ui3") || code.operand == AccessTools.Field(typeof(UIManager), "m_col_mpa3") || code.operand == AccessTools.Field(typeof(UIManager), "m_col_mpb3"))) {
                    code.opcode = OpCodes.Ldloc_3;
                    code.operand = null;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIElement_DrawQuickChatMP), "GetTeamColor"));
                    continue;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Colors shown on kill feed.
    /// </summary>
    /// <remarks>
    /// Transpiled due to odd results when modifying struct return val.
    /// </remarks>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawRecentKillsMP")]
    public static class UIElement_DrawRecentKillsMP {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            return codes.MethodReplacer(AccessTools.Method(typeof(UIElement), "GetMessageColor"), AccessTools.Method(typeof(Teams), "GetMessageColor"));
        }
    }

    /// <summary>
    /// Draws scores in the proper team color, and sorts players in Team Anarchy scoreboard by Kills, Assists, then Deaths instead of Anarchy scoring.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawScoresForTeam")]
    public static class UIElement_DrawScoresForTeam {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static List<int> SortTeamScores(List<int> list) {
            List<Player> players = NetworkManager.m_PlayersForScoreboard;
            list.Sort((int a, int b) =>
                players[a].m_kills != players[b].m_kills
                    ? players[b].m_kills.CompareTo(players[a].m_kills)
                    : (players[a].m_assists != players[b].m_assists ? players[b].m_assists.CompareTo(players[a].m_assists) : players[a].m_deaths.CompareTo(players[b].m_deaths))
            );
            return list;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var list_int_Reverse_Method = AccessTools.Method(typeof(List<int>), "Reverse");
            var mpTeams_UIElement_DrawScoresForTeams_SortTeamScores_Method = AccessTools.Method(typeof(UIElement_DrawScoresForTeam), "SortTeamScores");

            var codes = instructions.GetEnumerator();
            int cnt = 0;
            while (codes.MoveNext()) {
                var code = codes.Current;

                // Sort players.
                if (code.opcode == OpCodes.Callvirt && code.operand == list_int_Reverse_Method) {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, mpTeams_UIElement_DrawScoresForTeams_SortTeamScores_Method);
                    yield return new CodeInstruction(OpCodes.Stloc_2);
                    continue;
                }

                yield return code;
                
                // Draw scores in correct color.
                if (code.opcode == OpCodes.Ldarg_1 && (++cnt == 2 || cnt == 3))
                    foreach (var c in Teams.ChangeTeamColorLoad(codes, cnt == 2 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_4))
                        yield return c;
            }
        }
    }

    /// <summary>
    /// Draws the team header with proper colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawTeamHeader")]
    public static class UIElement_DrawTeamHeader {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance, Vector2 pos, MpTeam team, float w) {
            Teams.DrawTeamHeader(__instance, pos, team, w);
            return false;
        }
    }

    /// <summary>
    /// Draws the team score with proper colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawTeamScore")]
    public static class UIElement_DrawTeamScore {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance, Vector2 pos, MpTeam team, int score, float w, bool my_team) {
            Teams.DrawTeamScore(__instance, pos, team, score, w, my_team);
            return false;
        }
    }

    /// <summary>
    /// Draws the small team score with proper colors.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "DrawTeamScoreSmall")]
    public static class UIElement_DrawTeamScoreSmall {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance, Vector2 pos, MpTeam team, int score, float w, bool my_team) {
            Teams.DrawTeamScoreSmall(__instance, pos, team, score, w, my_team);
            return false;
        }
    }

    /// <summary>
    /// Draws the player list when there are more than 2 teams.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIElement), "MaybeDrawPlayerList")]
    public static class UIElement_MaybeDrawPlayerList {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(UIElement __instance, Vector2 pos) {
            if (!MenuManager.mp_display_player_list ||
                ((!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) || Teams.NetworkMatchTeamCount == 2)))
                return true;

            float name_offset = -250f;
            float highlight_width = 285f;
            float org_x = pos.x;
            int max_row_count = NetworkMatch.GetMaxPlayersForMatch() + Teams.NetworkMatchTeamCount;
            int cur_row_count = NetworkMatch.m_players.Count() + Teams.NetworkMatchTeamCount;
            bool split = max_row_count > 10;
            if (split) {
                pos.x -= 300f;
                pos.y += 50f + 24f;
            }
            float org_y = pos.y;
            float first_y = org_y;
            int rows_per_col = split ? (cur_row_count + 1) / 2 : cur_row_count;
            int row_num = 0;
            foreach (var team in Teams.GetTeams) {
                if (row_num >= rows_per_col) {
                    first_y = pos.y;
                    pos.x += 300f * 2;
                    pos.y = org_y;
                    rows_per_col = cur_row_count; // no more split
                    row_num = 0;
                }
                Teams.DrawTeamHeader(__instance, pos, team, 255f);
                pos.y += 24f;
                row_num++;
                int num = 0;
                foreach (var value in NetworkMatch.m_players.Values) {
                    if (value.m_team == team) {
                        __instance.DrawPlayerName(pos, value, num % 2 == 0, highlight_width, name_offset, -1f);
                        pos.y += 20f;
                        num++;
                        row_num++;
                    }
                }
                pos.y += 10f;
            }
            pos.y = Mathf.Max(first_y, pos.y) + 10f;
            pos.x = org_x;
            if (MenuManager.m_menu_micro_state != 2 && MenuManager.m_mp_private_match) {
                float alpha_mod = (MenuManager.m_mp_cst_timer > 0f) ? 0.2f : 1f;
                __instance.DrawStringSmall(ScriptTutorialMessage.ControlString(CCInput.MENU_DELETE) + " - " + Loc.LS("CHANGE TEAMS"), pos, 0.45f, StringOffset.CENTER, UIManager.m_col_ui0, alpha_mod, -1f);
            }

            return false;
        }
    }
}
