using HarmonyLib;
using Overload;

namespace GameMod
{
    [HarmonyPatch(typeof(GameManager), "FixCurrentDateFormat")]
    class DisableTimeCheat
    {
        static void Prefix()
        {
            GameManager.m_gm.DisableTimeCheatingDetector();
        }
    }
}
