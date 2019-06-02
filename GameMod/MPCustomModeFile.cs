using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(RobotManager), "DoReadMultiplayerModeFile")]
    class MPCustomModeFile
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

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "Empty")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = code.labels };
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPCustomModeFile), "ReadCustomFile"));
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
                typeof(LevelInfo).GetMethod("ReadChallengeModeText", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, null);
                Debug.Log("CheckReloadStory: did MP file, music is " + __instance.MusicTrack);
                ___m_loaded_language = Loc.CurrentLanguageCode;
                return false;
            }
            return true;
        }
    }

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
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MemberInfo)code.operand).Name == "DoReadChallengeModeFile")
                    code.operand = AccessTools.Method(typeof(MPCustomLevelInfoRead), "DoReadModeFile");
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
            Debug.Log("Multi tag: " + word);
            return word != null && word != "$music" && !word.StartsWith("$desc_");
        }
    }
}
