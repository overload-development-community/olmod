using System;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    static class MPLevelSelect
    {
        public static int[] LevelIndex;
        public static bool OTL;
        private static string filter = null;
        private static bool filterAll = false;
        private static bool filterNotFound = false;

        private static bool LevelMatchFilter(int idx)
        {
            if (String.IsNullOrEmpty(filter)) {
                return true;
            }
            string name = GameManager.MultiplayerMission.GetLevelDisplayName(idx).ToUpper();
            int pos = name.IndexOf(filter);
            if (pos == 0 || (pos > 0 && filterAll)) {
                return true;
            }
            return false;
        }

        private static int CountFilteredLevels()
        {
            int totalCnt = GameManager.MultiplayerMission.NumLevels;
            int cnt = 0;
            for (int i = 0; i < totalCnt; i++) {
                if (LevelMatchFilter(i)) {
                    cnt++;
                }
            }
            return cnt;
        }

        public static void InitList()
        {
            int curMapIndex = -1;
            if (LevelIndex != null && MenuManager.mms_level_num >= 0 && MenuManager.mms_level_num < LevelIndex.Length) {
                curMapIndex = LevelIndex[MenuManager.mms_level_num];
            }
            if (OTL)
            {
                int stock = 11;
                var lvls = new[] { "Belted_v2", "Burning Indika 1.1", "junebug", "Kegparty", "Mesa", "Sub Rosa v3", "TBDB_v2", "Tryhard", "Turnstile_v4" };
                LevelIndex = new int[stock + lvls.Length];
                for (int i = 0; i < stock; i++)
                    LevelIndex[i] = i;
                for (int i = 0; i < lvls.Length; i++)
                    LevelIndex[i + stock] = GameManager.MultiplayerMission.FindLevelIndex(lvls[i]);
            }
            else
            {
                if (String.IsNullOrEmpty(filter)) {
                    LevelIndex = new int[GameManager.MultiplayerMission.NumLevels];
                    for (int i = 0; i < MPLevelSelect.LevelIndex.Length; i++)
                        LevelIndex[i] = i;
                } else {
                    int totalCnt = GameManager.MultiplayerMission.NumLevels;
                    int cnt = CountFilteredLevels();
                    LevelIndex = new int[cnt];
                    cnt = 0;
                    for (int i = 0; i < totalCnt; i++) {
                        if (LevelMatchFilter(i)) {
                            LevelIndex[cnt] = i;
                            if (i == curMapIndex) {
                                MenuManager.mms_level_num = i;
                            }
                            cnt++;
                        }
                    }
                }
            }
            int cur = Math.Min(LevelIndex.Length - 1, MenuManager.mms_level_num); 
            MenuManager.m_selected_mission = null; //GameManager.MultiplayerMission;
            MenuManager.m_list_items_total_count = LevelIndex.Length; //MenuManager.m_selected_mission.NumLevels;
            MenuManager.m_list_items_max_per_page = 16;
            MenuManager.m_list_items_max_per_page = Math.Min(MenuManager.m_list_items_total_count, MenuManager.m_list_items_max_per_page);
            MenuManager.m_list_items_first = cur - (cur % MenuManager.m_list_items_max_per_page);
            MenuManager.m_list_items_last = Mathf.Min(MenuManager.m_list_items_first + MenuManager.m_list_items_max_per_page,
                MenuManager.m_list_items_total_count) - 1;
            MenuManager.m_list_item_paging = MenuManager.m_list_items_total_count > MenuManager.m_list_items_max_per_page;
        }

        public static void ResetFilter()
        {
            filter = null;
            filterAll = false;
            filterNotFound = false;
            InitList();
        }

        public static void UpdateFilter(string addPart)
        {
            if (String.IsNullOrEmpty(addPart)) {
                return;
            }

            string prevFilter = filter;

            foreach (char c in addPart) {
                if (c == 8) {
                    // Backspace
                    if (filter != null && filter.Length > 1) {
                        filter = filter.Substring(0, filter.Length-1);
                    } else {
                        filter = null;
                    }
                } else if (c == '*') {
                    filterAll = !filterAll;
                } else if (c >= 32) {
                    // Normal Chars
                    if (filter == null) {
                        filter = c.ToString();
                    } else {
                        filter = filter + c.ToString();
                    }
                }
            }
            if (!String.IsNullOrEmpty(filter)) {
                filter = filter.ToUpper();
            }

            if (CountFilteredLevels() > 0) {
                InitList();
                filterNotFound = false;
            } else {
                filter = prevFilter;
                filterNotFound = true;
            }
        }

        public static string GetSymbolicFilterName()
        {
            if (String.IsNullOrEmpty(filter)) {
                return null;
            }
            return (filterAll?"*":"") + filter + "*";
        }

    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPLevelSelectDraw
    {
        static Vector2 temp_pos;

        static void DrawPageArrows(UIElement uie, Vector2 pos, bool show_pgup, bool show_pgdn, float arrow_x)
        {
            float a = uie.m_alpha * (0.7f + 0.3f * uie.m_anim_state2);
            if (show_pgdn)
            {
                temp_pos = pos;
                temp_pos.x = temp_pos.x - arrow_x;
                uie.TestMouseInRect(temp_pos, 30f, 15f, 198, false);
                UIManager.DrawSpriteUI(temp_pos, 0.3f, 0.3f, (UIManager.m_menu_selection != 198) ? UIManager.m_col_ub0 : UIManager.m_col_ui5, a, 81);
            }
            if (show_pgup)
            {
                temp_pos = pos;
                temp_pos.x = temp_pos.x + arrow_x;
                uie.TestMouseInRect(temp_pos, 30f, 15f, 199, false);
                UIManager.DrawSpriteUI(temp_pos, 0.3f, 0.3f, (UIManager.m_menu_selection != 199) ? UIManager.m_col_ub0 : UIManager.m_col_ui5, a, 80);
            }
        }

        private static MethodInfo _UIElement_DrawMiniLevelImage_Method = typeof(UIElement).GetMethod("DrawMiniLevelImage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static MethodInfo _UIElement_DrawWrappedText_Method = typeof(UIElement).GetMethod("DrawWrappedText", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        static bool Prefix(UIElement __instance)
        {
            if (MenuManager.m_menu_micro_state != 8)
                return true;
            var uie = __instance;
            UIManager.X_SCALE = 0.35f;
            uie.DrawMenuBG();
            uie.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 30f), Loc.LS("MULTIPLAYER LEVEL SELECT"), 265f);
            string filter = MPLevelSelect.GetSymbolicFilterName();
            Vector2 position = uie.m_position;
            if (!String.IsNullOrEmpty(filter)) {
              position.y = UIManager.UI_TOP + 90f;
              uie.DrawTextLineSmall("Filter: '" + filter + "'", position);
            }
            position.y = UIManager.UI_TOP + 85f;
            // is this useful?
            //uie.SelectAndDrawStringOptionItem(Loc.LS("LEVEL SET"), position, 4, MPLevelSelect.OTL ? "OTL" : "LOCAL", string.Empty, 1.5f, false);
            position.y += 85f;
            uie.DrawMenuSeparator(position - Vector2.up * 35f);
            int colItems = 8;
            for (int col = 0; col < 2; col++)
            {
                position.y = UIManager.UI_TOP + 85f * 2;
                position.x = -214f + col * 428f;
                int first = MenuManager.m_list_items_first + col * colItems;
                int last = Math.Min(first + colItems, MenuManager.m_list_items_last + 1);
                for (int i = first; i < last; i++)
                {
                    int idx = MPLevelSelect.LevelIndex[i];
                    uie.SelectAndDrawHalfItem3(idx >= 0 ? GameManager.MultiplayerMission.GetLevelDisplayName(idx) : "?", position, 1000 + i, idx == -1, false);
                    position.y += 62f;
                }
            }
            position.y = UIManager.UI_TOP + 85f * 2 + 62f * colItems;
            position.x = 0f;
            position.y -= 62f;
            uie.DrawMenuSeparator(position + Vector2.up * 35f);
            position.y -= 62f * 3.5f;
            if (MenuManager.m_list_item_paging)
                DrawPageArrows(uie, position, true, true, 435f);
            position.x = -535f;
            position.y = -60f;
            _UIElement_DrawMiniLevelImage_Method.Invoke(uie, new object[] { position, 300f, UIManager.m_menu_selection >= 1000 });
            //uie.DrawMiniLevelImage(position, 300f, UIManager.m_menu_selection >= 1000);

            var curLevelIdx = UIManager.m_menu_selection >= 1000 && UIManager.m_menu_selection < 1000 + MPLevelSelect.LevelIndex.Length ?
                MPLevelSelect.LevelIndex[UIManager.m_menu_selection - 1000] : -1;

            position.y -= 70f;
            if (curLevelIdx >= 0) {
                uie.DrawMiniHeaderBright(position, GameManager.MultiplayerMission.GetLevelDisplayName(curLevelIdx), 75f);
            }
            position.y += 140f;

            LevelInfo level = curLevelIdx >= 0 ? GameManager.MultiplayerMission.OpenLevel(curLevelIdx) : null;
            if (level != null && level.IsAddOn)
            {
                if (level.LevelDescription != null && level.LevelDescription[0] != null) {
                    _UIElement_DrawWrappedText_Method.Invoke(uie, new object[] { level.LevelDescription[0], position, 0.4f, 15f, 150f, StringOffset.CENTER, float.MaxValue, 0f, 0f });
                }
            }
            else if (curLevelIdx >= 0)
            {
                uie.DrawMiniHeader(position, Loc.LS("PLAYERS"), 75f);
                position.y += 22f;
                uie.DrawStringSmall(GameManager.MultiplayerMission.GetLevelMPPlayers(curLevelIdx), position, 0.4f, StringOffset.CENTER, UIManager.m_col_ui2, 1f, -1f);
                position.y += 30f;
                uie.DrawMiniHeader(position, Loc.LS("TEAM"), 75f);
                position.y += 22f;
                uie.DrawStringSmall(GameManager.MultiplayerMission.GetLevelMPPlayersTeam(curLevelIdx), position, 0.4f, StringOffset.CENTER, UIManager.m_col_ui2, 1f, -1f);
            }

            position.x = 0f;
            position.y = UIManager.UI_BOTTOM - 30f;
            uie.SelectAndDrawItem(Loc.LS("BACK"), position, 100, false, 1f, 0.75f);
            return false;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPLevelSelectUpdate
    {
        private static void Postfix()
        {
            // click on level name in match settings -> jump to level select screen
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                MenuManager.m_menu_micro_state == 2 &&
                UIManager.m_menu_selection == 2 &&
                Controls.JustPressed(CCInput.MENU_SELECT) &&
                !UIManager.m_pushed_dir && !MenuManager.reversed_option)
            {
                float num = 500f * 1.5f;
                Vector2 pos = UIManager.SCREEN_CENTER;
                pos.y -= 217f;
                pos.y += 62f + 62f;
                pos.x += num * 0.5f - 123f;
                var size = new Vector2(24f, 24f);
                Debug.Log("mouse " + UIManager.m_mouse_pos + " pos " + pos + " size " + size);
                if (new Rect(pos + Vector2.right * 130f - size, size * 2).Contains(UIManager.m_mouse_pos)) // right button clicked?
                    return;

                // workaround to undo that we have already selected the next level
                MenuManager.mms_level_num = MenuManager.mms_level_num == 0 ? GameManager.MultiplayerMission.NumLevels - 1 : MenuManager.mms_level_num - 1;

                MenuManager.SetDefaultSelection(0);
                MPLevelSelect.ResetFilter();
                MenuManager.m_menu_micro_state = 8;
                MenuManager.UIPulse(2f);
                MenuManager.PlaySelectSound(1f);
            }
        }

        private static MethodInfo _MenuManager_CheckPaging_Method = typeof(MenuManager).GetMethod("CheckPaging", BindingFlags.NonPublic | BindingFlags.Static);
        private static bool Prefix()
        {
            if (MenuManager.m_menu_micro_state != 8 || MenuManager.m_menu_sub_state != MenuSubState.ACTIVE)
                return true;
            MPLevelSelect.UpdateFilter(Input.inputString);
            UIManager.MouseSelectUpdate();

            UIManager.ControllerMenu();
            if (UIManager.m_menu_selection >= 1000)
            {
                int selIdx = UIManager.m_menu_selection - 1000;
                int idx = -1;
                if (selIdx < MPLevelSelect.LevelIndex.Length) {
                    idx = MPLevelSelect.LevelIndex[selIdx];
                }
                if (idx >= 0)
                    UIManager.SetLevelTexture(GameManager.MultiplayerMission, idx);
                else
                    UIManager.SetTexture((Texture)Resources.Load("Textures/default"));
            }
            //bool flag = MenuManager.CheckPaging(ref MenuManager.m_list_items_first, ref MenuManager.m_list_items_last, MenuManager.m_selected_mission.NumLevels, 6);
            var args = new object[] {  MenuManager.m_list_items_first, MenuManager.m_list_items_last,
                MenuManager.m_list_items_total_count, MenuManager.m_list_items_max_per_page };
            bool flag = (bool)_MenuManager_CheckPaging_Method.Invoke(null, args);
            MenuManager.m_list_items_first = (int)args[0];
            MenuManager.m_list_items_last = (int)args[1];
            if (!flag)
            {
                if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()))
                {
                    MenuManager.MaybeReverseOption();
                    if (UIManager.m_menu_selection == 100) // back
                    {
                        MenuManager.m_menu_micro_state = 2;
                        MenuManager.UIPulse(2f);
                        MenuManager.PlaySelectSound(1f);
                    }
                    else if (UIManager.m_menu_selection == 4)
                    {
                        MenuManager.PlayCycleSound(1f, 1f);
                        MPLevelSelect.OTL = !MPLevelSelect.OTL;
                        MPLevelSelect.InitList();
                    }
                    else if (UIManager.m_menu_selection >= 1000) // level
                    {
                        MenuManager.mms_level_num = MPLevelSelect.LevelIndex[UIManager.m_menu_selection - 1000];
                        UIManager.SetLevelTexture(GameManager.MultiplayerMission, MenuManager.mms_level_num);
                        MenuManager.m_menu_micro_state = 2;
                        MenuManager.UIPulse(2f);
                        MenuManager.PlaySelectSound(1f);
                    }
                }
            }
            /*
                    break;
                }
            case MenuSubState.BACK:
                if (true) //MenuManager.m_menu_state_timer > 0.25f)
                {
                    //MenuManager.ChangeMenuState(MenuState.MAIN_MENU, false);
                    MenuManager.m_menu_micro_state = 3;
                    MenuManager.UIPulse(2f);
                    MenuManager.PlaySelectSound(1f);
                }
                break;
            /*
            default:
                if (menu_sub_state == MenuSubState.FADE_IN)
                {
                    if (true) // MenuManager.m_menu_state_timer > 0.25f)
                    {
                        UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.CHALLENGE_SELECT_MENU);
                        MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                        MenuManager.SetDefaultSelection(1000);
    
                        MPLevelSelect.InitList();
                        //NetworkManager.StartServerWithLocalConnection(); // ???
                    }
                }
                break;
            */
            return false;
        }
    }
}
