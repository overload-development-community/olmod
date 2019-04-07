using System.Collections.Generic;
using Harmony;
using Overload;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using System;

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

    // increase PingsMessage to 16 entries
    [HarmonyPatch(typeof(PingsMessage), MethodType.Constructor)]
    class MPMaxPings
    {
        static bool Prefix(PingsMessage __instance)
        {
            var pings = __instance.m_pings = new ClientPing[16];
            for (int i = 0; i < 16; i++)
                pings[i] = new ClientPing();
            return false;
        }
    }

    // set ServerPing max to 16 (will crash original client!)
    [HarmonyPatch(typeof(ServerPing), "Update")]
    class MPMaxServerPing
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Ldc_I4_8)
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
                else
                    yield return code;
        }
    }

    // set InitLobby max to 16
    [HarmonyPatch(typeof(NetworkMatch), "InitLobby")]
    class MPMaxInitLobby
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Ldc_I4_8)
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
                else
                    yield return code;
        }
    }

    // set client config max to 16
    [HarmonyPatch(typeof(Client), "ConfigureConnection")]
    class MPMaxClientConfig
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Ldc_I4_8)
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
                else
                    yield return code;
        }
    }


    // set TryLocalMatchmaking max to 16
    [HarmonyPatch]
    class MPMaxTryLocalMatchmaking
    {
        static MethodBase TargetMethod()
        {
            foreach (var x in typeof(NetworkMatch).GetNestedTypes(BindingFlags.NonPublic))
                if (x.Name.Contains("TryLocalMatchmaking"))
                {
                    var m = AccessTools.Method(x, "<>m__0");
                    if (m != null) {
                        Debug.Log("MPMaxTryLocalMatchmaking TargetMethod found");
                        return m;
                    }
                }
            Debug.Log("MPMaxTryLocalMatchmaking TargetMethod not found");
            return null;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Ldc_R8 && (double)code.operand == 8.0)
                    yield return new CodeInstruction(OpCodes.Ldc_R8, 16.0);
                else
                    yield return code;
        }
    }

    // increase PlayerSnapshotToClientMessage to 16 entries
    [HarmonyPatch(typeof(Player), "NetworkPlayerAwake")]
    class MPMaxPlayerSnapshot
    {
        static void Prefix(Player __instance)
        {
            __instance.m_snapshot_buffer.m_snapshots = new PlayerSnapshot[16];
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Ldc_I4_8)
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
                else
                    yield return code;
        }
    }

    [HarmonyPatch(typeof(PlayerSnapshotToClientMessage), "Deserialize")]
    class MPMaxPlayerSnapshotMsg
    {
        static void Prefix(PlayerSnapshotToClientMessage __instance)
        {
            if (__instance.m_snapshots.Length == 8)
                __instance.m_snapshots = new PlayerSnapshot[16];
        }
    }
}
