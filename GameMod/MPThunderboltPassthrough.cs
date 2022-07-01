using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace GameMod
{
	// Original Author: Tobias
	// Allows Thunderbolt projectiles to penetrate through ships
    class MPThunderboltPassthrough
    {

		public static bool isAllowed = false;

		[HarmonyPatch(typeof(Projectile), "ProcessCollision")]
		internal class MPClassic_Projectile_ProcessCollision
		{
			private static void ThunderboltExplode(Projectile proj, int layer)
			{
				if (isAllowed)
				{
					bool flag2 = layer != 11 && layer != 16;
					if (flag2)
					{
						proj.m_alive = false;
						proj.Explode(false);
					}
				}
			}

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
			{
				int state = 0;
				foreach (CodeInstruction code in codes)
				{
					bool flag = code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 16;
					if (flag)
					{
						state = 1;
					}
					bool flag2 = state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Projectile), "Explode", null, null);
					if (flag2)
					{
						yield return code;
						yield return new CodeInstruction(OpCodes.Ldarg_0, null);
						yield return new CodeInstruction(OpCodes.Ldloc_2, null);
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPClassic_Projectile_ProcessCollision), "ThunderboltExplode", null, null));
					}
					else
					{
						yield return code;
					}
				}
				yield break;
			}
		}
		
		[HarmonyPatch(typeof(Projectile), "Explode")]
		internal class MPClassic_Projectile_Explode
		{
			private static bool Prefix(Projectile __instance)
			{
				return !(isAllowed && __instance.m_type == ProjPrefab.proj_thunderbolt && __instance.m_alive);
			}
		}


	}
}
