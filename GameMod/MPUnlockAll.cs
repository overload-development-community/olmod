using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// contributed by terminal
namespace GameMod
{
    // Tricks the modifiers menu into checking if user's XP is greater than 0
    [HarmonyPatch(typeof(Player), "GetModifierMinXP")]
    class MPUnlockAllModifiers
    {
        static void Postfix(ref int __result)
        {
            __result = 0;
        }
    }
}
