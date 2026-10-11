using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class DemosMenu
    {
        public static readonly MenuState msDemos = (MenuState)81;
        public static readonly UIElementType uiDemos = (UIElementType)94;

        private const int SEL_BACK = 100;
        private const int SEL_RECORD_TOGGLE = 110;
        private const int SEL_PLAY = 120;
        private const int SEL_DELETE = 130;
        private const int SEL_BENCHMARK = 160;

        private const int SEL_SCROLL_UP = 140;
        private const int SEL_SCROLL_DOWN = 150;
        private const int SEL_LIST_BASE = 1000;
        private const int ROWS_PER_PAGE = 8;

        internal const int SEL_DEMOS_MAIN_MENU = 13;

        private static List<OlrInfo> replays = new List<OlrInfo>();
        private static int selectedIndex = -1;
        private static bool confirmDelete;

        public static void DemosUpdate(ref float timer)
        {
            UIManager.MouseSelectUpdate();
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.INIT:
                    if (timer > 0.25f)
                    {
                        selectedIndex = -1;
                        RefreshList();
                        selectedIndex = replays.Count > 0 ? 0 : -1;

                        UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, uiDemos);
                        MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                        timer = 0f;
                        MenuManager.SetDefaultSelection(replays.Count > 0 ? SEL_LIST_BASE : SEL_BACK);
                    }
                    break;

                case MenuSubState.ACTIVE:
                    UIManager.ControllerMenu();
                    if (timer > 0.25f)
                    {
                        checkPagingArgs[0] = MenuManager.m_list_items_first;
                        checkPagingArgs[1] = MenuManager.m_list_items_last;
                        checkPagingArgs[2] = MenuManager.m_list_items_total_count;
                        checkPagingArgs[3] = ROWS_PER_PAGE;
                        bool pagingChanged = (bool)checkPagingMethod.Invoke(null, checkPagingArgs);
                        MenuManager.m_list_items_first = (int)checkPagingArgs[0];
                        MenuManager.m_list_items_last = (int)checkPagingArgs[1];
                        if (pagingChanged)
                        {
                            confirmDelete = false;
                            break;
                        }

                        float wheel = Input.GetAxis("Mouse ScrollWheel");
                        if (wheel != 0f)
                            ScrollListTo(MenuManager.m_list_items_first + (wheel < 0f ? 1 : -1));

                        if (!UIManager.PushedSelect(SEL_BACK))
                            break;

                        int selection = UIManager.m_menu_selection;
                        if (selection == SEL_BACK)
                        {
                            MenuManager.PlaySelectSound(1f);
                            timer = 0f;
                            UIManager.DestroyAll(false);
                            MenuManager.ChangeMenuState(MenuState.MAIN_MENU, true);
                        }
                        else if (selection == SEL_RECORD_TOGGLE)
                        {
                            Session.SetRecordReplays(!Session.RecordReplays);
                            MenuManager.PlayCycleSound(1f, 1f);
                        }
                        else if (selection == SEL_PLAY)
                        {
                            if (selectedIndex >= 0 && selectedIndex < replays.Count)
                                StartPlayback(ref timer);
                        }
                        else if (selection == SEL_BENCHMARK)
                        {
                            if (selectedIndex >= 0 && selectedIndex < replays.Count)
                            {
                                MenuManager.PlaySelectSound(1f);
                                timer = 0f;
                                UIManager.DestroyAll(false);
                                Benchmark.Start(replays[selectedIndex].path, 0,
                                    120f); // cap a button-started benchmark at two minutes
                            }
                        }
                        else if (selection == SEL_DELETE)
                        {
                            DoDelete();
                        }
                        else if (selection == SEL_SCROLL_UP)
                        {
                            ScrollListTo(MenuManager.m_list_items_first - 1);
                            MenuManager.PlayCycleSound(1f, -1f);
                        }
                        else if (selection == SEL_SCROLL_DOWN)
                        {
                            ScrollListTo(MenuManager.m_list_items_first + 1);
                            MenuManager.PlayCycleSound(1f, 1f);
                        }
                        else if (selection >= SEL_LIST_BASE && selection < SEL_LIST_BASE + replays.Count)
                        {
                            int rowIndex = selection - SEL_LIST_BASE;
                            confirmDelete = false;
                            if (rowIndex != selectedIndex)
                            {
                                selectedIndex = rowIndex;
                                MenuManager.PlayCycleSound(1f, 1f);
                            }
                            else
                            {
                                StartPlayback(ref timer);
                            }
                        }
                    }
                    break;
            }
        }

        private static void StartPlayback(ref float timer)
        {
            MenuManager.PlaySelectSound(1f);
            timer = 0f;
            UIManager.DestroyAll(false);

            Playback.Start(replays[selectedIndex].path);
        }

        private static void DoDelete()
        {
            if (selectedIndex < 0 || selectedIndex >= replays.Count)
                return;
            if (!confirmDelete)
            {
                confirmDelete = true;
                MenuManager.PlayCycleSound(1f, 1f);
                return;
            }
            try
            {
                File.Delete(replays[selectedIndex].path);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("REPLAY: delete failed: {0}", e);
            }
            MenuManager.PlaySelectSound(1f);
            RefreshList();
        }

        private static readonly MethodInfo checkPagingMethod =
            AccessTools.Method(typeof(MenuManager), "CheckPaging");

        private static readonly object[] checkPagingArgs = new object[4];

        private struct DemoRow
        {
            public string listLabel, map, mode, dateTime, duration, playerCount;
        }
        private static readonly List<DemoRow> demoRows = new List<DemoRow>();

        private static void RefreshList()
        {
            replays = OlrFile.ListInfos();
            demoRows.Clear();
            foreach (var info in replays)
            {
                string map;
                if (!info.IsPlayable)
                    map = info.formatVersion > 0 ? "OLD FORMAT (V" + info.formatVersion + ")" : "UNREADABLE";
                else
                    map = string.IsNullOrEmpty(info.sceneName)
                        ? "UNKNOWN" : info.sceneName.ToUpperInvariant();
                string date = DateText(info, withTime: false);
                demoRows.Add(new DemoRow
                {
                    listLabel = map + " - " + date,
                    map = map,
                    mode = Session.ModeName(info.matchMode),
                    dateTime = DateText(info, withTime: true),
                    duration = info.durationSeconds < 0f
                        ? "--:--" : Session.FormatTime(info.durationSeconds),
                    playerCount = info.playerCount.ToString(),
                });
            }
            MenuManager.m_list_items_total_count = replays.Count;
            MenuManager.m_list_items_first = 0;
            MenuManager.m_list_items_last = Mathf.Min(ROWS_PER_PAGE - 1, replays.Count - 1);
            MenuManager.m_list_items_max_per_page = Math.Min(replays.Count, ROWS_PER_PAGE);
            MenuManager.m_list_item_paging = replays.Count > ROWS_PER_PAGE;
            selectedIndex = replays.Count > 0 ? Mathf.Clamp(selectedIndex, 0, replays.Count - 1) : -1;
            confirmDelete = false;
        }

        private static void ScrollListTo(int first)
        {
            int perPage = Math.Max(1, MenuManager.m_list_items_max_per_page);
            first = Mathf.Clamp(first, 0, Math.Max(0, replays.Count - perPage));
            MenuManager.m_list_items_first = first;
            MenuManager.m_list_items_last = Math.Min(first + perPage - 1, replays.Count - 1);
        }

        private static bool scrollbarDragging;

        public static void DrawDemos(UIElement ui)
        {
            const float listX = -330f, detailX = 330f, columnHalfWidth = 212f;
            const float itemScale = 0.85f, itemTextSize = 0.6f;
            const float rowStride = 62f;
            const float listTop = -279f;
            const float listBottom = listTop + rowStride * (ROWS_PER_PAGE - 1);

            ui.m_alpha = 1f;
            UIManager.ui_bg_dark = true;
            ui.DrawMenuBG();
            ui.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 30f), "DEMOS", 265f);
            ui.DrawMenuSeparator(new Vector2(0f, UIManager.UI_TOP + 55f));
            ui.DrawMenuSeparator(new Vector2(0f, UIManager.UI_BOTTOM - 80f));

            if (replays.Count == 0)
            {
                ui.DrawLabelSmall(new Vector2(0f, (listTop + listBottom) * 0.5f),
                    "NO REPLAYS RECORDED YET", 400f);
            }
            else
            {
                Vector2 rowPos = new Vector2(listX, listTop);
                for (int i = MenuManager.m_list_items_first;
                     i <= MenuManager.m_list_items_last && i < replays.Count; i++)
                {
                    if (i == selectedIndex)
                    {
                        Vector2 boxHalf = new Vector2(500f * itemScale * 0.5f + 32f, 28f);
                        Color boxColor = UIManager.m_col_ui2;
                        boxColor.a = ui.m_alpha;
                        UIManager.DrawBoxOutline(rowPos - boxHalf, rowPos + boxHalf, boxColor, 1.5f, 13);
                    }
                    ui.SelectAndDrawItem(demoRows[i].listLabel, rowPos, SEL_LIST_BASE + i,
                        false, itemScale, 0.5f);
                    rowPos.y += rowStride;
                }

                int perPage = Math.Max(1, MenuManager.m_list_items_max_per_page);
                if (replays.Count > perPage)
                {
                    const float barX = -60f;
                    int maxFirst = replays.Count - perPage;
                    float trackTop = listTop - 10f;
                    float trackBottom = listBottom + 10f;
                    float trackHeight = trackBottom - trackTop;
                    float thumbHeight = trackHeight * perPage / replays.Count;

                    bool mouseOnTrack = Mathf.Abs(UIManager.m_mouse_pos.x - barX) <= 14f &&
                        UIManager.m_mouse_pos.y >= trackTop - 6f &&
                        UIManager.m_mouse_pos.y <= trackBottom + 6f;
                    if (Input.GetMouseButtonDown(0) && mouseOnTrack)
                        scrollbarDragging = true;
                    if (!Input.GetMouseButton(0))
                        scrollbarDragging = false;
                    if (scrollbarDragging)
                    {
                        float dragRange = trackHeight - thumbHeight;
                        float dragFraction = dragRange > 0f
                            ? Mathf.Clamp01((UIManager.m_mouse_pos.y - trackTop - thumbHeight * 0.5f) / dragRange)
                            : 0f;
                        ScrollListTo(Mathf.RoundToInt(dragFraction * maxFirst));
                    }

                    Vector2 trackCenter = new Vector2(barX, (trackTop + trackBottom) * 0.5f);
                    UIManager.DrawQuadUI(trackCenter, 6f, trackHeight * 0.5f + 4f,
                        UIManager.m_col_ub0, ui.m_alpha * 0.1f, 12);
                    UIManager.DrawQuadUI(trackCenter, 1.2f, trackHeight * 0.5f,
                        UIManager.m_col_ui0, ui.m_alpha * 0.5f, 12);

                    float thumbFraction = Mathf.Clamp01((float)MenuManager.m_list_items_first / maxFirst);
                    float thumbTop = trackTop + thumbFraction * (trackHeight - thumbHeight);
                    Vector2 thumbCenter = new Vector2(barX, thumbTop + thumbHeight * 0.5f);
                    Color thumbColor = (scrollbarDragging || mouseOnTrack)
                        ? Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6,
                            UnityEngine.Random.Range(0f, 0.2f * UIElement.FLICKER))
                        : UIManager.m_col_ui2;
                    UIManager.DrawQuadUIInner(thumbCenter, 5f, thumbHeight * 0.5f, thumbColor,
                        ui.m_alpha, 11, 0.75f);

                    DrawScrollArrow(ui, new Vector2(barX, trackTop - 18f), SEL_SCROLL_UP,
                        MenuManager.m_list_items_first > 0, true);
                    DrawScrollArrow(ui, new Vector2(barX, trackBottom + 18f), SEL_SCROLL_DOWN,
                        MenuManager.m_list_items_last < replays.Count - 1, false);
                }

                if (MenuManager.m_list_item_paging)
                    ui.DrawPageControls(new Vector2(0f, listBottom + 72f),
                        string.Format("DEMOS {0}-{1} OF {2}",
                            MenuManager.m_list_items_first + 1,
                            MenuManager.m_list_items_last + 1,
                            MenuManager.m_list_items_total_count),
                        true, true,
                        MenuManager.m_list_items_first > 0,
                        MenuManager.m_list_items_last < MenuManager.m_list_items_total_count - 1);

                // ---- right column: details of the selected demo plus its roster ----
                if (selectedIndex < 0 || selectedIndex >= replays.Count)
                {
                    ui.DrawLabelSmall(new Vector2(detailX, listTop), "SELECT A DEMO", columnHalfWidth);
                }
                else
                {
                    var info = replays[selectedIndex];
                    var row = demoRows[selectedIndex];

                    Vector2 labelPos = new Vector2(detailX - columnHalfWidth + 6f, listTop - 6f);
                    DrawDetailLine(ui, ref labelPos, "MAP", row.map);
                    DrawDetailLine(ui, ref labelPos, "MODE", row.mode);
                    DrawDetailLine(ui, ref labelPos, "DATE", row.dateTime);
                    DrawDetailLine(ui, ref labelPos, "DURATION", row.duration);
                    DrawDetailLine(ui, ref labelPos, "PLAYERS", row.playerCount);

                    if (info.playerNames != null && info.playerNames.Length > 0)
                    {
                        labelPos.y += 12f;
                        for (int i = 0; i < info.playerNames.Length && i < 8; i++) // two columns of four
                        {
                            Vector2 at = labelPos + new Vector2(i < 4 ? 4f : 212f, (i % 4) * 22f);
                            ui.DrawStringSmall(info.playerNames[i], at, 0.3f, StringOffset.LEFT,
                                UIManager.m_col_ui1, 1f, 200f);
                        }
                    }
                }

                float actionY = listBottom - 52f;
                bool buttonsDisabled = selectedIndex < 0;
                ui.SelectAndDrawItem("PLAY", new Vector2(detailX, actionY - 52f),
                    SEL_PLAY, buttonsDisabled, itemScale, itemTextSize);
                ui.SelectAndDrawItem("BENCHMARK", new Vector2(detailX, actionY),
                    SEL_BENCHMARK, buttonsDisabled, itemScale, itemTextSize);
                ui.SelectAndDrawItem(confirmDelete ? "CONFIRM DELETE" : "DELETE",
                    new Vector2(detailX, actionY + 52f), SEL_DELETE, buttonsDisabled,
                    itemScale, itemTextSize);
            }

            ui.SelectAndDrawItem("RECORD MATCHES: " + (Session.RecordReplays ? "ON" : "OFF"),
                new Vector2(listX, UIManager.UI_BOTTOM - 45f), SEL_RECORD_TOGGLE, false,
                itemScale, itemTextSize);
            ui.SelectAndDrawItem(Loc.LS("BACK"), new Vector2(detailX, UIManager.UI_BOTTOM - 45f),
                SEL_BACK, false, itemScale, itemTextSize);
        }

        private static void DrawScrollArrow(UIElement ui, Vector2 center, int selection,
            bool enabled, bool up)
        {
            if (!enabled)
                return;

            const float arm = 5f;
            const float boxHalf = 11f;
            ui.TestMouseInRect(center, boxHalf, boxHalf, selection, false);
            Color color = UIManager.m_menu_selection == selection
                ? Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6,
                    UnityEngine.Random.Range(0f, 0.5f * UIElement.FLICKER))
                : UIManager.m_col_ui2;
            float tipY = center.y + (up ? -arm * 0.5f : arm * 0.5f);
            float baseY = center.y + (up ? arm * 0.5f : -arm * 0.5f);
            UIManager.DrawQuadCenterLine(new Vector2(center.x - arm, baseY),
                new Vector2(center.x, tipY), 1.5f, 1f, color, 13);
            UIManager.DrawQuadCenterLine(new Vector2(center.x, tipY),
                new Vector2(center.x + arm, baseY), 1.5f, 1f, color, 13);
            Color boxColor = UIManager.m_col_ui2;
            boxColor.a = ui.m_alpha;
            UIManager.DrawBoxOutline(
                new Vector2(center.x - boxHalf, center.y - boxHalf),
                new Vector2(center.x + boxHalf, center.y + boxHalf),
                boxColor, 1.2f, 13);
        }

        private static void DrawDetailLine(UIElement ui, ref Vector2 pos, string label, string value)
        {
            ui.DrawStringSmall(label, pos, 0.35f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            ui.DrawStringSmall(value, pos + Vector2.right * 110f, 0.4f, StringOffset.LEFT,
                UIManager.m_col_ui1, 1f, 300f);
            pos.y += 26f;
        }

        private static string DateText(OlrInfo info, bool withTime)
        {
            if (info.date == DateTime.MinValue)
                return "--/--/----";
            return info.date.ToString(withTime ? "dd MM yyyy HH:mm" : "dd MM yyyy",
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMainMenu")]
    class DemosMenu_DrawMainMenu
    {
        internal static bool TranspilerApplied;

        public static void DrawDemosMainMenuRow(UIElement ui, ref Vector2 pos)
        {
            ui.SelectAndDrawItem("DEMOS", pos, DemosMenu.SEL_DEMOS_MAIN_MENU, false);
            pos.y += 62f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            int seedIndex = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand is float &&
                    (float)codes[i].operand == -155f) // the stock menu stack's top y seeds the layout
                {
                    if (seedIndex >= 0) { seedIndex = -1; break; }
                    seedIndex = i;
                }
            }

            var outlineMethod = AccessTools.Method(typeof(UIElement), "SelectAndDrawItemOutline");
            int storeIndex = -1;
            CodeInstruction positionLoad = null;
            for (int i = 0; i < codes.Count && storeIndex < 0; i++)
            {
                if (!(codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt) ||
                    !Equals(codes[i].operand, outlineMethod))
                    continue;
                bool isMultiplayerItem = false;
                for (int j = Math.Max(0, i - 8); j < i; j++)
                    if (codes[j].opcode == OpCodes.Ldc_I4_7)
                        isMultiplayerItem = true;
                if (!isMultiplayerItem)
                    continue;
                for (int j = i + 1; j < Math.Min(codes.Count, i + 14); j++)
                {
                    if (codes[j].opcode == OpCodes.Ldloca || codes[j].opcode == OpCodes.Ldloca_S)
                        positionLoad = codes[j];
                    bool isStore = codes[j].opcode == OpCodes.Stind_R4 ||
                        (codes[j].opcode == OpCodes.Stfld && codes[j].operand is FieldInfo &&
                         ((FieldInfo)codes[j].operand).Name == "y");
                    if (isStore)
                    {
                        if (positionLoad != null)
                            storeIndex = j;
                        break;
                    }
                }
                break;
            }

            if (seedIndex < 0 || storeIndex < 0)
            {
                Debug.LogWarning("REPLAY: DrawMainMenu anchors not found, DEMOS falls back to half-item");
                TranspilerApplied = false;
                return instructions;
            }

            codes[seedIndex].operand = -186f; // start the stack half a row higher to fit the DEMOS row
            codes.InsertRange(storeIndex + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),

                new CodeInstruction(positionLoad.opcode, positionLoad.operand),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(DemosMenu_DrawMainMenu), nameof(DrawDemosMainMenuRow)))
            });
            TranspilerApplied = true;
            return codes;
        }

        static void Postfix(UIElement __instance)
        {
            if (!TranspilerApplied)
                __instance.SelectAndDrawHalfItem("DEMOS", new Vector2(500f, 217f),
                    DemosMenu.SEL_DEMOS_MAIN_MENU, false);
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MainMenuUpdate")]
    class DemosMenu_MainMenuUpdate
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                !NetworkManager.IsHeadless() && UIManager.PushedSelect() &&
                UIManager.m_menu_selection == DemosMenu.SEL_DEMOS_MAIN_MENU)
            {
                UIManager.DestroyAll();
                MenuManager.ChangeMenuState(DemosMenu.msDemos);
                MenuManager.PlaySelectSound(1f);
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "Update")]
    class DemosMenu_MenuManager_Update
    {
        static void Postfix(ref float ___m_menu_state_timer)
        {
            if (MenuManager.m_menu_state == DemosMenu.msDemos)
                DemosMenu.DemosUpdate(ref ___m_menu_state_timer);
        }
    }

    [HarmonyPatch(typeof(UIElement), "Draw")]
    class DemosMenu_UIElement_Draw
    {
        static void Postfix(UIElement __instance)
        {
            if (__instance.m_type == DemosMenu.uiDemos && !__instance.m_fade_die)
                DemosMenu.DrawDemos(__instance);
        }
    }

    [HarmonyPatch(typeof(Console), "RegisterCommands")]
    class Commands_RegisterCommands
    {
        static void Postfix()
        {
            uConsole.RegisterCommand("replay", new uConsole.DebugCommand(Commands.CmdReplay));
            uConsole.RegisterCommand("replay_save", new uConsole.DebugCommand(Commands.CmdReplaySave));
        }
    }

    static class Commands
    {
        internal static void LogReplayList(List<string> paths, string usage)
        {
            uConsole.Log("replays in " + OlrFile.Directory + ":");
            for (int i = 0; i < paths.Count; i++)
                uConsole.Log("  [" + i + "] " + Path.GetFileName(paths[i]));
            uConsole.Log(usage);
        }

        internal static string ResolveReplayPath(List<string> paths, string indexOrName)
        {
            int index;
            if (int.TryParse(indexOrName, out index) && index >= 0 && index < paths.Count)
                return paths[index];
            return paths.Find(path => Path.GetFileName(path).ToLower()
                                          .Contains(indexOrName.ToLower()));
        }

        internal static void CmdReplaySave()
        {
            if (Recorder.IsRecording)
            {
                Recorder.StopAndSave();
                uConsole.Log("Recording saved.");
            }
            else
            {
                uConsole.Log("Not currently recording.");
            }
        }

        internal static void CmdReplay()
        {
            var demoPaths = OlrFile.ListPaths();
            if (uConsole.GetNumParameters() == 0)
            {
                LogReplayList(demoPaths, "Use: replay <index|name>");
                return;
            }
            string demoArg = uConsole.GetString();
            string path = ResolveReplayPath(demoPaths, demoArg);
            if (path == null)
            {
                uConsole.Log("Replay not found: " + demoArg);
                return;
            }
            uConsole.Log("Playing " + Path.GetFileName(path));
            Playback.Start(path);
        }
    }
}
