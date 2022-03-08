using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    class FrameTimeInit
    {
        private static void Postfix()
        {
            GameManager.m_display_fps = Menus.mms_show_framerate;
        }
    }
}
