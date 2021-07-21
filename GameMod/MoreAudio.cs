using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace mod_moreaudio
{
    static class MoreAudio
    {
        public static IEnumerable<CodeInstruction> ChangeMaxSources(IEnumerable<CodeInstruction> cs) {
            //int n = 0;
            foreach (var c in cs)
            {
                if (c.opcode == OpCodes.Ldc_I4 && (int)c.operand == 128)
                {
                    c.operand = 512;
                    //n++;
                }
                yield return c;
            }
            //Debug.Log(n);
        }
    }

    [HarmonyPatch]
    class MoreAudioIterator
    {
        static MethodBase TargetMethod()
        {
            foreach (var x in typeof(UnityAudio).GetNestedTypes(BindingFlags.NonPublic))
                if (x.Name.Contains("LoadSoundEffects"))
                {
                    //UnityEngine.Debug.Log("Found LoadSoundEffects iterator " + x.Name);
                    return x.GetMethod("MoveNext");
                }
            return null;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); }
    }

    [HarmonyPatch(typeof(UnityAudio), "InitializeForNewLevel")]
    class MoreAudio2 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "AdjustSFXPitch")]
    class MoreAudio3 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "UpdateAudio")]
    class MoreAudio4 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "PauseAllSounds")]
    class MoreAudio5 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "RestartSoundsFromLoadGame")]
    class MoreAudio6 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "Serialize")]
    class MoreAudio7 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "Deserialize")]
    class MoreAudio8 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), MethodType.Constructor)]
    class MoreAudio9 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }

    [HarmonyPatch(typeof(UnityAudio), "FindNextOpenAudioSlot")]
    class MoreAudio10 { static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) { return MoreAudio.ChangeMaxSources(cs); } }
}
