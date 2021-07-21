using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GameMod {
    [HarmonyPatch(typeof(ProFlareBatch), "CreateMat")]
    class Shaders_CreateMat {
        public static bool Prefix(ProFlareBatch __instance) {
            if (!Core.GameMod.VREnabled) {
                return true;
            }

            __instance.mat = new Material((Shader)null);
            __instance.meshRender.material = __instance.mat;
            if (__instance._atlas && __instance._atlas.texture) {
                __instance.mat.mainTexture = __instance._atlas.texture;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ProFlareBatch), "Reset")]
    class Shaders_Reset {
        private static MethodInfo _ProFlareBatch_CreateHelperTransform_Method = AccessTools.Method(typeof(ProFlareBatch), "CreateHelperTransform");
        private static MethodInfo _ProFlareBatch_SetupMeshes_Method = AccessTools.Method(typeof(ProFlareBatch), "SetupMeshes");
        public static bool Prefix(ProFlareBatch __instance) {
            if (!Core.GameMod.VREnabled) {
                return true;
            }

            if (__instance.helperTransform == null) {
                _ProFlareBatch_CreateHelperTransform_Method.Invoke(__instance, null);
            }
            __instance.mat = new Material((Shader)null);
            if (__instance.meshFilter == null) {
                __instance.meshFilter = __instance.GetComponent<MeshFilter>();
            }
            if (__instance.meshFilter == null) {
                __instance.meshFilter = __instance.gameObject.AddComponent<MeshFilter>();
            }
            __instance.meshRender = __instance.gameObject.GetComponent<MeshRenderer>();
            if (__instance.meshRender == null) {
                __instance.meshRender = __instance.gameObject.AddComponent<MeshRenderer>();
            }
            if (__instance.FlareCamera == null) {
                __instance.FlareCamera = __instance.transform.root.GetComponentInChildren<Camera>();
            }
            __instance.meshRender.material = __instance.mat;
            _ProFlareBatch_SetupMeshes_Method.Invoke(__instance, null);
            __instance.dirty = true;

            return false;
        }
    }
}
