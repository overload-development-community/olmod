using System;
using System.IO;
using System.Linq;
using GameMod.Objects;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameMod {
    static class Config
    {
        public static string OLModDir;
        public static JObject Settings;

        private static void LoadSettings()
        {
            if (string.IsNullOrEmpty(OLModDir))
            {
                Debug.Log("olmod directory unknown " + Environment.GetEnvironmentVariable("OLMODDIR") + " path " + Environment.GetEnvironmentVariable("PATH"));
                return;
            }
            string settingsFilename = "olmodsettings.json";
            if (Switches.ConfigFile != null)
                settingsFilename = Switches.ConfigFile;
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
            Settings = new JObject();
            LoadSettings();
        }
    }
}
