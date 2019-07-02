using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameMod
{
    static class Config
    {
        public static string OLModDir;
        public static bool NoDownload;
        public static JObject Settings;

        private static void LoadSettings()
        {
            if (string.IsNullOrEmpty(OLModDir))
            {
                Debug.Log("olmod directory unknown " + Environment.GetEnvironmentVariable("OLMODDIR") + " path " + Environment.GetEnvironmentVariable("PATH"));
                return;
            }
            string settingsFilename = Path.Combine(OLModDir, "olmodsettings.json");
            try
            {
                Settings = JObject.Parse(File.ReadAllText(settingsFilename));
                Debug.Log("olmod settings loaded from " + settingsFilename);
            }
            catch (Exception ex)
            {
                Debug.Log("olmod settings loading failed " + ex.Message + " " + settingsFilename);
            }
        }

        public static void Init()
        {
            OLModDir = Environment.GetEnvironmentVariable("OLMODDIR");
            NoDownload = Core.GameMod.FindArg("-nodownload");
            Settings = new JObject();
            LoadSettings();
        }
    }
}
