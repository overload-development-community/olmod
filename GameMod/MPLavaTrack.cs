using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    class MPLavaTrack
    {
        public static GameObject CurrentOwner;
    }

    // Store current owner so we can tag all created explosions with this owner
    [HarmonyPatch(typeof(Projectile), "ProcessCollision")]
    class MPLavaTrackProcessCollision
    {
        static void Prefix(GameObject ___m_owner, GameObject collider, ProjPrefab ___m_type)
        {
            if (GameplayManager.IsMultiplayerActive && ___m_owner != null && ___m_owner.GetComponent<Player>())
                MPLavaTrack.CurrentOwner = ___m_owner;
            else
                MPLavaTrack.CurrentOwner = null;
            //Debug.Log("ProcessCollision layer=" + collider.layer + " owner = " + MPLavaTrack.CurrentOwner + " type=" + ___m_type);
        }
        static void Postfix()
        {
            MPLavaTrack.CurrentOwner = null;
            //Debug.Log("ProcessCollision done");
        }
    }
    
    /*
    // Play is called in StartParticleInstant after SetExplosionProperties
    [HarmonyPatch(typeof(ParticleElement), "Play")]
    class MPLavaTrackSetRobot
    {
        static void Prefix(Explosion ___c_exp)
        {
            Debug.Log("Play ___c_exp=" + ___c_exp + " owner=" + (___c_exp ? ___c_exp.m_owner : null));
            if (!___c_exp || ___c_exp.m_owner != null)
                return;
            ___c_exp.m_owner = MPLavaTrack.CurrentOwner;
            Debug.Log("Play owner=" + ___c_exp.m_owner);
        }
    }

    [HarmonyPatch(typeof(ParticleSubManager), "StartParticleInstant")]
    class MPLavaLog
    {
        static void Prefix()
        {
            Debug.Log("ParticleSubManager.StartParticleInstant");
        }
    }
    */

    // call SetExplosionProperties(MPLavaTrack.CurrentOwner, false, ProjPrefab.none); if proj == null in StartParticleInstant
    [HarmonyPatch(typeof(ParticleSubManager), "StartParticleInstant")]
    class MPLavaTrackStartParticleInstant
    {
        static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = before ldarg proj, 1 = before brfase, 2 = before callvirt SetExplosionProperties, 3 = done
            Label endLabel = default(Label);
            Label elseLabel = ilGen.DefineLabel();
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldarg_S && (byte)code.operand == 5) // proj
                {
                    state = 1;
                }
                else if (state == 1 && code.opcode == OpCodes.Brfalse)
                {
                    endLabel = (Label)code.operand;
                    code.operand = elseLabel;
                    state = 2;
                }
                else if (state == 2 && code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "SetExplosionProperties")
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Br, endLabel);
                    yield return new CodeInstruction(OpCodes.Ldloc_0) { labels = { elseLabel } }; // else: ld c_exp
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPLavaTrack), "CurrentOwner")); // ld CurrentOwner
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0); // ld false
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0); // ld ProjPrefab.none
                    yield return new CodeInstruction(code.opcode, code.operand);
                    state = 3;
                    continue;
                }
                yield return code;
            }
        }
    }
}
