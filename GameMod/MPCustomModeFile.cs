using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    class MPCustomModeFile
    {
        public static bool PickupCheck;
    }

    [HarmonyPatch(typeof(RobotManager), "DoReadMultiplayerModeFile")]
    class MPCustomModeFileRead
    {
        public static string ReadCustomFile(LevelInfo level)
        {
            if (level.IsAddOn)
            {
                string filename = "multi_mode_" + level.FileName;
                string filepath = Path.Combine(Path.GetDirectoryName(level.FilePath), filename);
                string ext = null;
                Debug.Log("DoReadMultiplayerModeFile check custom " + filepath);
                byte[] data = Mission.LoadAddonData(level.ZipPath, filepath, ref ext, new [] { ".txt" });
                if (data != null)
                    return Encoding.UTF8.GetString(data);
            }
            return string.Empty;
        }

        private static void Prefix()
        {
            MPCustomModeFile.PickupCheck = false;
            Debug.Log("reset PickupCheck to false");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpCustomModeFileRead_ReadCustomFile_Method = AccessTools.Method(typeof(MPCustomModeFileRead), "ReadCustomFile");

            int state = 0;
            foreach (var code in codes)
            {
                // replace init to empty string with init to custom level string
                if (state == 0 && code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "Empty")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = code.labels };
                    yield return new CodeInstruction(OpCodes.Call, mpCustomModeFileRead_ReadCustomFile_Method);
                    state = 1;
                    continue;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(LevelInfo), "CheckReloadStory")]
    class MPCustomLevelInfoCheck
    {
        private static MethodInfo _LevelInfo_ReadChallengeModeText_Method = typeof(LevelInfo).GetMethod("ReadChallengeModeText", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool Prefix(LevelInfo __instance, ref string ___m_loaded_language)
        {
            /*
            Debug.Log("CheckReloadStory " +
                (Server.IsActive() ? "server" : "client") + " addon " +
                __instance.IsAddOn + " " + __instance.FileName + " " +
                " ld lang " + ___m_loaded_language + " cur lang " + Loc.CurrentLanguageCode +
                " type " + __instance.Mission.Type);
            */
            if (___m_loaded_language != Loc.CurrentLanguageCode &&
                __instance.Mission.Type == MissionType.MULTIPLAYER) {
                _LevelInfo_ReadChallengeModeText_Method.Invoke(__instance, null);
                Debug.Log("CheckReloadStory: did MP file, music is " + __instance.MusicTrack);
                ___m_loaded_language = Loc.CurrentLanguageCode;
                return false;
            }
            return true;
        }
    }

    // also scan multi_mode for desc / music tags
    [HarmonyPatch(typeof(LevelInfo), "ReadChallengeModeText")]
    class MPCustomLevelInfoRead
    {
        public static void DoReadModeFile(LevelInfo level, Action<string> tag_parser)
        {
            Debug.Log("DoReadModeFile " + level.IsAddOn + " " + level.FileName);
            if (level.Mission.Type == MissionType.CHALLENGE)
                RobotManager.DoReadChallengeModeFile(level, tag_parser);
            else if (level.Mission.Type == MissionType.MULTIPLAYER)
                RobotManager.DoReadMultiplayerModeFile(level, tag_parser);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpCustomLevelInfoRead_DoReadModeFile_Method = AccessTools.Method(typeof(MPCustomLevelInfoRead), "DoReadModeFile");

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MemberInfo)code.operand).Name == "DoReadChallengeModeFile")
                    code.operand = mpCustomLevelInfoRead_DoReadModeFile_Method;
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(RobotManager), "ParseTagMultiplayer")]
    class MPCustomLevelInfoSkipInfoTags
    {
        private static bool Prefix(string[] words)
        {
            string word = words[0];
            if (word == "$pickup_check") {
                if (words.Length != 2 || !bool.TryParse(words[1], out MPCustomModeFile.PickupCheck))
                    Debug.Log("Invalid argument to pickup_check in multi_mode file. Must be true or false.");
                else
                    Debug.Log("pickup_check changed to " + MPCustomModeFile.PickupCheck);
                return false;
            }
            //Debug.Log("Multi tag: " + word);
            return word != null && word != "$music" && !word.StartsWith("$desc_");
        }
    }
}
