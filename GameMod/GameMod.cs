using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Schema;
using GameMod.VersionHandling;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Core {
    public class GameMod
    {
        private static Version GameVersion;
        public static bool Modded = false;
        public static bool VREnabled = false;
        public static string ModsLoaded = "";

        public static void Initialize()
        {
            if (GameVersion != null)
            {
                Debug.Log("olmod Initialize called but is already initialized!");
                return;
            }

            Modded = FindArg("-modded");
            VREnabled = FindArgVal("-vrmode", out var vrmode) && vrmode != "none";

            GameVersion = typeof(GameManager).Assembly.GetName().Version;
            Debug.Log("Initializing " + OlmodVersion.FullVersionString + ", game " + GameVersion);
            Debug.Log("Command line " + String.Join(" ", Environment.GetCommandLineArgs()));
            Config.Init();
            MPInternet.CheckInternetServer();
            Harmony.DEBUG = FindArg("-harmonydebug");
            var harmony = new Harmony("olmod.olmod");
            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
            Debug.Log("Done initializing " + OlmodVersion.FullVersionString);

            if (Modded && Config.OLModDir != null && Config.OLModDir != "")
            {
                Modded = false; // Modded mode was on, we turn it off here because we don't want to have it on if there aren't actually any mods.
                try
                {
                    var files = Directory.GetFiles(Config.OLModDir, "Mod-*.dll");
                    ModsLoaded = string.Join(",", files);

                    foreach (var f in files)
                    {
                        Debug.Log("Loading mod " + f);
                        var asm = Assembly.LoadFile(f);
                        try
                        {
                            harmony.PatchAll(asm);
                            Modded = true; // At this point we're sure we're modded, so set to true.
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Running mod " + f + ": " + ex.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
                }
            }

            if (Modded)
            {
                OlmodVersion.Modded = true; // Only display modded tag if you're playing modded.
            }

            if (FindArg("-poor-mans-profiler")) {
                PoorMansProfiler.Initialize(harmony);
            }

            MPSpawnExtensionVis.visualizing = FindArg("-spawnpoint-editor");
            if (MPSpawnExtensionVis.visualizing)
            {
                uConsole.RegisterCommand("export-spawns", "Exports spawnpoints from the editor to a .json file in the OLmod directory", new uConsole.DebugCommand(MPSpawnExtensionVis.Export));
            }

            if (FindArg("-telemetry")) 
                TelemetryMod.telemetry_enabled = true;

            if (FindArgVal("-telemetry-ip", out string ip_string)){
                if (!String.IsNullOrEmpty(ip_string))
                {
                    string[] ip_string_parts = ip_string.Split('.');
                    if (ip_string_parts.Length == 4)
                    {
                        bool valid = true;
                        for (int i = 0; i < 3; i++)
                            if (int.TryParse(ip_string_parts[i], out int value))
                                if (value < 0 | value > 255)
                                    valid = false;

                        if (valid)
                            TelemetryMod.telemetry_ip = ip_string;
                        else
                            Debug.Log("Invalid Input. Telemetry IP contains subvalues outside of the [0,255] range: " + ip_string);
                    }

                }
            }

            if (FindArgVal("-telemetry-port", out string port_string))
            {
                if (int.TryParse(port_string, out int port))
                    if (port >= 0 && port < 65535)
                        TelemetryMod.telemetry_port = port;
                    else
                        Debug.Log("Invalid Input. Telemetry Port is outside of the allowed [0, 65534] range: " + port_string);
                else
                    Debug.Log("Invalid Input. Telemetry Port is not a whole number: " + port_string);
            }
        }

        public static bool FindArg(string arg)
        {
            return Array.IndexOf<string>(Environment.GetCommandLineArgs(), arg) >= 0;
        }

        public static bool FindArgVal(string arg, out string val)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf<string>(args, arg);
            val = null;
            if (i < 0 || i + 1 >= args.Length)
                return false;
            val = args[i + 1];
            return true;
        }

        public static bool HasInternetMatch()
        {
            return GameVersion.CompareTo(new Version(1, 0, 1885)) >= 0;
        }

    }

    [HarmonyPatch(typeof(LocalLANHost), "GetServerLocation")]
    class ServerLocationPatch
    {
        private static bool Prefix(ref string __result)
        {
            __result = OlmodVersion.FullVersionString.ToUpperInvariant();
            return false;
        }
    }

    // Remove annoying Tobii errors.
    [HarmonyPatch(typeof(Debug), "LogError", new Type[] { typeof(object) })]
    class RemoveTobiiErrors
    {
        static bool Prefix(object message)
        {
            return !(message is string msg && msg.StartsWith("Could not find any window with process id"));
        }
    }

    // Remove 10 FPS floor for the game time in multiplayer matches.
    [HarmonyPatch(typeof(GameManager), "Update")]
    class MPRemove10FPSFloor
    {
        static float MaybeMin(float a, float b)
        {
            if (GameplayManager.IsMultiplayer)
            {
                return b;
            }

            return Mathf.Min(a, b);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var mpRemove10FPSFloor_MaybeMin_Method = AccessTools.Method(typeof(MPRemove10FPSFloor), "MaybeMin");

            var found = false;
            foreach (var code in instructions)
            {
                if (!found)
                {
                    if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "Min")
                    {
                        code.operand = mpRemove10FPSFloor_MaybeMin_Method;
                        found = true;
                    }
                }
                yield return code;
            }
        }
    }

    // Shenanigans.
    [HarmonyPatch(typeof(StringParse), "IsNiceWord")]
    class ReallyIsNiceWord
    {
        static bool Prefix(string s, ref bool __result)
        {
            if ((new string[] { "shenanigans", "methinks", "goodnight" }).Contains(s.ToLower()))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
}
