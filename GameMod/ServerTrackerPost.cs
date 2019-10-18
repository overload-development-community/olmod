using Harmony;
using Newtonsoft.Json.Linq;
using Overload;
using System.Timers;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(MenuManager), "MainMenuUpdate")]
    class ServerTrackerPost
    {
        private static bool started = false;
        private static Timer timer;
        private static string url;

        public static void Postfix()
        {
            if (!Config.Settings.Value<bool>("isServer") ||
                string.IsNullOrEmpty(url = Config.Settings.Value<string>("trackerBaseUrl")))
                return;

            if (!started && NetworkManager.IsHeadless())
            {
                started = true;
                Ping();

                timer = new Timer(5 * 60 * 1000);

                timer.Elapsed += Ping;
                timer.AutoReset = true;
                timer.Enabled = true;
            }
        }

        public static void Ping(object source = null, ElapsedEventArgs e = null)
        {
            ServerStatLog.Post(url + "/api/ping", JObject.FromObject(new
            {
                keepListed = Config.Settings.Value<bool>("keepListed"),
                name = Config.Settings.Value<string>("serverName"),
                notes = Config.Settings.Value<string>("notes")
            }));
        }
    }
}
