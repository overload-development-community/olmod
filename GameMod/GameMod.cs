using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod.Core
{
    public class GameMod
    {
        public static readonly string Version = "olmod 0.3.4";
        private static Version GameVersion;

        public static void Initialize()
        {
            if (GameVersion != null)
            {
                Debug.Log("olmod Initialize called but is already initialized!");
                return;
            }
            GameVersion = typeof(Overload.GameManager).Assembly.GetName().Version;
            Debug.Log("Initializing " + Version + ", game " + GameVersion);
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
            Debug.Log("Done initializing " + Version);

            if (Config.OLModDir != null && Config.OLModDir != "")
            {
                try
                {
                    foreach (var f in Directory.GetFiles(Config.OLModDir, "Mod-*.dll"))
                    {
                        Debug.Log("Loading mod " + f);
                        var asm = Assembly.LoadFile(f);
                        try
                        {
                            harmony.PatchAll(asm);
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

        // enable monsterball mode, allow max players up to 16
        [HarmonyPatch(typeof(Overload.MenuManager), "MpMatchSetup")]
        class MBModeSelPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int n = 0;
                var codes = new List<CodeInstruction>(instructions);
                for (var i = 0; i < codes.Count; i++)
                {
                    // increase max mode to allow monsterball mode
                    if (codes[i].opcode == OpCodes.Ldsfld && (codes[i].operand as FieldInfo).Name == "mms_mode")
                    {
                        i++;
                        if (codes[i].opcode == OpCodes.Ldc_I4_2)
                            codes[i].opcode = OpCodes.Ldc_I4_4;
                        i++;
                        while (codes[i].opcode == OpCodes.Add || codes[i].opcode == OpCodes.Ldsfld)
                            i++;
                        if (codes[i].opcode == OpCodes.Ldc_I4_2)
                            codes[i].opcode = OpCodes.Ldc_I4_4;
                        n++;
                    }
                    if (codes[i].opcode == OpCodes.Ldsfld && (codes[i].operand as FieldInfo).Name == "mms_max_players" &&
                        i > 0 && codes[i - 1].opcode == OpCodes.Br) // take !online branch
                    {
                        while (codes[i].opcode == OpCodes.Add || codes[i].opcode == OpCodes.Ldsfld)
                            i++;
                        if (codes[i].opcode == OpCodes.Ldc_I4_1 && codes[i + 1].opcode == OpCodes.Ldc_I4_8)
                        {
                            codes[i + 1].opcode = OpCodes.Ldc_I4;
                            codes[i + 1].operand = 16;
                        }
                        n++;
                    }
                }
                Debug.Log("Patched MpMatchSetup n=" + n);
                return codes;
            }
        }

        public static bool HasInternetMatch()
        {
            return GameVersion.CompareTo(new Version(1, 0, 1885)) >= 0;
        }

        // add modified indicator to main menu
        [HarmonyPatch(typeof(Overload.UIElement), "DrawMainMenu")]
        class VersionPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (var i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand as string == "VERSION {0}.{1} BUILD {2}")
                    {
                        codes[i].operand = "VERSION {0}.{1} BUILD {2} " + Version.ToUpperInvariant();
                    }
                }
                return codes;
            }

            static void Postfix(UIElement __instance)
            {
                Vector2 pos = new Vector2(UIManager.UI_RIGHT - 10f, -155f - 60f + 50f + 40f);
                __instance.DrawStringSmall("UNOFFICIAL MODIFIED VERSION", pos,
                    0.35f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);
            }
        }

    }

    // add monsterball mb_arena1 level to multiplayer levels
    [HarmonyPatch(typeof(Overload.GameManager), "ScanForLevels")]
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
            __result = GameMod.Version.ToUpperInvariant();
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
            var found = false;
            foreach (var code in instructions)
            {
                if (!found)
                {
                    if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "Min")
                    {
                        code.operand = AccessTools.Method(typeof(MPRemove10FPSFloor), "MaybeMin");
                        found = true;
                    }
                }
                yield return code;
            }
        }
    }

    // GSync fix
    [HarmonyPatch(typeof(GameManager), "UpdateTargetFramerate")]
    class GSyncFix {
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

    [HarmonyPatch(typeof(Player), "RestorePlayerShipDataAfterRespawn")]
    class CycloneFlakTBAfterDeathFix
    {
        static void Prefix(Player __instance)
        {
            __instance.c_player_ship.GetType().GetField("flak_fire_count", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance.c_player_ship, 0);
            __instance.c_player_ship.m_thunder_power = 0;
        }
    }

    [HarmonyPatch(typeof(StringParse), "IsNiceWord")]
    class ReallyIsNiceWord
    {
        static bool Prefix(string s, ref bool __result)
        {
            if ((new string[] { "shenanigans" }).Contains(s.ToLower()))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

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
}
