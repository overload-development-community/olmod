using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    // not ready yet

    public enum NewSounds
    {
        // last index in the original table is 486, we start there. Add new clips before num and bump the counts up.
        LancerCharge3s = 486,
        LancerCharge2s5 = 487,
        LancerCharge2s = 488,
        PlasmaFire1 = 489,
        PlasmaFire2 = 490,
        PlasmaFire3 = 491,
        MortarExplode = 492,
        MortarExplode2 = 493,
        MortarFire1 = 494,
        MortarFire2 = 495,
        MortarFire3 = 496,
        NUM = 497
        //tbbolt = 486,
        //novathrust = 487,
        //devthrust = 488,
        //NUM = 489
    }

    public static class MPSoundExt
    {
        public static AssetBundle ab;
        public static GameObject[] m_a_object = new GameObject[512];
        public static AudioSource[] m_a_source = new AudioSource[512];
        public static AudioLowPassFilter[] m_a_filter = new AudioLowPassFilter[512];
    }

    // not ready yet

    
    // loads additional sounds added to the "audio" assetbundle and listed in the NewSounds enum.
    [HarmonyPatch(typeof(UnityAudio), "LoadSoundEffects")]
    internal class MPSoundExt_UnityAudio_LoadSoundEffects
    {
        static void Postfix(UnityAudio __instance, ref AudioClip[] ___m_sound_effects, ref float[] ___m_sound_effects_volume, ref float[] ___m_sound_effects_pitch_amt, ref float[] ___m_sound_effects_cooldown)
        {
            //__instance.m_debug_sounds = true;

            int num = (int)NewSounds.NUM;

            Array.Resize(ref ___m_sound_effects, num);
            Array.Resize(ref ___m_sound_effects_volume, num);
            Array.Resize(ref ___m_sound_effects_pitch_amt, num);
            Array.Resize(ref ___m_sound_effects_cooldown, num);

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMod.Resources.audio"))
            {
                MPSoundExt.ab = AssetBundle.LoadFromStream(stream);

                for (int i = 486; i < num; i++)
                {
                    //Debug.Log("CCC loading audio clip " + ((NewSounds)i).ToString());
                    ___m_sound_effects[i] = UnityEngine.Object.Instantiate(MPSoundExt.ab.LoadAsset<AudioClip>(((NewSounds)i).ToString()));
                    ___m_sound_effects_volume[i] = 1f;
                    ___m_sound_effects_pitch_amt[i] = 0f;
                    ___m_sound_effects_cooldown[i] = 0f;
                }

                //Debug.Log("CCC clip loaded " + ___m_sound_effects[486].ToString());
                //Debug.Log("CCC clip state is " + ___m_sound_effects[486].loadState.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(UnityAudio), "UpdateAudio")]
    internal class MPSoundExt_UnityAudio_UpdateAudio
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4 && (int)code.operand == 486)
                {
                    code.operand = (int)NewSounds.NUM;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UnityAudio), "StopSound")]
    public static class MPSoundExt_UnityAudio_StopSound
    {
        public static bool Prefix(int idx)
        {
            //Debug.Log("AUDIO stopping " + idx);
            if (idx > -1)
            {
                if (idx < MPSoundExt.m_a_source.Length && MPSoundExt.m_a_source[idx] != null)
                {
                    if (MPSoundExt.m_a_source[idx].isPlaying)
                    {
                        MPSoundExt.m_a_source[idx].Stop();
                    }
                }
                else
                {
                    Debug.Log("AUDIO ERROR - Attempted to stop an audio source that didn't exist at index " + idx);
                }
            }
            return false;
        }
    }

    /*
     * *************************************************
     * LOOPED SOUND PERFORMANCE OPTIMIZATIONS BEGIN HERE
     * *************************************************
     */

    /* I think this one is single player only? May re-enable. It was a copy of the one for AddEnergyDefault
    // Increases the loop wait time for the energy center sounds. There is a noticeable performance hit when charging.
    [HarmonyPatch(typeof(Player), "AddWeakEnergy")]
    internal class MPSoundExt_Player_AddWeakEnergy
    */

    // Increases the loop wait time for the energy center sounds. There is a noticeable performance hit when charging.
    [HarmonyPatch(typeof(Player), "AddEnergyDefault")]
    internal class MPSoundExt_Player_AddEnergyDefault
    {        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool state = false;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 0.1f)
                {
                    if (!state) // first one is the sound cue timer
                    {
                        code.operand = 0.5f;
                        state = true;
                    }
                    else // remaining ones are for boost energy and heat recharge
                    {
                        code.operand = 0.2f;
                    }
                }

                yield return code;
            }
        }
    }

    // Uses an unused bool field "m_one_time" in TriggerEnergy to cause it to only activate every second frame with a double energy payload from stock.
    // Shouldn't strictly be necessary but apparently the energy center causes a lot of activity simultaneously and this *should* cut that
    // in half with no gameplay side-effect. This could have been done faster (but a little less efficiently) with a prefix probably.
    [HarmonyPatch(typeof(TriggerEnergy), "OnTriggerStay")]
    internal class MPSoundExt_TriggerEnergy_OnTriggerStay
    {
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            bool state = false;
            Label a = ilGen.DefineLabel();

            foreach (var code in codes)
            {
                if (!state)
                {
                    state = true; // only run once
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerEnergy), "m_one_time"));
                    yield return new CodeInstruction(OpCodes.Brtrue, a); // if true, run the original actual method
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(TriggerEnergy), "m_one_time"));
                    yield return new CodeInstruction(OpCodes.Ret); // if false above, this exits the method early after setting m_one_time to true
                    CodeInstruction after = new CodeInstruction(OpCodes.Ldarg_0);
                    after.labels.Add(a);
                    yield return after; // "brtrue" jumps here if m_one_time is true, sets it to false, and continues the method
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(TriggerEnergy), "m_one_time"));
                }

                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 15f)
                {
                    code.operand = 30f; // double the energy recharge payout since we halved the active recharge frames
                }

                yield return code;
            }
        }
    }

    /* ====== This needs to be tested properly and re-enabled in a future version - right now it works, but has not been run through the wringer, so it's commented out for now
    
    // slightly reduces the maximum number of player damage sound effects happening per second from 25-40 *per type* down to ~20.
    // Lightens the load a bit caused by extra physics linecasts in heavy games when something like Flak is pelting multiple
    // targets simultaneously.
    [HarmonyPatch(typeof(PlayerShip), "RpcApplyDamageEffects")]
    internal class MPSoundExt_PlayerShip_RpcApplyDamageEffects
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && ((float)code.operand == 0.025f || (float)code.operand == 0.04f))
                {
                    code.operand = 0.05f;   // *** THIS NEEDS TO BE FINE-TUNED AND TESTED THOROUGHLY to make sure there are still enough damage cues available that no one gets blindsided
                }
                yield return code;
            }
        }
    }
    */
}