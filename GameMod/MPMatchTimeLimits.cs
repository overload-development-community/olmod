using HarmonyLib;
using Overload;

namespace GameMod
{
    class MPMatchTimeLimits
    {
        public static int MatchTimeLimit = 600;
    }

    [HarmonyPatch(typeof(MenuManager), "GetMMSTimeLimit")]
    class MPMatchTimeLimits_MenuManager_GetMMSTimeLimit
    {
        static bool Prefix(ref string __result)
        {
            if (Menus.mms_match_time_limit == 0)
            {
                __result = "NONE";
            }
            else
            {
                __result = (Menus.mms_match_time_limit / 60).ToString() + " MINUTE" + ((Menus.mms_match_time_limit > 60) ? "S" : "");
            }
            return false;
        }
    }

}
