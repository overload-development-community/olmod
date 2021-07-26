using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    // load faster by doing multiple steps in the same frame
    [HarmonyPatch(typeof(GameManager), "MaybeEnableDeveloperMode")]
    class FastLoadInit
    {
        private static bool Prepare()
        {
            var enabled = Core.GameMod.FindArg("-fastload");
            if (enabled)
                Debug.Log("fastload enabled");
            return enabled;
        }
        private static void Prefix(GameManager __instance, IEnumerator<float> ___m_initialization_enumerator, ref float ___m_initialization_progress)
        {
            var yieldTime = Time.realtimeSinceStartup + 0.1f;
            while (!GameManager.m_initialized && Time.realtimeSinceStartup < yieldTime)
            {
                ___m_initialization_progress = ___m_initialization_enumerator.Current;
                GameManager.m_initialized = !___m_initialization_enumerator.MoveNext();
            }
        }
    }

    [HarmonyPatch(typeof(UnityAudio), "LoadSoundEffects")]
    class FastLoadNoSound
    {
        private static bool Prepare()
        {
            var enabled = Core.GameMod.FindArg("-nosound");
            if (enabled)
                Debug.Log("nosound enabled");
            return enabled;
        }

        private static MethodInfo _UnityAudio_CreateAudioSourceAndObject_Method = typeof(UnityAudio).GetMethod("CreateAudioSourceAndObject", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool Prefix(ref IEnumerable<float> __result, AudioClip[] ___m_sound_effects, float[] ___m_sound_effects_volume,
            float[] ___m_sound_effects_pitch_amt, float[] ___m_sound_effects_cooldown, UnityAudio __instance)
        {
            for (int i = 1; i < 486; i++)
            {
                ___m_sound_effects[i] = AudioClip.Create(i.ToString(), 1, 1, 44100, false);
                ___m_sound_effects_volume[i] = 1f;
            }
            __instance.InitSoundFX();
            var args = new object[] { 0, "" };
            for (int j = 0; j < 512; j++)
            {
                args[0] = j;
                _UnityAudio_CreateAudioSourceAndObject_Method.Invoke(__instance, args);
            }
            __instance.SFXVolume = MenuManager.opt_volume_sfx;
            __result = new float[0];
            return false;
        }
    }

    [HarmonyPatch(typeof(RobotManager), "Initialize")]
    class FastLoadNoRobot
    {
        private static bool Prepare()
        {
            var enabled = Core.GameMod.FindArg("-norobot");
            if (enabled)
                Debug.Log("norobot enabled");
            return enabled;
        }
        private static bool Prefix(ref IEnumerable<float> __result, ref GameObject[] ___m_enemy_prefab)
        {
            ___m_enemy_prefab = new GameObject[26];
            for (int i = 0; i < 26; i++)
            {
                var go = ___m_enemy_prefab[i] = new GameObject();
                go.AddComponent<Rigidbody>();
                Robot r = go.AddComponent<Robot>();
                r.fire_pos = new Transform[0];
                r.m_left_blade_transform = r.m_right_blade_transform = r.transform;
            }
            __result = new float[0];
            return false;
        }
    }

    [HarmonyPatch(typeof(GameManager), "SetupDedicatedServer")]
    class FastLoadTexResLow
    {
        private static bool Prepare()
        {
            var enabled = Core.GameMod.FindArg("-texreslow");
            if (enabled)
                Debug.Log("texreslow enabled");
            return enabled;
        }
        private static void Prefix()
        {
            QualitySettings.masterTextureLimit = 2 - 0;
        }
    }
}
