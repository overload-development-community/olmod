using HarmonyLib;
using Overload;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
	public static class MPSoundOcclusion
    {	
		//                                    N/A   LOW     MED     STRONG			
		public static float[] MAXDISTS =    { 0f,   110f,   100f,   95f };    // xtra strong 85f
		public static float[] BOOSTS =      { 0f,   0.10f,  0.15f,  0.20f };  // xtra strong 0.25f
		public static float[] LOWFREQS =    { 0f,   950f,   800f,   500f };   // xtra strong 500f
		public static float[] CUTOFFS =     { 0f,   9000f,  9500f,  10000f }; // xtra strong 10500f
		// actual cutoff starting point is currently targetted at ~7khz since we are clamping to 15 units minimum distance below

		// Change this at your peril, gotta recalculate the curves if you do
		public const float MINDIST = 15f;

		public static float CutoffFreq;
		public static float BoostAmount;

		public static int CueState = 0;
		public static bool Occluded;
	}

	[HarmonyPatch(typeof(UnityAudio), "CreateAudioSourceAndObject")]
	internal class MPSoundOcclusion_UnityAudio_CreateAudioSourceAndObject
	{
		static void Postfix(ref GameObject[] ___m_a_object, int i)
        {
			//GameManager.m_audio.m_debug_sounds = true;

			if (___m_a_object != null)
			{
				MPSoundExt.m_a_source[i] = ___m_a_object[i].GetComponent<AudioSource>();
				if (___m_a_object[i].GetComponent<AudioLowPassFilter>() == null)
				{
					MPSoundExt.m_a_filter[i] = ___m_a_object[i].AddComponent<AudioLowPassFilter>();
				}
				else
				{
					MPSoundExt.m_a_filter[i] = ___m_a_object[i].GetComponent<AudioLowPassFilter>();
					// this *shouldn't* happen but who knows with Overload
				}
				MPSoundExt.m_a_filter[i].cutoffFrequency = 22000f;
				MPSoundExt.m_a_filter[i].enabled = false;
			}
		}
	}

	[HarmonyPatch(typeof(GameplayManager), "DoneLevel")]
	internal class MPSoundOcclusion_GameplayManager_DoneLevel
	{
		static void Postfix()
		{
			foreach (AudioLowPassFilter f in MPSoundExt.m_a_filter)
			{
				f.cutoffFrequency = 22000f;
				f.enabled = false;
			}
		}
	}

	// Sets an int to track state for use with Occlusion in the Cue play commands since they can fire off several layers simultaneously.
	// Previously this was resulting in several Linecasts from the same position. Should make things more efficient by allowing
	// the PlaySound method to only do the check once for each cue.
	// Needs to patch PlayCue2D, PlayCuePos, and PlayThunderboltFire. See the next method.
	public class MPSoundOcclusion_SFXCueManager_PlayCuePatch
	{
		public static void Prefix()
		{
			MPSoundOcclusion.CueState = 1;
		}

		public static void Postfix()
		{
			MPSoundOcclusion.CueState = 0;
		}
	}

	// This whole nonsense is because SXFCueManager is a static class with a static constructor. All sorts of fun breakage
	// using the regular patch method because the constructor ends up calling too early and there's nothing you can do
	// to prevent it. You need to patch waaaaay at the end of the process and this seems to be the only way to force it to
	// do this, as recommended by the Harmony devs on Discord.
	[HarmonyPatch(typeof(PilotManager), "Initialize")]
	class MPSoundOcclusion_PilotManager_Initialize
	{
		static void Postfix()
        {
			var harmony = new Harmony("olmod.postpatcher");

			var orig1 = typeof(SFXCueManager).GetMethod("PlayCue2D");
			var orig2 = typeof(SFXCueManager).GetMethod("PlayCuePos");
			var orig3 = typeof(SFXCueManager).GetMethod("PlayThunderboltFire");

			var prefix = typeof(MPSoundOcclusion_SFXCueManager_PlayCuePatch).GetMethod("Prefix");
			var postfix = typeof(MPSoundOcclusion_SFXCueManager_PlayCuePatch).GetMethod("Postfix");

			harmony.Patch(orig1, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			harmony.Patch(orig2, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			harmony.Patch(orig3, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
		}
	}

	// Now that that whole flustercluck is out of the way...
	// Main logic starts happening here
	[HarmonyPatch(typeof(UnityAudio), "PlaySound")]
	internal class MPSoundOcclusion_UnityAudio_PlaySound
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
		{
			foreach (var code in codes)
			{
				if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(AudioSource), "Play"))
				{
					yield return new CodeInstruction(OpCodes.Nop);
				}
				else
				{
					yield return code;
				}
			}
		}

		static void Postfix(int __result, Vector3 pos3d)
		{	
			if (__result != -1) // if -1 then we ran out of audio slots, skip the whole thing
			{
				// If pos3d == Vector3.zero, then it's almost without a doubt a 2D cue on the local client. It's beyond infeasible that sound could accidentally come from *exactly* this point.
				// If it ever does, well then you get 1 glitched cue and you should have bought a lottery ticke
				if (Menus.mms_audio_occlusion_strength == 0 || pos3d == Vector3.zero)
                {
					MPSoundOcclusion.Occluded = false;
                }
				else
				{
					if ((!MPObserver.Enabled || MPObserver.ObservedPlayer != null) && !GameplayManager.IsDedicatedServer()) // last check probably not necessary but whatever
					{
						// if we're mid-cue there's multiple sounds being played from the same location - use the previous Linecast calculations for efficiency
						if (MPSoundOcclusion.CueState < 2)
						{
							Vector3 shipPos = GameManager.m_player_ship.transform.localPosition;

							RaycastHit ray1;
							if (Physics.Linecast(pos3d, shipPos, out ray1, 67256320))
							{
								// we don't have line-of-sight
								// This is the "Tier 3" approach, taking both distance to target and thickness of obstruction into account
								RaycastHit ray2;
								Physics.Linecast(shipPos, pos3d, out ray2, 67256320);

								float maxdist = MPSoundOcclusion.MAXDISTS[Menus.mms_audio_occlusion_strength];
								float cutoff = MPSoundOcclusion.CUTOFFS[Menus.mms_audio_occlusion_strength];
								float lowfreq = MPSoundOcclusion.LOWFREQS[Menus.mms_audio_occlusion_strength];
								float boost = MPSoundOcclusion.BOOSTS[Menus.mms_audio_occlusion_strength];

								float p2pDist = Vector3.Distance(pos3d, shipPos); // point to point distance

								float thick = Mathf.Clamp(p2pDist - ray1.distance - ray2.distance, 1f, maxdist); // how thick the obstruction is, clamped
								p2pDist = Mathf.Clamp(p2pDist, MPSoundOcclusion.MINDIST, maxdist); // clamp the p2pDist value
								float factor = (maxdist - (0.6f * thick + 0.4f * p2pDist)) / (maxdist);

								MPSoundOcclusion.CutoffFreq = lowfreq + (cutoff * factor * factor); // exponential curve
								MPSoundOcclusion.BoostAmount = boost * (0.85f - factor);
								MPSoundOcclusion.Occluded = true;
							}
							else
							{
								MPSoundOcclusion.Occluded = false;
							}
						}
						if (MPSoundOcclusion.CueState == 1) // we're in a multicue, pause calculations
                        {
							MPSoundOcclusion.CueState = 2;
						}
					}
				}
				if (MPSoundOcclusion.Occluded)
				{
					MPSoundExt.m_a_filter[__result].cutoffFrequency = MPSoundOcclusion.CutoffFreq;
					MPSoundExt.m_a_source[__result].volume = MPSoundExt.m_a_source[__result].volume + MPSoundOcclusion.BoostAmount; // slight boost to volume as range increases to counter the HF rolloff
					MPSoundExt.m_a_filter[__result].enabled = true;
				}
				else
				{
					// restore the normal filter
					MPSoundExt.m_a_filter[__result].cutoffFrequency = 22000f;
					MPSoundExt.m_a_filter[__result].enabled = false;
				}

				MPSoundExt.m_a_source[__result].Play();  // Nop'd out in the transpiler
			}
		}
	}
}