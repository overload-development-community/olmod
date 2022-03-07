using HarmonyLib;

namespace GameMod
{
    class DisableProfanityFilter
    {
        /// <summary>
        /// Author: luponix
        /// Created: 2022-03-07
        /// Removes the effects of the Profanity filter by returning the string immediatly
        /// </summary>
        [HarmonyPatch(typeof(StringParse), "ProfanityFilter")]
        class DisableProfanityFilter_StringParse_ProfanityFilter
        {
            static bool Prefix(string s, ref string __result)
            {
                __result = s;
                return false;
            }
        }
    }
}
