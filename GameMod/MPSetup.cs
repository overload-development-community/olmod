using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(NetworkMatch), "SetDefaultMatchSettings")]
    class MPSetupDefault
    {
        public static void Postfix()
        {
            MPTeams.NetworkMatchTeamCount = 2;
            MPJoinInProgress.NetworkMatchEnabled = false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ApplyPrivateMatchSettings")]
    class MPSetupApplyPMD
    {
        public static void Postfix()
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
            }
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    class MPSetupAcceptedToLobby
    {
        static void Postfix()
        {
            MPSetupApplyPMD.Postfix();
        }
    }

    [HarmonyPatch(typeof(MenuManager), "BuildPrivateMatchData")]
    class MPSetupBuildPMD
    {
        static void Postfix(PrivateMatchDataMessage __result)
        {
            if ((MPTeams.MenuManagerTeamCount > 2 || MPJoinInProgress.MenuManagerEnabled) && MenuManager.m_mp_lan_match)
            {
                __result.m_name += new string(new char[] { '\0', (char)(
                    ((Math.Max(2, MPTeams.MenuManagerTeamCount) - 2) & 7) |
                    (MPJoinInProgress.MenuManagerEnabled ? 8 : 0))});
            }
            Debug.Log("Build PMD name " + String.Join(",", __result.m_name.Select(x => ((int)x).ToString()).ToArray()));
            if (MPJoinInProgress.MenuManagerEnabled)
                __result.m_num_players_to_start = 1;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "InitMpPrivateMatch")]
    class MPSetupMenuInit
    {
        static void Postfix()
        {
            MPTeams.MenuManagerTeamCount = 2;
            MPJoinInProgress.MenuManagerEnabled = false;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "LoadPreferences")]
    class MPSetupLoad
    {
        static void Postfix()
        {
            MPTeams.MenuManagerTeamCount = MenuManager.LocalGetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
            MPJoinInProgress.MenuManagerEnabled = MenuManager.LocalGetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
        }
    }

    [HarmonyPatch(typeof(MenuManager), "SavePreferences")]
    class MPSetupSave
    {
        public static void Store()
        {
            MenuManager.LocalSetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
            MenuManager.LocalSetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
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
