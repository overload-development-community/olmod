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
            string settingsFilename = "olmodsettings.json";
            if (Core.GameMod.FindArgVal("-config", out string configArg))
                settingsFilename = configArg;
            if (!settingsFilename.Contains(Path.DirectorySeparatorChar) && !settingsFilename.Contains(Path.AltDirectorySeparatorChar))
                settingsFilename = Path.Combine(OLModDir, settingsFilename);
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
            if (OLModDir == null || OLModDir == "") {
                OLModDir = Path.GetDirectoryName(typeof(Core.GameMod).Assembly.Location);
                //if (OLModDir != null && OLModDir.EndsWith(Path.DirectorySeparatorChar + "Overload_Data" + Path.DirectorySeparatorChar + "Managed", StringComparison.InvariantCultureIgnoreCase))
                //    OLModDir = Path.GetDirectoryName(Path.GetDirectoryName(OLModDir));
            }
            Debug.Log("olmod directory " + OLModDir);
            NoDownload = Core.GameMod.FindArg("-nodownload");
            Settings = new JObject();
            LoadSettings();
        }
    }
}
