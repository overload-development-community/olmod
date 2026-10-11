using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class ControlBar
    {
        public static bool Shown = true;

        public static bool DraggingSlider;

        private const float SLIDER_X_MIN = -480f;
        private const float SLIDER_X_MAX = 480f;
        private const float SLIDER_Y_FROM_BOTTOM = 92f;
        private const float BUTTON_ROW_Y_FROM_BOTTOM = 46f;
        private const float TRACK_HALF_HEIGHT = 3f;
        private const float TIME_LABEL_GAP = 16f;

        private const float PANEL_MID_Y_FROM_BOTTOM = 73f;
        private const float PANEL_HALF_WIDTH = 562f;
        private const float PANEL_HALF_HEIGHT = 62f;

        private const float SKIP_BACK_X = -466f;
        private const float PLAY_PAUSE_X = -330f;
        private const float SKIP_FORWARD_X = -194f;
        private const float SPEED_DOWN_X = -90f;
        private const float SPEED_READOUT_X = 0f;
        private const float SPEED_UP_X = 90f;
        private const float PREVIOUS_POV_X = 200f;
        private const float NEXT_POV_X = 346f;
        private const float CAMERA_MODE_X = 476f;

        private const float SKIP_HALF_WIDTH = 44f;
        private const float PLAY_PAUSE_HALF_WIDTH = 46f;
        private const float SPEED_STEP_HALF_WIDTH = 14f;
        private const float SPEED_READOUT_HALF_WIDTH = 30f;
        private const float POV_HALF_WIDTH = 50f;
        private const float CAMERA_MODE_HALF_WIDTH = 34f;

        private const float SMALL_ITEM_HALF_WIDTH = 250f;
        private const float BUTTON_TEXT_SIZE = 0.42f;

        private const float DRAG_ROUNDING_SECONDS = 0.05f;

        private static float pendingSeekSeconds = -1f;
        private static float appliedSeekSeconds = -1f;
        private static float lastSeekRealTime;
        private static float fineDragSeconds;
        private static float lastDragMouseX;

        private enum BarButton
        {
            None, SkipBack, PlayPause, SkipForward, SpeedDown, SpeedUp,
            PreviousPov, NextPov, CameraMode
        }

        private static BarButton hovered;

        public static void Reset()
        {
            Shown = true;
            DraggingSlider = false;
            pendingSeekSeconds = appliedSeekSeconds = -1f;
            hovered = BarButton.None;
        }

        public static void SetShown(bool on)
        {
            Shown = on;
            DraggingSlider = false;

            pendingSeekSeconds = -1f;
            if (on)
            {
                FreeCursor();
                GameplayManager.AddHUDMessage("Replay: controls shown - N to hide and fly");
            }
            else if (Viewpoint.IsFree)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                GameplayManager.AddHUDMessage("Replay: flying - N for controls");
            }
            else
            {
                FreeCursor();
                GameplayManager.AddHUDMessage("Replay: controls hidden - N to show");
            }
        }

        public static void FreeCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void ReadMouseInput()
        {
            FreeCursor();

            if (uConsole.IsOn())
            {
                hovered = BarButton.None;
                DraggingSlider = false;
                return;
            }
            UIManager.FindMousePosition();

            Vector2 mouse = UIManager.m_mouse_pos;
            float sliderY = UIManager.UI_BOTTOM - SLIDER_Y_FROM_BOTTOM;
            float buttonRowY = UIManager.UI_BOTTOM - BUTTON_ROW_Y_FROM_BOTTOM;
            bool clicked = Input.GetMouseButtonDown(0);
            bool mouseHeld = Input.GetMouseButton(0);
            if (!mouseHeld)
                DraggingSlider = false;

            bool overTrack = mouse.x >= SLIDER_X_MIN - 12f && mouse.x <= SLIDER_X_MAX + 12f &&
                             Mathf.Abs(mouse.y - sliderY) <= 18f;
            if (clicked && overTrack)
            {
                DraggingSlider = true;

                appliedSeekSeconds = -1f;
                fineDragSeconds = Playback.Clock;
                lastDragMouseX = mouse.x;
            }
            float duration = Playback.Duration;
            if (DraggingSlider && mouseHeld && duration > 0.01f)
            {
                float rawTargetSeconds;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    fineDragSeconds = Mathf.Clamp(fineDragSeconds + (mouse.x - lastDragMouseX)
                        * (duration / (SLIDER_X_MAX - SLIDER_X_MIN)) * 0.05f, 0f, duration); // Shift: 20x finer
                    rawTargetSeconds = fineDragSeconds;
                }
                else
                {
                    float fraction = Mathf.Clamp01((mouse.x - SLIDER_X_MIN) / (SLIDER_X_MAX - SLIDER_X_MIN));
                    rawTargetSeconds = fraction * duration;
                    fineDragSeconds = rawTargetSeconds;
                }
                lastDragMouseX = mouse.x;
                float target = Mathf.Round(rawTargetSeconds / DRAG_ROUNDING_SECONDS) * DRAG_ROUNDING_SECONDS;
                if (target != appliedSeekSeconds)
                    pendingSeekSeconds = target;
            }

            if (pendingSeekSeconds >= 0f &&
                (!DraggingSlider || Time.unscaledTime - lastSeekRealTime >= 1f / 60f)) // throttle live-drag seeks
            {
                Playback.SeekTo(pendingSeekSeconds);
                appliedSeekSeconds = pendingSeekSeconds;
                lastSeekRealTime = Time.unscaledTime;
                pendingSeekSeconds = -1f;
            }

            hovered = BarButton.None;
            if (Mathf.Abs(mouse.y - buttonRowY) > 22f) // the button row's vertical hit extent
                return;

            if (MouseOver(mouse.x, SKIP_BACK_X, SKIP_HALF_WIDTH))
            {
                hovered = BarButton.SkipBack;
                if (clicked)
                    Playback.SeekTo(Playback.Clock - Playback.SEEK_STEP_SECONDS);
            }
            else if (MouseOver(mouse.x, PLAY_PAUSE_X, PLAY_PAUSE_HALF_WIDTH))
            {
                hovered = BarButton.PlayPause;
                if (clicked)
                    Playback.TogglePause();
            }
            else if (MouseOver(mouse.x, SKIP_FORWARD_X, SKIP_HALF_WIDTH))
            {
                hovered = BarButton.SkipForward;
                if (clicked)
                    Playback.SeekTo(Playback.Clock + Playback.SEEK_STEP_SECONDS);
            }
            else if (MouseOver(mouse.x, SPEED_DOWN_X, SPEED_STEP_HALF_WIDTH))
            {
                hovered = BarButton.SpeedDown;
                if (clicked)
                    Playback.StepSpeed(-1);
            }
            else if (MouseOver(mouse.x, SPEED_UP_X, SPEED_STEP_HALF_WIDTH))
            {
                hovered = BarButton.SpeedUp;
                if (clicked)
                    Playback.StepSpeed(1);
            }
            else if (MouseOver(mouse.x, PREVIOUS_POV_X, POV_HALF_WIDTH))
            {
                hovered = BarButton.PreviousPov;
                if (clicked)
                    Viewpoint.CycleWatched(-1);
            }
            else if (MouseOver(mouse.x, NEXT_POV_X, POV_HALF_WIDTH))
            {
                hovered = BarButton.NextPov;
                if (clicked)
                    Viewpoint.CycleWatched(1);
            }
            else if (MouseOver(mouse.x, CAMERA_MODE_X, CAMERA_MODE_HALF_WIDTH))
            {
                hovered = BarButton.CameraMode;
                if (clicked)
                    Viewpoint.CycleMode();
            }
        }

        private static bool MouseOver(float mouseX, float centerX, float halfWidth)
        {
            return Mathf.Abs(mouseX - centerX) <= halfWidth;
        }

        public static void Draw(UIElement ui)
        {
            var replay = Playback.Replay;
            if (replay == null)
                return;

            string povLabel = (Playback.Paused ? "[PAUSED]  " : "") +
                              (Viewpoint.IsFree || Viewpoint.Watching < 0
                                  ? "FREE CAMERA"
                                  : Playback.PlayerName(Viewpoint.Watching));
            if (!Shown)
            {
                ui.DrawStringSmall("N: replay controls",
                    new Vector2(0f, UIManager.UI_BOTTOM - 18f), 0.35f, StringOffset.CENTER,
                    UIManager.m_col_ui1, 0.6f, -1f);
                ui.DrawStringSmall(povLabel, new Vector2(0f, ShipHud.BannerY - 28f), 0.45f,
                    StringOffset.CENTER, UIManager.m_col_ui5, 1f, -1f);
                return;
            }

            Color dim = UIManager.m_col_ui1;
            Color hot = UIManager.m_col_ui4;
            float sliderY = UIManager.UI_BOTTOM - SLIDER_Y_FROM_BOTTOM;
            float buttonRowY = UIManager.UI_BOTTOM - BUTTON_ROW_Y_FROM_BOTTOM;
            float clock = Mathf.Max(0f, Playback.Clock);
            float duration = Mathf.Max(0.01f, replay.Duration);
            float handleX = Mathf.Lerp(SLIDER_X_MIN, SLIDER_X_MAX, Mathf.Clamp01(clock / duration));

            UIManager.DrawQuadUIInner(new Vector2(0f, UIManager.UI_BOTTOM - PANEL_MID_Y_FROM_BOTTOM),
                PANEL_HALF_WIDTH, PANEL_HALF_HEIGHT, Color.black, 0.85f, Hud.TEXTURE_SOLID, Hud.SOLID_INNER);

            UIManager.DrawQuadBarHorizontal(new Vector2(0f, sliderY), TRACK_HALF_HEIGHT, TRACK_HALF_HEIGHT,
                SLIDER_X_MAX - SLIDER_X_MIN, dim, 11);
            if (handleX > SLIDER_X_MIN + 0.5f)
                UIManager.DrawQuadUIInner(new Vector2((SLIDER_X_MIN + handleX) * 0.5f, sliderY),
                    (handleX - SLIDER_X_MIN) * 0.5f, TRACK_HALF_HEIGHT, hot, 0.9f,
                    Hud.TEXTURE_SOLID, Hud.SOLID_INNER);
            UIManager.DrawQuadUI(new Vector2(handleX, sliderY), 3f, 11f, hot, 1f, 11); // the slider handle

            ui.DrawStringSmall(
                DraggingSlider ? Session.FormatTimeTenths(clock) : Session.FormatTime(clock),
                new Vector2(SLIDER_X_MIN - TIME_LABEL_GAP, sliderY),
                0.4f, StringOffset.RIGHT, dim, 1f, -1f);
            ui.DrawStringSmall(Session.FormatTime(duration),
                new Vector2(SLIDER_X_MAX + TIME_LABEL_GAP, sliderY),
                0.4f, StringOffset.LEFT, dim, 1f, -1f);

            ui.DrawSmallItem("- 5s", new Vector2(SKIP_BACK_X, buttonRowY),
                hovered == BarButton.SkipBack, false, SKIP_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem(Playback.Paused ? "PLAY" : "PAUSE", new Vector2(PLAY_PAUSE_X, buttonRowY),
                hovered == BarButton.PlayPause, false, PLAY_PAUSE_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem("+ 5s", new Vector2(SKIP_FORWARD_X, buttonRowY),
                hovered == BarButton.SkipForward, false, SKIP_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem("-", new Vector2(SPEED_DOWN_X, buttonRowY),
                hovered == BarButton.SpeedDown, false, SPEED_STEP_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem("", new Vector2(SPEED_READOUT_X, buttonRowY),
                false, false, SPEED_READOUT_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem("+", new Vector2(SPEED_UP_X, buttonRowY),
                hovered == BarButton.SpeedUp, false, SPEED_STEP_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem("PREV POV", new Vector2(PREVIOUS_POV_X, buttonRowY),
                hovered == BarButton.PreviousPov, false, POV_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem("NEXT POV", new Vector2(NEXT_POV_X, buttonRowY),
                hovered == BarButton.NextPov, false, POV_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);
            ui.DrawSmallItem(Viewpoint.ModeLabel, new Vector2(CAMERA_MODE_X, buttonRowY),
                hovered == BarButton.CameraMode, false, CAMERA_MODE_HALF_WIDTH / SMALL_ITEM_HALF_WIDTH, BUTTON_TEXT_SIZE);

            ui.DrawStringSmall(Playback.Speed.ToString("0.##") + "x",
                new Vector2(SPEED_READOUT_X, buttonRowY), BUTTON_TEXT_SIZE,
                StringOffset.CENTER, UIManager.m_col_ui2, 1f, SPEED_READOUT_HALF_WIDTH * 2f);

            float panelTopY = UIManager.UI_BOTTOM - PANEL_MID_Y_FROM_BOTTOM - PANEL_HALF_HEIGHT;
            ui.DrawStringSmall(povLabel, new Vector2(0f, panelTopY + 16f), 0.45f,
                StringOffset.CENTER, UIManager.m_col_ui5, 1f, -1f);
        }
    }
}
