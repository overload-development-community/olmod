using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    [Mod(Mods.VRScale)]
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
}
