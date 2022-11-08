using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Objects;
using GameMod.VersionHandling;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Core {
    public class GameMod
    {
        private static Version GameVersion;
        public static string ModsLoaded = "";

        public static void Initialize()
        {
            if (GameVersion != null)
            {
                Debug.Log("olmod Initialize called but is already initialized!");
                return;
            }

            Switches.Init();

            GameVersion = typeof(GameManager).Assembly.GetName().Version;
            Debug.Log("Initializing " + OlmodVersion.FullVersionString + ", game " + GameVersion);
            Debug.Log("Command line " + String.Join(" ", Environment.GetCommandLineArgs()));
            Config.Init();
            MPInternet.CheckInternetServer();
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

            if (Switches.Modded && Config.OLModDir != null && Config.OLModDir != "")
            {
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
                            OlmodVersion.Modded = true;
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

            if (Switches.Profiler) {
                PoorMansProfiler.Initialize(harmony);
            }
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
