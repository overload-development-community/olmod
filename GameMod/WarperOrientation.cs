using UnityEngine;
using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace GameMod
{
    // Try to fix object rotation coming out of a warper
    [HarmonyPatch(typeof(TriggerWarper), "TeleportObject")]
    class TriggerWarper_RotationFix
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            const int BufferSize = 3;
            int state = 1;
            var buffer = new List<CodeInstruction>(BufferSize);

            foreach (var i in code)
            {
                if (buffer.Count >= BufferSize)
                {
                    if (state != 2)
                    {
                        yield return buffer[0];
                    }
                    buffer.RemoveAt(0);
                }
                buffer.Add(i);

                switch (state)
                {
                    case 1:
                        // 1. Find spot where Transform::get_rotation is called on arg 1
                        if (i.opcode == OpCodes.Callvirt
                            && ((MethodInfo)i.operand).DeclaringType == typeof(Transform)
                            && ((MethodInfo)i.operand).Name == "get_rotation")
                        {
                            var prev = buffer[BufferSize - 1 - 1];
                            if (prev.opcode != OpCodes.Ldarg_1)
                            {
                                break;
                            }

                            // flush buffer
                            foreach (var j in buffer)
                            {
                                yield return j;
                            }
                            buffer.Clear();
                            state = 2;
                        }
                        break;
                    case 2:
                        // 2. Delete old rotation code (continues until set_rotation with arg 1), then insert new rotation code
                        if (i.opcode == OpCodes.Callvirt
                            && ((MethodInfo)i.operand).DeclaringType == typeof(Transform)
                            && ((MethodInfo)i.operand).Name == "set_rotation")
                        {
                            var prev = buffer[BufferSize - 1 - 2];
                            if (prev.opcode != OpCodes.Ldarg_1)
                            {
                                break;
                            }

                            // Rotation formula:
                            // obj_transform.rotation = this.dest_warper.c_transform.rotation * Quaternion.Inverse( this.c_transform.rotation * Quaternion.AngleAxis( 180, Vector3.up ) ) * obj_transform.rotation
                            //
                            // Explanation
                            // - The quaternion pointing into the entrance warper is (this.c_transform.rotation * Quaternion.AngleAxis( 180, Vector3.up ))
                            //   The up vector is not relative to c_transform because the AngleAxis rotation will be applied starting from c_transform's frame of reference
                            //   i.e. we're in a context where Vector3.up already represents the warper's up vector
                            // - Suppose we want a rotation A which represents the difference between the entrance warper's orientation (W) and the warped object's orientation (X)
                            //   Then W * A = X
                            //     => Quaternion.Inverse(W) * W * A = Quaternion.Inverse(W) * X
                            //     => A = Quaternion.Inverse(W) * X
                            //   because a transform and its inverse cancel each other out
                            // - And hopefully setting the warped object's orientation to be the destination warper's orientation, but rotated by A,
                            //   makes it feel like the warp preserves the object's orientation.

                            // stack starts with obj_transform.rotation
                            // save obj_transform.rotation
                            yield return new CodeInstruction(OpCodes.Stloc_2);
                            // this.dest_warper.c_transform.rotation
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerWarper), "dest_warper"));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerWarper), "c_transform"));
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(UnityEngine.Transform), "rotation").GetGetMethod());
                            // this.c_transform.rotation
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerWarper), "c_transform"));
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(UnityEngine.Transform), "rotation").GetGetMethod());
                            // Quaternion.AngleAxis(180, Vector3.up)
                            yield return new CodeInstruction(OpCodes.Ldc_R4, 180.0f);
                            yield return new CodeInstruction(OpCodes.Call, typeof(UnityEngine.Vector3).GetProperty("up", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "AngleAxis"));
                            // Quaternion.Inverse(this.c_transform.rotation * Quaternion.AngleAxis(180, Vector3.up))
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }));
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "Inverse"));
                            // multiply with this.dest_warper.c_transform.rotation
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }));
                            // multiply with obj_transform.rotation
                            yield return new CodeInstruction(OpCodes.Ldloc_2);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }));
                            // save into obj_transform.rotation
                            yield return new CodeInstruction(OpCodes.Stloc_2);
                            yield return new CodeInstruction(OpCodes.Ldarg_1);
                            yield return new CodeInstruction(OpCodes.Ldloc_2);
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(UnityEngine.Transform), "rotation").GetSetMethod());

                            buffer.Clear();
                            state = 3;
                        }
                        break;
                    case 3:
                    default:
                        break;
                }
            }

            foreach (var i in buffer)
            {
                yield return i;
            }
        }
    }
}

