using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    static class RearView
    {
        public static bool Enabled
        {
            get
            {
                if (GameplayManager.IsMultiplayer)
                {
                    return RearView.MenuManagerEnabled
                        && RearView.MPNetworkMatchEnabled
                        && NetworkMatch.m_match_elapsed_seconds > 0f;
                }
                else
                {
                    return RearView.MenuManagerEnabled;
                }
            }
        }
        public static bool MenuManagerEnabled;
        public static bool MPMenuManagerEnabled;
        public static bool MPNetworkMatchEnabled;
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
        }

        public static void Toggle()
        {
            MenuManagerEnabled = !MenuManagerEnabled;
            if (MenuManagerEnabled)
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
            {
                RearView.Pause();
            }
            else if (new_state == GameplayState.PLAYING)
            {
                if (RearView.Enabled)
                    RearView.Init();
                else
                    RearView.Pause();
            }
        }
    }

    [HarmonyPatch(typeof(Overload.UIElement), "DrawHUD")]
    class RearViewHUDPatch
    {
        static void Postfix()
        {
            if (!GameplayManager.ShowHud || !RearView.Enabled)
                return;

            if (RearView.rearTex == null || RearView.rearCam == null || RearView.rearCam.gameObject == null)
                RearView.Init();

            if (GameManager.m_local_player.m_hitpoints <= 0) {
                RearView.Pause();
                return;
            }

            RearView.rearCam.enabled = true;
            var pos = new Vector2(288f, 288f);
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

    [HarmonyPatch(typeof(MenuManager), "HUDOptionsUpdate")]
    class RearView_MenuManager_HUDOptionsUpdate
    {

        static void HandleRearViewToggle()
        {
            if (UIManager.m_menu_selection == 11)
            {
                RearView.Toggle();
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(MenuManager), "MaybeReverseOption"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RearView_MenuManager_HUDOptionsUpdate), "HandleRearViewToggle"));
                    continue;
                }
                yield return code;
            }
        }
    }

    // Process slider input
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class RearView_MenuManager_MpMatchSetup
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 3 &&
                UIManager.m_menu_selection == 11)
            {
                RearView.MPMenuManagerEnabled = !RearView.MPMenuManagerEnabled;
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }
        }
    }

}
