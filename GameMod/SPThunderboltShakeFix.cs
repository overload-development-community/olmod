using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting.Messaging;
using System.Text;
using UnityEngine;

namespace GameMod
{
    internal class SPThunderboltShakeFix
    {
        // In the singleplayer while charging the thunderbolt. torque towards a random direction gets applied on the ship.
        // To bad that the amount of torque is simply  charge / frametime 
        // So with the charge always being in the same range the ships get thrown more and more violently the lower
        // the frametime gets (and also more frequently since the method is built as sth that
        // should only get called at a fixed rate but ends up getting called every frame)
        // 
        // https://www.youtube.com/watch?v=tfDDnJwi9LY

        [HarmonyPatch(typeof(PlayerShip), "ThunderCharge")]
        class FixThunderboltShake
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                int state = 0;
                foreach (var code in codes)
                {
                    // ldloc_0 is not unique, so only look for it after passing the PlayCameraShake call
                    if (state == 0 && code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "PlayCameraShake")
                        state = 1;

                    if (state == 1 && code.opcode == OpCodes.Ldloc_0)
                    {
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 0.0166667f);
                        state = 2;
                        continue;
                    }
                    yield return code;
                }
            }
        }
    }
}
