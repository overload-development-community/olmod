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
		
		[HarmonyPatch(typeof(Projectile), "Explode")]
		internal class MPClassic_Projectile_Explode
		{
			private static bool Prefix(Projectile __instance)
			{
				return !(isAllowed && __instance.m_type == ProjPrefab.proj_thunderbolt && __instance.m_alive);
			}
		}

		[HarmonyPatch(typeof(Projectile), "ProcessCollision")]
		internal class MPWeaponBehavior_Projectile_ProcessCollision
		{
			private static void ThunderboltExplode(Projectile proj, int layer)
			{

				bool flag = layer == 11 || layer == 16;
				if (!(flag & isAllowed))
                {
					proj.m_alive = false;
					proj.Explode(!flag);
				}
			}

			private static void FakeExplode(Projectile proj, int layer)
			{

			}

			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
			{
				var projectile_m_alive_Field = AccessTools.Field(typeof(Projectile), "m_alive");

				int state = 0;
				foreach (CodeInstruction code in codes)
				{
					if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 26)
						state = 1;

					if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Projectile), "Explode", null, null))
                    {
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Ldc_I4_0);
						yield return new CodeInstruction(OpCodes.Stfld, projectile_m_alive_Field);
						state = 2;
                    }

					if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 16)
						state = 3;

					if (state == 3 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Projectile), "Explode", null, null))
					{
						List<Label> lbls = code.labels;
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeaponBehavior_Projectile_ProcessCollision), "FakeExplode"));
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Ldloc_2);
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeaponBehavior_Projectile_ProcessCollision), "ThunderboltExplode")) { labels = lbls };
						state = 4;
						continue;
					}

					yield return code;
				}
			}
		}
	}
}
