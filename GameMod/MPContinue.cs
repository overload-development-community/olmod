using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(UIElement), "DrawMpMatchEndedScoreboard")]
    class MPContinueDrawPostMatch
    {
        static MethodInfo DrawMpScoreboardRaw;

        static void Prepare()
        {
            DrawMpScoreboardRaw = typeof(UIElement).GetMethod("DrawMpScoreboardRaw", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        static bool Prefix(UIElement __instance)
        {
            if (!MenuManager.m_mp_lan_match)
                return true;
            var uie = __instance;
            uie.DrawMenuBG();
            Vector2 position = uie.m_position;
            position.y = UIManager.UI_TOP + 75f;
            uie.DrawHeaderLarge(position, Loc.LS("SCOREBOARD"));
            position.y += 42f;
            MatchMode mode = NetworkMatch.GetMode();
            string s = NetworkMatch.GetModeString(mode) + " - " + GameplayManager.Level.DisplayName;
            uie.DrawSubHeader(s, position);
            position.y += 20f;
            uie.DrawMenuSeparator(position);
            position.y += 20f;
            position.x = 0f;
            position.y += 10f;
            DrawMpScoreboardRaw.Invoke(uie, new object[] { position });
            position.y = UIManager.UI_BOTTOM - 30f;
            uie.SelectAndDrawItem(Loc.LS("MULTIPLAYER MENU"), position, 100, false);
            position.y -= 62f;
            uie.SelectAndDrawItem(Loc.LS(NetworkMatch.m_match_req_password == "" ? "CREATE AGAIN" : "JOIN AGAIN"), position, 2, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchEndedScoreboard")]
    class MPContinueUpdatePostMatch
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state != MenuSubState.ACTIVE || NetworkManager.IsHeadless() || !UIManager.PushedSelect(100) ||
                UIManager.m_menu_selection != 2)
                return;
            MenuManager.PlaySelectSound();
            UIManager.DestroyAll();

            NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.Invalid);
            NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.LocalLAN);
            MenuManager.ClearMpStatus();

            if (NetworkMatch.m_match_req_password == "")
            {
                //MenuManager.m_updating_pm_settings = true;
                //MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH);

                //var pmd = (PrivateMatchDataMessage)typeof(MenuManager).GetMethod("BuildPrivateMatchData", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { false });
                //NetworkMatch.StartPrivateLobby(pmd);
                //MenuManager.m_updating_pm_settings = true;
                MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH);
            }
            else
            {
                MenuManager.m_mp_status = Loc.LS("JOINING " + MPInternet.ClientModeName());
                NetworkMatch.JoinPrivateLobby(MenuManager.mms_match_password);
            }
        }
    }
}
