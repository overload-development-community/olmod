using System;
using HarmonyLib;
using UnityEngine.Profiling;

namespace GameMod.Objects {
    /// <summary>
    /// Loads the command line switches and stores them in memory.
    /// </summary>
    public static class Switches {
        private static string[] Args;

        public static string ChatCommandPassword = null;
        public static string ConfigFile = null;
        public static bool DisableChatCommands = false;
        public static bool FastLoad = false;
        public static string Filter = null;
        public static string Host = null;
        public static bool Internet = false;
        public static string Interval = null;
        public static bool Lazy = false;
        public static string Locking = null;
        public static string MissionPath = null;
        public static bool Modded = false;
        public static bool NoDownload = false;
        public static bool NoRobot = false;
        public static bool NoSound = false;
        public static string OutputPath = null;
        public static string Port = null;
        public static bool Profiler = false;
        public static bool TexResLow = false;
        public static string TrustedPlayerIds = null;
        public static bool VREnabled = false;

        public static void Init() {
            Args = Environment.GetCommandLineArgs();

            Harmony.DEBUG = FindArg("-harmonydebug");

            FindArgVal("-chatCommandPassword", out ChatCommandPassword);
            FindArgVal("-config", out ConfigFile);
            DisableChatCommands = FindArg("-disableChatCommands");
            FastLoad = FindArg("-fastload");
            FindArgVal("-filter", out Filter);
            FindArgVal("-host", out Host);
            Internet = FindArg("-internet");
            FindArgVal("-interval", out Interval);
            FindArgVal("-missionpath", out MissionPath);
            Lazy = FindArg("-lazy");
            FindArgVal("-locking", out Locking);
            Modded = FindArg("-modded");
            NoDownload = FindArg("-nodownload");
            NoRobot = FindArg("-norobot");
            NoSound = FindArg("-nosound");
            FindArgVal("-outputpath", out OutputPath);
            FindArgVal("-port", out Port);
            Profiler = FindArg("-profiler");
            TexResLow = FindArg("-texreslow");
            FindArgVal("-trustedPlayerIds", out TrustedPlayerIds);
            VREnabled = FindArgVal("-vrmode", out var vrmode) && vrmode != "none";
        }

        private static bool FindArg(string arg) {
            return Array.IndexOf(Args, arg) >= 0;
        }

        private static bool FindArgVal(string arg, out string val) {
            val = null;

            int i = Array.IndexOf(Args, arg);
            if (i == -1)
                return false;

            val = Args[i + 1];

            return true;
        }
    }
}
