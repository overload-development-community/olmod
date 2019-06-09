using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(NetworkMatch), "SetDefaultMatchSettings")]
    class MPSetupDefault
    {
        public static void Postfix()
        {
            MPDownloadLevel.Reset();
            MPTeams.NetworkMatchTeamCount = 2;
            MPJoinInProgress.NetworkMatchEnabled = false;
            RearView.MPNetworkMatchEnabled = false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ApplyPrivateMatchSettings")]
    class MPSetupApplyPMD
    {
        public static void ApplyMatchOLModData()
        {
            Debug.Log("Apply PMD name " + String.Join(",", NetworkMatch.m_name.Select(x => ((int)x).ToString()).ToArray()));
            var i = NetworkMatch.m_name.IndexOf('\0');
            if (i == -1)
            {
                MPSetupDefault.Postfix();
            }
            else
            {
                MPTeams.NetworkMatchTeamCount = (NetworkMatch.m_name[i + 1] & 7) + 2;
                MPJoinInProgress.NetworkMatchEnabled = (NetworkMatch.m_name[i + 1] & 8) != 0;
                RearView.MPNetworkMatchEnabled = (NetworkMatch.m_name[i + 1] & 16) != 0;
            }
        }

        private static void Postfix(ref bool __result, PrivateMatchDataMessage pmd)
        {
            ApplyMatchOLModData();
            if (!__result && !Config.NoDownload && !string.IsNullOrEmpty(pmd.m_addon_level_name_hash)) // unknown level?
            {
                MPDownloadLevel.StartGetLevel(pmd.m_addon_level_name_hash);
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    class MPSetupAcceptedToLobby
    {
        static void Postfix()
        {
            MPSetupApplyPMD.ApplyMatchOLModData();
        }
    }

    [HarmonyPatch(typeof(MenuManager), "BuildPrivateMatchData")]
    class MPSetupBuildPMD
    {
        static void Postfix(PrivateMatchDataMessage __result)
        {
            if ((MPTeams.MenuManagerTeamCount > 2 || MPJoinInProgress.MenuManagerEnabled || RearView.MPMenuManagerEnabled) && MenuManager.m_mp_lan_match)
            {
                __result.m_name += new string(new char[] { '\0', (char)(
                    ((Math.Max(2, MPTeams.MenuManagerTeamCount) - 2) & 7) |
                    (MPJoinInProgress.MenuManagerEnabled ? 8 : 0) |
                    (RearView.MPMenuManagerEnabled ? 16 : 0))});
            }
            Debug.Log("Build PMD name " + String.Join(",", __result.m_name.Select(x => ((int)x).ToString()).ToArray()));
            if (MPJoinInProgress.MenuManagerEnabled)
                __result.m_num_players_to_start = 1;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "InitMpPrivateMatch")]
    class MPSetupMenuInit
    {
        public static void Postfix()
        {
            MPTeams.MenuManagerTeamCount = 2;
            MPJoinInProgress.MenuManagerEnabled = false;
            RearView.MPMenuManagerEnabled = false;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "SetPreferencesDefaults")]
    class MPSetupMenuDefault
    {
        static void Postfix()
        {
            MPSetupMenuInit.Postfix();
            Console.KeyEnabled = false;
            Console.CustomUIColor = 0;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "LoadPreferences")]
    class MPSetupLoad
    {
        static void Postfix()
        {
            MPTeams.MenuManagerTeamCount = MenuManager.LocalGetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
            MPJoinInProgress.MenuManagerEnabled = MenuManager.LocalGetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
            RearView.MPMenuManagerEnabled = MenuManager.LocalGetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
            Console.KeyEnabled = MenuManager.LocalGetBool("O_CONSOLE_KEY", Console.KeyEnabled);
            Console.CustomUIColor = MenuManager.LocalGetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
        }
    }

    [HarmonyPatch(typeof(MenuManager), "SavePreferences")]
    class MPSetupSave
    {
        private static int lastXP;

        public static void Store()
        {
            if (MenuManager.LocalGetInt("PS_XP2", 0) == 0 && lastXP > 0)
                MenuManager.LocalSetInt("PS_XP2", lastXP);

            MenuManager.LocalSetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
            MenuManager.LocalSetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
            MenuManager.LocalSetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
            MenuManager.LocalSetBool("O_CONSOLE_KEY", Console.KeyEnabled);
            MenuManager.LocalSetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
        }

        private static void Prefix()
        {
            lastXP = MenuManager.LocalGetInt("PS_XP2", 0);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "Flush")
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSetupSave), "Store"));
                yield return code;
            }
        }
    }
}
