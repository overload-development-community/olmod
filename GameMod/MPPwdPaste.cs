using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPPwdPaste
    {
        static void Prefix()
        {
            if ((MenuManager.m_menu_micro_state == 1 || MenuManager.m_menu_micro_state == 4) && UIManager.m_menu_selection == 11 &&
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V)))
                MenuManager.mms_match_password = GUIUtility.systemCopyBuffer;
        }
    }
}
