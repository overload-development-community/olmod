using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameMod
{
    // detect "frametime" "cheat code"
    [HarmonyPatch(typeof(Overload.PlayerShip))]
    [HarmonyPatch("FrameUpdateReadKeysFromInput")]
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
}
