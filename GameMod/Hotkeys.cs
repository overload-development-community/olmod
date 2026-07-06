using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(GameManager), "Update")]
    class FullscreenHotkey
    {
        static int s_windowedWidth = -1;
        static int s_windowedHeight = -1;

        static void Postfix()
        {
            if (GameplayManager.IsDedicatedServer() )
                return;
            if (uConsole.IsOn() || PlayerShip.m_typing_in_chat)
                return;

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
                Input.GetKeyDown(KeyCode.F))
            {
                if (!Screen.fullScreen)
                {
                    s_windowedWidth = Screen.width;
                    s_windowedHeight = Screen.height;
                    var native = Screen.currentResolution;
                    Screen.SetResolution(native.width, native.height, true);
                    MenuManager.m_resolution_width = native.width;
                    MenuManager.m_resolution_height = native.height;
                }
                else
                {
                    int w = s_windowedWidth > 0 ? s_windowedWidth : MenuManager.m_resolution_width;
                    int h = s_windowedHeight > 0 ? s_windowedHeight : MenuManager.m_resolution_height;
                    Screen.SetResolution(w, h, false);
                    MenuManager.m_resolution_width = w;
                    MenuManager.m_resolution_height = h;
                }
            }
        }
    }
}