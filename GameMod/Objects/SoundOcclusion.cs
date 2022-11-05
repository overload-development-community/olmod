using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    [Mod(Mods.SoundOcclusion)]
    public static class SoundOcclusion {
        //                                          N/A LOW    MED    STRONG
        public static readonly float[] MAXDISTS = { 0f, 110f,  100f,  95f };    // xtra strong 85f
        public static readonly float[] BOOSTS =   { 0f, 0.10f, 0.15f, 0.20f };  // xtra strong 0.25f
        public static readonly float[] LOWFREQS = { 0f, 950f,  800f,  500f };   // xtra strong 500f
        public static readonly float[] CUTOFFS =  { 0f, 9000f, 9500f, 10000f }; // xtra strong 10500f
        // actual cutoff starting point is currently targetted at ~7khz since we are clamping to 15 units minimum distance below

        // Change this at your peril, gotta recalculate the curves if you do
        public const float MINDIST = 15f;

        public static float CutoffFreq;
        public static float BoostAmount;

        public static int CueState = 0;
        public static bool Occluded;

        public static GameObject[] m_a_object = new GameObject[512];
        public static AudioSource[] m_a_source = new AudioSource[512];
        public static AudioLowPassFilter[] m_a_filter = new AudioLowPassFilter[512];

        // an attempt to optimize the "disabled" setting for occlusion - AddFilters re-adds a new set of filters if re-enabled
        public static void AddFilters() {
            if (!GameplayManager.IsDedicatedServer()) {
                for (int i = 0; i < 512; i++) {
                    AddFilter(i);
                }
            }
        }

        public static void AddFilter(int i) {
            if (m_a_object[i].GetComponent<AudioLowPassFilter>() == null) {
                m_a_filter[i] = m_a_object[i].AddComponent<AudioLowPassFilter>();
            } else {
                m_a_filter[i] = m_a_object[i].GetComponent<AudioLowPassFilter>();
            }
            m_a_filter[i].cutoffFrequency = 22000f;
            m_a_filter[i].enabled = false;
        }

        // ... while RemoveFilters takes them out completely
        public static void RemoveFilters() {
            for (int i = 0; i < 512; i++) {
                if (m_a_object[i] != null && m_a_object[i].GetComponent<AudioLowPassFilter>() != null) {
                    Object.Destroy(m_a_filter[i]);
                }
            }
        }
    }
}
