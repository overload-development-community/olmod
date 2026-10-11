using System.Collections.Generic;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson,
        Free,
    }

    public static class Viewpoint
    {
        public static int Watching = -1;
        public static CameraMode Mode = CameraMode.Free;

        public static bool IsFree { get { return Mode == CameraMode.Free; } }

        public static string ModeLabel
        {
            get
            {
                switch (Mode)
                {
                    case CameraMode.FirstPerson: return "1ST";
                    case CameraMode.ThirdPerson: return "3RD";
                    default: return "FREE";
                }
            }
        }

        public static void Reset()
        {
            Watching = -1;
            Mode = CameraMode.Free;
            hiddenHullRenderers = null;
        }

        public static void Watch(int rosterIndex)
        {
            var replay = Playback.Replay;
            if (replay == null)
                return;
            var previousMode = Mode;
            bool isWatchableSlot = rosterIndex >= 0 && rosterIndex < replay.players.Count;
            Watching = isWatchableSlot ? rosterIndex : -1;

            if (!isWatchableSlot)
                Mode = CameraMode.Free;
            else if (Mode == CameraMode.Free)
                Mode = CameraMode.FirstPerson;
            PullBackWhenLeavingFirstPerson(previousMode);
            Announce();
        }

        public static void CycleWatched(int step)
        {
            var replay = Playback.Replay;
            if (replay == null)
                return;
            int next = Watching + step;
            if (next >= replay.players.Count)
                next = -1;
            else if (next < -1)
                next = replay.players.Count - 1;
            Watch(next);
        }

        public static void CycleMode()
        {
            if (Playback.Replay == null)
                return;
            var previousMode = Mode;
            switch (Mode)
            {
                case CameraMode.FirstPerson:
                    Mode = CameraMode.ThirdPerson;
                    break;
                case CameraMode.ThirdPerson:
                    Mode = CameraMode.Free;
                    break;
                default:
                    if (Watching < 0)
                        Watching = FirstWatchablePlayer();
                    if (Watching < 0)
                    {
                        GameplayManager.AddHUDMessage("Replay: nobody to follow");
                        return;
                    }
                    Mode = CameraMode.FirstPerson;
                    break;
            }
            PullBackWhenLeavingFirstPerson(previousMode);
            Announce();
        }

        public static int FirstWatchablePlayer()
        {
            var replay = Playback.Replay;
            if (replay == null || replay.players.Count == 0)
                return -1;
            int recorder = replay.recorderSlot >= 0 && replay.recorderSlot < replay.players.Count
                ? replay.recorderSlot : -1;
            if (recorder >= 0 && IsOnScreen(recorder))
                return recorder;
            for (int slot = 0; slot < replay.players.Count; slot++)
                if (IsOnScreen(slot))
                    return slot;

            return recorder >= 0 ? recorder : 0;
        }

        private static bool IsOnScreen(int rosterIndex)
        {
            var state = Ships.StateOf(rosterIndex);
            return state.present && state.alive;
        }

        private static void Announce()
        {
            if (Mode == CameraMode.Free)
                GameplayManager.AddHUDMessage("Camera: free");
            else
                GameplayManager.AddHUDMessage("Camera: " +
                    (Mode == CameraMode.FirstPerson ? "first person - " : "third person - ") +
                    Playback.PlayerName(Watching));
        }

        private static void PullBackWhenLeavingFirstPerson(CameraMode previousMode)
        {
            if (Mode != CameraMode.Free || previousMode != CameraMode.FirstPerson)
                return;
            var cameraTransform = CameraTransform;
            if (cameraTransform != null)
                cameraTransform.position = cameraTransform.position - cameraTransform.forward * 8f
                                                  + cameraTransform.up * 2f;
        }

        private static Transform CameraTransform
        {
            get
            {
                return GameManager.m_player_ship != null ? GameManager.m_player_ship.c_transform : null;
            }
        }

        public static void Drive()
        {
            KeepShipInert();
            if (Mode == CameraMode.Free)
                return;
            var cameraTransform = CameraTransform;
            var watchedShip = Ships.TransformOf(Watching);
            if (cameraTransform == null || watchedShip == null)
                return;

            if (Mode == CameraMode.FirstPerson)
            {
                cameraTransform.position = watchedShip.position;
                cameraTransform.rotation = watchedShip.rotation;
                return;
            }

            Vector3 eye = watchedShip.position + watchedShip.rotation * new Vector3(2.2f, 1.6f, -6.5f); // right, up, back
            Vector3 lookAt = watchedShip.position + watchedShip.forward * 12f;
            cameraTransform.position = eye;
            Vector3 forward = lookAt - eye;

            if (forward.sqrMagnitude > 1e-6f)
                cameraTransform.rotation = Quaternion.LookRotation(forward, watchedShip.up);
        }

        private static MeshRenderer[] hiddenHullRenderers;

        public static void HideShip()
        {
            var ship = GameManager.m_player_ship;
            if (ship == null)
                return;
            if (ship.c_trans_ship != null)
            {
                var cameraTransform = ship.c_camera != null ? ship.c_camera.transform : null;
                var hull = new List<MeshRenderer>();
                foreach (var renderer in ship.c_trans_ship.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer == null || renderer.GetComponent<UnityRenderLayer>() != null)
                        continue;
                    if (cameraTransform != null && renderer.transform.IsChildOf(cameraTransform))
                        continue;
                    renderer.enabled = false;
                    hull.Add(renderer);
                }
                hiddenHullRenderers = hull.ToArray();
            }
            foreach (var collider in ship.GetComponentsInChildren<Collider>(true))
                if (collider != null)
                    collider.enabled = false;
        }

        public static void KeepShipInert()
        {
            if (hiddenHullRenderers != null)
                foreach (var renderer in hiddenHullRenderers)
                    if (renderer != null && renderer.enabled)
                        renderer.enabled = false;

            var ship = GameManager.m_player_ship;
            if (ship != null && ship.c_rigidbody != null)
            {
                ship.c_rigidbody.velocity = Vector3.zero;
                ship.c_rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private const float MOVE_SPEED = 20.25f;
        private const float STRAFE_SPEED = 14.175f;
        private const float TURN_RATE = 360f;
        private const float ROLL_RATE = 180f;
        private const float MOUSE_SENSITIVITY = 1.5f;

        public static void FlyFree()
        {
            var cameraTransform = CameraTransform;
            float deltaSeconds = Time.unscaledDeltaTime;
            if (cameraTransform == null || deltaSeconds <= 0f)
                return;

            bool sliding = Controls.IsPressed(CCInput.SLIDE_MODIFIER);

            float slideRight = Controls.AnalogValue(CCInput.SLIDE_LEFT, CCInput.SLIDE_RIGHT, false);
            float slideUp = Controls.AnalogValue(CCInput.SLIDE_DOWN, CCInput.SLIDE_UP, false);
            float moveForward = Controls.AnalogValue(CCInput.MOVE_BACK, CCInput.MOVE_FORE, false);
            if (sliding)
            {
                float modifierSlideUp = JoystickAxis(CCInput.PITCH_DOWN, CCInput.PITCH_UP);
                if (MenuManager.opt_invert_slide_modifier)
                    modifierSlideUp = -modifierSlideUp;
                slideRight = Controls.GreaterMagnitude(
                    JoystickAxis(CCInput.TURN_LEFT, CCInput.TURN_RIGHT), slideRight);
                slideUp = Controls.GreaterMagnitude(modifierSlideUp, slideUp);
            }
            cameraTransform.position += (cameraTransform.right * slideRight * STRAFE_SPEED +
                                cameraTransform.up * slideUp * STRAFE_SPEED +
                                cameraTransform.forward * moveForward * MOVE_SPEED) * deltaSeconds;

            float roll = -Controls.AnalogValue(CCInput.ROLL_LEFT, CCInput.ROLL_RIGHT) *
                         ROLL_RATE * deltaSeconds;
            float pitch = 0f;
            float yaw = 0f;

            if (!sliding)
            {
                pitch += JoystickAxis(CCInput.PITCH_UP, CCInput.PITCH_DOWN) * TURN_RATE * deltaSeconds;
                yaw += JoystickAxis(CCInput.TURN_LEFT, CCInput.TURN_RIGHT) * TURN_RATE * deltaSeconds;
            }

            if (MenuManager.opt_use_mouse > 0)
            {
                float pitchSign = MenuManager.opt_invert_mouse != 0 ? 1f : -1f;
                yaw += Input.GetAxis("Mouse X") * MOUSE_SENSITIVITY;
                pitch += pitchSign * Input.GetAxis("Mouse Y") * MOUSE_SENSITIVITY;
            }

            cameraTransform.Rotate(pitch, yaw, roll, Space.Self);
        }

        private static float JoystickAxis(CCInput negative, CCInput positive)
        {
            float axisValue = 0f;
            for (int slot = 0; slot < 2; slot++)
            {
                if (Controls.m_input_joy[slot, (int)negative].IsValid)
                    axisValue = Controls.GreaterMagnitude(
                        -Controls.m_input_joy[slot, (int)negative].Value(), axisValue);
                if (Controls.m_input_joy[slot, (int)positive].IsValid)
                    axisValue = Controls.GreaterMagnitude(
                        Controls.m_input_joy[slot, (int)positive].Value(), axisValue);
            }
            return axisValue;
        }
    }
}
