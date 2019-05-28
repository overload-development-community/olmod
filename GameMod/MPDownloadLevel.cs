using Harmony;
using Newtonsoft.Json.Linq;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine.Networking;

namespace GameMod
{
    class MPDownloadLevel
    {
        private static bool downloadBusy;

        private static bool CanCreateFile(string filename)
        {
            try
            {
                File.Create(filename).Close();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static IEnumerable<string> DoDownloadLevel(string url)
        {
            var i = url.LastIndexOf('/');
            var basefn = url.Substring(i + 1);
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Path.Combine("Revival", "Overload"));
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            var fn = Path.Combine(dir, basefn);
            if (File.Exists(fn))
            {
                yield return "DOWNLOAD FAILED: " + basefn + " ALREADY EXISTS";
                yield break;
            }
            if (!CanCreateFile(fn + ".tmp"))
            {
                dir = Path.Combine(Environment.CurrentDirectory, "DLC");
                try { Directory.CreateDirectory(dir); } catch (Exception) { }
                fn = Path.Combine(dir, basefn);
                if (File.Exists(fn))
                {
                    yield return "DOWNLOAD FAILED: " + basefn + " ALREADY EXISTS";
                    yield break;
                }
                if (!CanCreateFile(fn + ".tmp"))
                {
                    yield return "DOWNLOAD FAILED: DIRECTORY UNWRITABLE";
                    yield break;
                }
            }
            var fntmp = fn + ".tmp";

            downloadBusy = true;
            using (UnityWebRequest www = new UnityWebRequest(url, "GET"))
            {
                www.downloadHandler = new DownloadHandlerFile(fntmp);
                var request = www.SendWebRequest();
                while (!request.isDone)
                {
                    yield return "DOWNLOADING " + basefn + " ... " + Math.Round(request.progress * 100) + "%";
                }
                if (www.isNetworkError || www.isHttpError)
                {
                    File.Delete(fntmp);
                    yield return "DOWNLOADING " + basefn + " FAILED!";
                    downloadBusy = false;
                    yield break;
                }
            }
            downloadBusy = false;
            yield return "DOWNLOADING " + basefn + " COMPLETED, RELOADING...";
            File.Move(fntmp, fn);
            Console.CmdReloadMissions();
            yield return "DOWNLOADING " + basefn + " COMPLETED";
        }

        private static IEnumerable<bool> DoGetLevel(string level)
        {
            Debug.Log("DoGetLevel " + level);
            var li = level.IndexOf(".MP");
            MenuManager.AddMpStatus("SEARCHING " + level.Substring(0, li), 1f, 9);
            string last = null;
            foreach (var x in NetworkMatch.Get("mpget", new Dictionary<string, string> { { "level", level } }, "https://www.overloadmaps.com/api/"))
            {
                last = x;
                yield return false;
            }
            JObject ret = JObject.Parse(last);
            if (ret.TryGetValue("url", out JToken urlVal))
            {
                string url = urlVal.GetString();
                var i = url.LastIndexOf('/');
                MenuManager.AddMpStatus("DOWNLOADING " + url.Substring(i + 1), 1f, 9);
                yield return false;
                foreach (var msg in DoDownloadLevel(url))
                {
                    MenuManager.AddMpStatus(msg, 1f, 9);
                    yield return false;
                }
            }
            else
                MenuManager.AddMpStatus("LEVEL NOT FOUND ON OVERLOADMAPS.COM", 2f, 9);
            yield return true;
        }

        public static void StartGetLevel(string level)
        {
            if (downloadBusy)
                return;
            var tasks = typeof(NetworkMatch).GetField("m_gatewayWebTasks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            tasks.GetType().GetMethod("Add").Invoke(tasks, new object[] { DoGetLevel(level).GetEnumerator() });
        }
    }

    [HarmonyPatch(typeof(Client), "OnLobbyStatusToClient")]
    class MPSetupClientLobbyStatus
    {
        static void Postfix()
        {
            string level = NetworkMatch.m_last_lobby_status.m_match_playlist_addon_idstringhash;
            if (level != null && level != "" && GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(level) < 0)
                MPDownloadLevel.StartGetLevel(level);
        }
    }
}
