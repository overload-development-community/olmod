using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public enum SessionMode { Off, Recording, Playback }

    public static class Session
    {
        public static SessionMode Mode = SessionMode.Off;

        public static bool RecordReplays = false;

        public static void SetRecordReplays(bool on)
        {
            RecordReplays = on;
            PilotManager.SavePreferences();
        }

        public static string RosterName(string rosterName, int rosterIndex, string nameFromEvent = null)
        {
            if (!string.IsNullOrEmpty(rosterName))
                return rosterName;
            if (!string.IsNullOrEmpty(nameFromEvent))
                return nameFromEvent;
            return rosterIndex >= 0 ? "PLAYER " + (rosterIndex + 1) : "?";
        }

        public static string ModeName(int matchMode)
        {
            if (matchMode >= 0 && matchMode < (int)ExtMatchMode.NUM)
                return ExtMatchMode.ToString((MatchMode)matchMode);
            return "MODE " + matchMode;
        }

        public static string FormatTime(float seconds)
        {
            int wholeSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            return (wholeSeconds / 60).ToString("0") + ":" + (wholeSeconds % 60).ToString("00");
        }

        public static string FormatTimeTenths(float seconds)
        {
            int tenths = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 10f);
            return (tenths / 600).ToString("0") + ":" + (tenths / 10 % 60).ToString("00") +
                   "." + (tenths % 10).ToString();
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(GameplayManager), "CreateNewGame")]
    class Session_CreateNewGame
    {
        static void Prefix()
        {
            if (Playback.Launching)
                return;

            Recorder.OnNewGame();
            Playback.Stop();
        }
    }
}
