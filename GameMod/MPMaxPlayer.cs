using System.Collections.Generic;
using Harmony;
using Overload;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;

namespace GameMod
{
    // fixup max 0 players (because of truncation to 4 bits) to 16 players
    [HarmonyPatch(typeof(Overload.PrivateMatchDataMessage), "DeserializePacked")]
    class MPMaxPlayerPMD
    {
        static void Postfix(PrivateMatchDataMessage __result)
        {
            if (__result.m_max_players_for_match == 0)
                __result.m_max_players_for_match = 16;
        }
    }

    // set ConfigureConnection max connections to 16
    [HarmonyPatch(typeof(Overload.Server), "ConfigureConnection")]
    class MPMaxPlayerServConn
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_8)
                {
                    codes[i].opcode = OpCodes.Ldc_I4;
                    codes[i].operand = 16;
                    Debug.Log("Patched ConfigureConnection");
                    break;
                }
            }
            return codes;
        }
    }

    // set LocalLANHost.FindGameSessionToCreate max players to 16
    [HarmonyPatch]
    class MPMaxPlayerFindGameSess
    {
        static MethodBase TargetMethod()
        {
            Debug.Log("MPMaxPlayerFindGameSess TargetMethod");
            foreach (var x in typeof(LocalLANHost).GetNestedTypes(BindingFlags.NonPublic))
                if (x.Name.Contains("FindGameSessionToCreate"))
                    return x.GetMethod("MoveNext");
            return null;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var i = 0;
            while (i < codes.Count && (codes[i].opcode != OpCodes.Ldfld ||
                (codes[i].operand as FieldInfo).Name != "m_matchMaker"))
                i++;
            while (i < codes.Count)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_8)
                {
                    codes[i].opcode = OpCodes.Ldc_I4;
                    codes[i].operand = 16;
                    Debug.Log("Patched FindGameSessionToCreate");
                    break;
                }
                i++;
            }
            return codes;
        }
    }

    // set FindPrivateMatchGrouping max players to 16
    [HarmonyPatch(typeof(NetworkMatch), "FindPrivateMatchGrouping")]
    class MPMaxPlayerFindPMG
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int n = 0;
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_8)
                {
                    codes[i].opcode = OpCodes.Ldc_I4;
                    codes[i].operand = 16;
                    n++;
                }
            }
            Debug.Log("Patched FindPrivateMatchGrouping n=" + n);
            return codes;
        }

        static void Postfix(DistributedMatchUp.Match[] __result)
        {
            Debug.Log("FindPrivateMatchGrouping result: " + (__result == null ? "null" : __result.Length.ToString()));
        }
    }

}
