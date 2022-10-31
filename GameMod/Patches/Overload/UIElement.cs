using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Draws the rear view.
    /// </summary>
    [Mod(Mods.RearView)]
    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    public class RearViewHUDPatch {
        public static void Postfix() {
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
}
