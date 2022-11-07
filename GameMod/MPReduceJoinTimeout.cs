using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;


namespace GameMod
{
    // change the 60 second "Player Joining" timeout to 5 seconds
    [HarmonyPatch]
    class MPReduceJoinTimeout
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostPlayerMatchmakerInfo", AccessTools.all).GetMethod("GetStatus");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0) {
                    if (code.opcode == OpCodes.Ldc_R8 && (double)code.operand == 60) {
                        state = 1;
                        yield return new CodeInstruction(OpCodes.Ldc_R8, 5.0);
                    } else {
                        yield return code;
                    }
                } else {
                    yield return code;
                }
            }
            if (state != 1) {
                Debug.LogFormat("MPReduceJoinTimeout: transpiler failed at state {0}",state);
            }
        }
    }
}
