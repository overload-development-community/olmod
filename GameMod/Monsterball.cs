using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

// by terminal
namespace GameMod
{
    //postfix weapon collision to Monsterball Awake()
    [HarmonyPatch(typeof(MonsterBall), "Awake")]
    class MonsterballEnableWeaponCollision
    {
        static void Postfix()
        {
            Physics.IgnoreLayerCollision(31, 13, false);
        }
    }

    //reset the monsterball weapon collision once match is over
    [HarmonyPatch(typeof(NetworkManager), "OnSceneUnloaded")]
    class MonsterballDisableWeaponCollision
    {
        static void Postfix()
        {
            Physics.IgnoreLayerCollision(31, 13, true);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SubtractPointForTeam")]
    class MonsterballDisableSuicidePenalty
    {
        //make suicides not count as -1 score in Monsterball
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            object operand = codes[1].operand;
            codes[1] = new CodeInstruction(OpCodes.Ldc_I4_1, null);
            CodeInstruction insertedCode = new CodeInstruction(OpCodes.Bne_Un, operand);
            codes.Insert(2, insertedCode);
            return codes;
        }
    }
}