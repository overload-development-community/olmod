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

    /*public enum NewSounds
    {
        // last index in the original table is 486, we start there. Add new clips before num and bump the counts up.
        tbbolt = 486,
        novathrust = 487,
        devthrust = 488,
        NUM = 489
    }*/

    public static class MPSoundExt
    {
        //public static AssetBundle ab;
        public static AudioSource[] m_a_source = new AudioSource[512];
        public static AudioLowPassFilter[] m_a_filter = new AudioLowPassFilter[512];
    }

    // not ready yet

    /*
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
                    yield return code;
                }
                else
                {
                    yield return code;
                }
            }
        }
    }*/
}