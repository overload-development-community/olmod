using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    /// <summary>
    /// Functions to enable the rear view.
    /// </summary>
    [Mod(Mods.RearView)]
    public static class RearView {
        public static bool MenuManagerEnabled;
        public static bool MPMenuManagerEnabled;
        public static bool MPNetworkMatchEnabled;
        public static Camera rearCam;
        public static RenderTexture rearTex;
        private static Quaternion m_rear_view_rotation = Quaternion.Euler(0f, 180f, 0f);

        public static bool Enabled {
            get {
                if (GameplayManager.IsMultiplayer) {
                    return MenuManagerEnabled
                        && MPNetworkMatchEnabled
                        && NetworkMatch.m_match_elapsed_seconds > 0f;
                } else {
                    return MenuManagerEnabled;
                }
            }
        }

        public static void Init() {
            if (rearCam != null)
                return;
            Camera mainCam = Camera.main;
            GameObject rearCamGO = new GameObject("RearCam");
            rearCamGO.transform.parent = mainCam.transform.parent;
            // add distance for backside of cockpit
            rearCamGO.transform.localPosition = mainCam.transform.localPosition + new Vector3(0, 0, -2.1f);
            rearCamGO.transform.localRotation = m_rear_view_rotation;
            rearCamGO.transform.localScale = mainCam.transform.localScale;
            rearCam = rearCamGO.AddComponent<Camera>();
            rearCam.nearClipPlane = 0.01f;

            rearTex = new RenderTexture(256, 256, 24);
            rearCam.targetTexture = rearTex;
        }

        public static void Pause() {
            if (rearCam != null)
                rearCam.enabled = false;
        }

        public static void Toggle() {
            MenuManagerEnabled = !MenuManagerEnabled;
            if (MenuManagerEnabled) {
                if (rearTex == null || rearCam == null || rearCam.gameObject == null)
                    Init();
            } else
                Pause();
        }
    }
}
