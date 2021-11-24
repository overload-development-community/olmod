using HarmonyLib;
using UnityEngine;

namespace GameMod
{
    // Fix reloading custom ambient sounds by unloading them after the level ends
    [HarmonyPatch(typeof(UnityAudio), "KillAllSounds")]
    class AmbientUnload
    {
        static void Postfix(UnityAudio __instance) {
            foreach (AudioSource src in __instance.m_ambient_sources)
                src.clip.UnloadAudioData();
        }
    }
}
