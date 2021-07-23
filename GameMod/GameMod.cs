using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.VersionHandling;
using Harmony;
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
            VREnabled = FindArg("-vrmode");

            GameVersion = typeof(GameManager).Assembly.GetName().Version;
            Debug.Log("Initializing " + OlmodVersion.FullVersionString + ", game " + GameVersion);
            Debug.Log("Command line " + String.Join(" ", Environment.GetCommandLineArgs()));
            Config.Init();
            MPInternet.CheckInternetServer();
            HarmonyInstance.DEBUG = FindArg("-harmonydebug");
            var harmony = HarmonyInstance.Create("olmod.olmod");
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

    // add monsterball mb_arena1 level to multiplayer levels
    [HarmonyPatch(typeof(GameManager), "ScanForLevels")]
    class MBLevelPatch
    {
        public static bool SLInit = false;
        static void Prefix()
        {
            if (SLInit)
                return;
            SLInit = true;
            /*
            Overload.GameManager.MultiplayerMission.AddLevel("mb_arena1", "ARENA", "TITAN_06", new int[]
                        {
                                1,
                                4,
                                2,
                                8
                        });
            */
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

    // GSync fix
    [HarmonyPatch(typeof(GameManager), "UpdateTargetFramerate")]
    class GSyncFix
    {
        static bool Prefix()
        {
            if (GameplayManager.IsDedicatedServer())
            {
                Application.targetFrameRate = 120;
            }
            else
            {
                Application.targetFrameRate = -1;
            }
            return false;
        }
    }

    // Shenanigans.
    [HarmonyPatch(typeof(StringParse), "IsNiceWord")]
    class ReallyIsNiceWord
    {
        static bool Prefix(string s, ref bool __result)
        {
            if ((new string[] { "shenanigans", "methinks" }).Contains(s.ToLower()))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    // Fix next/previous resolution buttons.
    [HarmonyPatch(typeof(MenuManager), "SelectNextResolution")]
    class FixSelectNextResolution
    {
        static bool Prefix()
        {
            var resolutions = Screen.resolutions.Where(r => r.width >= 800 && r.height >= 540).Select(r => new Resolution { width = r.width, height = r.height }).Distinct().ToList();

            resolutions.Sort((a, b) =>
            {
                return a.width == b.width ? a.height - b.height : a.width - b.width;
            });

            var index = resolutions.IndexOf(new Resolution { width = MenuManager.m_resolution_width, height = MenuManager.m_resolution_height });

            if (index == -1)
            {
                index = resolutions.Count() - 1;
            }
            else if (UIManager.m_select_dir > 0)
            {
                index++;
                if (index >= resolutions.Count())
                {
                    index = 0;
                }
            }
            else
            {
                index--;
                if (index < 0)
                {
                    index = resolutions.Count() - 1;
                }
            }

            MenuManager.m_resolution_width = resolutions[index].width;
            MenuManager.m_resolution_height = resolutions[index].height;

            return false;
        }
    }

    // Fix ptaching GameManager.Awake
    // When "GameManager.Awake" gets patched, two things happen:
    // 1. GameManager.Version gets set to 0.0.0.0 becuae it internally uses
    //    GetExecutingAssembly() to query the version, but the patched version
    //     lives in another assembly
    // 2. The "anitcheat" injection detector is triggered. But that thing
    //    doesn't do anything useful anyway, so disable it
    [HarmonyPatch(typeof(GameManager), "Awake")]
    class FixGameManagerAwakePatching
    {

        // return the Assembly which contains Overload.GameManager
        public static Assembly GetOverloadAssembly()
        {
            return Assembly.GetAssembly(typeof(Overload.GameManager));
        }

        // transpiler
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            // This patches the next call of Server.IsDedicatedServer() call after
            // a StartCoroutine was called to just pushing true onto the stack instead.
            // We play safe here becuase other patches might add IsDedicatedServer() calls
            // to that method, so we search specifically for the first one after
            // StartCoroutine was called.
            int state = 0;

            foreach (var code in codes) {
                // patch GetExecutingAssembly to use GetOverloadAssembly instead
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "GetExecutingAssembly") {
                    var method = AccessTools.Method(typeof(FixGameManagerAwakePatching), "GetOverloadAssembly");
                    yield return new CodeInstruction(OpCodes.Call,method);
                    continue;
                }
                if (state == 0 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "StartCoroutine") {
                    state =1;
                } else if (state == 1 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsDedicatedServer") {
                  // this is the first IsDedicatedServer call after StartCoroutine
                  yield return new CodeInstruction(OpCodes.Ldc_I4_1); // push true on the stack instead
                  state = 2; // do not patch other invocations of StartCoroutine
                  continue;
                }

                yield return code;
            }
        }
    }
}
