using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// contributed by terminal
namespace GameMod
{
    //Replace loadout Reflex with Impulse in loadouts (data message) on server. Targets the Warrior loadout (0) specifically.
    [HarmonyPatch(typeof(LoadoutDataMessage), "GetMpLoadoutWeapon1")]
    class MPLoadoutsReplaceReflex
    {
        static void Postfix(int idx, ref WeaponType __result)
        {
            if (idx == 0)
                __result = WeaponType.IMPULSE;
        }
    }

    //Replace Reflex with Impulse in loadouts (menu). Targets the Warrior loadout (0) specifically.
    [HarmonyPatch(typeof(Player), "GetMpLoadoutWeapon1")]
    class MPLoadoutsReplaceReflexInMenu
    {
        static void Postfix(int idx, ref WeaponType __result)
        {
            if (idx == 0)
                __result = WeaponType.IMPULSE;
        }
    }

    // Change loadout unlocks to 0
    [HarmonyPatch]
    class MPUnlockAllLoadouts
    {
        static bool Prepare()
        {
            for (int i = 0, len = Player.MP_XP_UNLOCK_LOADOUTS.Length; i < len; i++)
                Player.MP_XP_UNLOCK_LOADOUTS[i] = 0;
            return false;
        }
    }

    // Tricks the modifiers menu into checking if user's XP is greater than 0
    [HarmonyPatch(typeof(Player), "GetModifierMinXP")]
    class MPUnlockAllModifiers
    {
        static void Postfix(ref int __result)
        {
            __result = 0;
        }
    }

    // Change warrior loadout to custom loadout with local definitions (reflex changed to impulse) when sending to server
    [HarmonyPatch(typeof(Client), "SendPlayerLoadoutToServer")]
    class MPUnlockAllSendPlayerLoadoutToServer
    {
        static void ModifyLoadout(LoadoutDataMessage loadoutDataMessage)
        {
            if (loadoutDataMessage.m_mp_loadout1 == 0 && loadoutDataMessage.m_mp_loadout2 != 6)
                loadoutDataMessage.m_mp_loadout1 = 6;
            else if (loadoutDataMessage.m_mp_loadout2 == 0 && loadoutDataMessage.m_mp_loadout1 != 6)
                loadoutDataMessage.m_mp_loadout2 = 6;
            else
                return;
            loadoutDataMessage.m_mp_custom1_w1 = Player.GetMpLoadoutWeapon1(0);
            loadoutDataMessage.m_mp_custom1_m1 = Player.GetMpLoadoutMissile1(0);
            loadoutDataMessage.m_mp_custom1_m2 = Player.GetMpLoadoutMissile2(0);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Newobj)
                    state = 1;
                else if (state == 1 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "GetClient")
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPUnlockAllSendPlayerLoadoutToServer), "ModifyLoadout"));
                    state = 2;
                }
                yield return code;
            }
        }
    }
}
