using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    static class DataReader
    {
        public static string GetData(TextAsset ta, string filename)
        {
            string dir = Environment.GetEnvironmentVariable("OLMODDIR");
            try
            {
                return File.ReadAllText(dir + Path.DirectorySeparatorChar + filename);
            }
            catch (FileNotFoundException)
            {
            }
            return ta.text;
        }
        public static string GetProjData(TextAsset ta)
        {
            return GetData(ta, "projdata.txt");
        }
        public static string GetRobotData(TextAsset ta)
        {
            return GetData(ta, "robotdata.txt");
        }
    }

    [HarmonyPatch(typeof(Overload.ProjectileManager), "ReadProjPresetData")]
    class ReadProjPresetDataPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                    yield return new CodeInstruction(OpCodes.Call, typeof(DataReader).GetMethod("GetProjData"));
                else
                    yield return code;
        }
    }

    [HarmonyPatch(typeof(RobotManager), "ReadPresetData")]
    class ReadRobotPresetDataPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                    yield return new CodeInstruction(OpCodes.Call, typeof(DataReader).GetMethod("GetRobotData"));
                else
                    yield return code;
        }
    }
}
