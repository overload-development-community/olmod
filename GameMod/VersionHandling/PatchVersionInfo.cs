using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod.VersionHandling
{
    
    [HarmonyPatch(typeof(GameplayManager), "Initialize")]
    class PCheckNewOlmodVersion
    {
        static void Postfix()
        {
            OlmodVersion.TryRefreshLatestKnownVersion();
        }
    }

    // Display olmod related information on main menu
    [HarmonyPatch(typeof(UIElement), "DrawMainMenu")]
    class PVersionDisplay
    {
        static string GetVersion(string stockVersion)
        {
            return $"{stockVersion} {OlmodVersion.FullVersionString.ToUpperInvariant()}";
        }

        // append olmod version to the regular version display on the main menu
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var _string_Format_Method = AccessTools.Method(typeof(String), "Format", new Type[] { typeof(string), typeof(object), typeof(object), typeof(object) });
            var _versionPatch_GetVersion_Method = AccessTools.Method(typeof(PVersionDisplay), "GetVersion");

            int state = 0;

            foreach (var code in codes)
            {
                // this.DrawStringSmall(string.Format(Loc.LS("VERSION {0}.{1} BUILD {2}"), GameManager.Version.Major, GameManager.Version.Minor, GameManager.Version.Build), position, 0.5f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == _string_Format_Method)
                {
                    state = 1;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, _versionPatch_GetVersion_Method);
                    continue;
                }

                yield return code;
            }
        }


        // Draw olmod modified label and olmod update button, if applicable
        static void Postfix(UIElement __instance)
        {

            Vector2 pos = new Vector2(UIManager.UI_RIGHT - 10f, -155f - 60f + 50f + 40f);
            __instance.DrawStringSmall("UNOFFICIAL MODIFIED VERSION", pos,
                0.35f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);
            
            // notify the player when a newer olmod verision is available
            if (OlmodVersion.RunningVersion < OlmodVersion.LatestKnownVersion)
            {
                pos = new Vector2(UIManager.UI_RIGHT - 10f, -155f - 60f + 50f + 60f);
                __instance.DrawStringSmall("OLMOD UPDATE AVAILABLE", pos,
                    0.35f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);

                __instance.SelectAndDrawHalfItem($"GET OLMOD {OlmodVersion.LatestKnownVersion}", new Vector2(UIManager.UI_RIGHT - 140f, 279f), 12, false);
            }
        }
    }

    // On select, get latest olmod version from main menu
    [HarmonyPatch(typeof(MenuManager), "MainMenuUpdate")]
    class PHandleOlmodUpdateSelect
    {
        private static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE && !NetworkManager.IsHeadless() && UIManager.PushedSelect(-1))
            {
                if (UIManager.m_menu_selection == 12)
                {
                    Application.OpenURL(OlmodVersion.NewVersionReleasesUrl);
                    MenuManager.PlayCycleSound(1f, 1f);
                }
            }
        }
    }

}