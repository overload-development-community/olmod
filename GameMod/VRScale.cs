using UnityEngine;

namespace GameMod {
    /// <summary>
    /// This mod is intended to set the camera scale for VR to make spaces feel bigger/smaller in VR.  It's based on https://github.com/Raicuparta/unity-scale-adjuster.  However, it is not currently working, it only messes with the cockpit, not the world.
    /// </summary>
    public class VRScale {
        private static float _VR_Scale = 1f;
        public static float VR_Scale {
            get {
                return _VR_Scale;
            }
            set {
                Camera.main.transform.localScale = Vector3.one * value;

                _VR_Scale = value;
            }
        }
    }
}
