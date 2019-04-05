using Harmony;
using Overload;
using UnityEngine;

namespace GameMod
{
    static class RearView
    {
        public static bool Enabled;
        public static Camera rearCam;
        public static RenderTexture rearTex;
        static Quaternion m_rear_view_rotation = Quaternion.Euler(0f, 180f, 0f);

        public static void Init()
        {
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

        public static void Pause()
        {
            if (rearCam != null)
                rearCam.enabled = false;
        }

        public static void Reset()
        {
            Pause();
            Enabled = false;
        }

        public static void Toggle()
        {
            Enabled = !Enabled;
            if (Enabled)
            {
                GameManager.m_local_player.SetCheaterFlag(true);
                GameplayManager.AddHUDMessage("CHEATER! REAR VIEW ENABLED!");
            }
            else
                GameplayManager.AddHUDMessage("REAR VIEW DISABLED!");
            if (Enabled)
            {
                if (RearView.rearTex == null || RearView.rearCam == null || RearView.rearCam.gameObject == null)
                    RearView.Init();
            }
            else
                Pause();
        }
    }

    [HarmonyPatch(typeof(Overload.GameplayManager), "ChangeGameplayState")]
    class RearViewGPStatePatch
    {
        static void Prefix(GameplayState new_state)
        {
            if (new_state != GameplayState.PLAYING)
                RearView.Pause();
        }
    }

    [HarmonyPatch(typeof(Overload.Player), "ResetAllCheats")]
    class RearViewResetCheatsPatch
    {
        static void Postfix()
        {
            RearView.Reset();
        }
    }

    [HarmonyPatch(typeof(Overload.UIElement), "DrawHUD")]
    class RearViewHUDPatch
    {
        static void Postfix()
        {
            if (!GameplayManager.ShowHud || !RearView.Enabled)
                return;
            RearView.rearCam.enabled = true;
            var pos = new Vector2(289f, 289f);
            var posTile = new Vector2(pos.x, pos.y - 0.01f);
            var size = 100f;
            UIManager.DrawQuadUI(pos, size, size, UIManager.m_col_ui0, 1, 11);
            UIManager.SetTexture(RearView.rearTex);
            UIManager.PauseMainDrawing();
            UIManager.StartDrawing(UIManager.url[1], false, 750f);
            UIManager.DrawTile(posTile, size * 0.93f, size * 0.93f, new Color(0.8f, 0.8f, 0.8f, 1.0f), 1, 0, 0, 1, 1);
            UIManager.ResumeMainDrawing();
        }
    }

    // detect "rearview" cheat code
    [HarmonyPatch(typeof(Overload.PlayerShip))]
    [HarmonyPatch("FrameUpdateReadKeysFromInput")]
    class RearViewReadKeys
    {
        private static string code = "rearview";
        private static int codeIdx = 0;

        static void Prefix()
        {
            if (GameplayManager.IsMultiplayerActive)
                return;
            foreach (char c in Input.inputString)
            {
                if (code[codeIdx] == c)
                    if (++codeIdx < code.Length)
                        continue;
                    else
                        RearView.Toggle();
                codeIdx = 0;
            }
        }
    }
}
