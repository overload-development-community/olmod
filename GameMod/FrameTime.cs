using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameMod
{
    // detect "frametime" "cheat code"
    [HarmonyPatch(typeof(PlayerShip), "FrameUpdateReadKeysFromInput")]
    class FrameTimeReadKeys
    {
        private static string code = "frametime";
        private static int codeIdx = 0;

        static void Prefix()
        {
            foreach (char c in Input.inputString)
            {
                if (code[codeIdx] == c)
                    if (++codeIdx < code.Length)
                        continue;
                    else
                        GameManager.m_display_fps = !GameManager.m_display_fps;
                codeIdx = 0;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    class FrameTimeInit
    {
        private static void Postfix()
        {
            if (Core.GameMod.FindArg("-frametime"))
                GameManager.m_display_fps = true;
        }
    }
}
