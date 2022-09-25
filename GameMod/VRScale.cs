using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    /// <summary>
    /// This mod is intended to set the camera scale for VR to make spaces feel bigger/smaller in VR.  It's based on https://github.com/Raicuparta/unity-scale-adjuster.
    /// </summary>
    public class VRScale {
        private static float _VR_Scale = 1f;
        public static float VR_Scale {
            get {
                return _VR_Scale;
            }
            set {
                GameManager.m_player_ship.c_camera_transform.localScale = Vector3.one * value;

                _VR_Scale = value;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    public class VRScale_PlayerShip_Update {
        private static void Postfix(PlayerShip __instance) {
            __instance.c_camera_transform.localScale = Vector3.one * VRScale.VR_Scale;
        }
    }
}
