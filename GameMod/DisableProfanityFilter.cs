using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    class DisableProfanityFilter
    {
        /// <summary>
        /// Author: luponix
        /// Created: 2022-03-07
        /// Removes the effects of the Profanity filter by returning the string immediatly
        /// and shifts the responsibility to the clients
        /// </summary>

        public static bool profanity_filter = false;

        [HarmonyPatch(typeof(StringParse), "ProfanityFilter")]
        class DisableProfanityFilter_StringParse_ProfanityFilter
        {
            static bool Prefix(string s, ref string __result)
            {
                if (GameplayManager.IsDedicatedServer() | !profanity_filter)
                {
                    __result = s;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(NetworkMessageManager), "AddFullChatMessage")]
        class DisableProfanityFilter_NetworkMessageManager_AddFullChatMessage
        {
            static void Prefix(ref string msg)
            {
                msg = StringParse.ProfanityFilter(msg);
            }
        }
    }
}
