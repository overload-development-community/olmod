using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches.Overload {
    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    public class PlayerShip_Awake {
        public static void Postfix(PlayerShip __instance) {
            __instance.c_camera_transform.localScale = Vector3.one * VRScale.VR_Scale;
        }
    }
}
