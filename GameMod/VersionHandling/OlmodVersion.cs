using Overload;
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Version = System.Version;

namespace GameMod.VersionHandling
{

    public class OlmodVersion
    {
        public Version LatestKnownVersion { get; set; }
        public Version RunningVersion { get; set; }

        public bool Modded { get; set; }

        public string FullVersionString => $"olmod {RunningVersion}{(Modded ? " **MODDED**" : "")}";

        public OlmodVersion()
        {
            LatestKnownVersion = RunningVersion = typeof(OlmodVersion).Assembly.GetName().Version;
        }
        
        public void TryRefreshLatestKnownVersion() 
        {
            
            if (GameManager.m_gm != null)
            {
                GameManager.m_gm.StartCoroutine(versionRequest());
            }
        }

        IEnumerator versionRequest() 
        {
            string url = "https://raw.githubusercontent.com/overload-development-community/olmod-stable-binaries/master/bin/README.txt";
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                yield break;
            }
            else
            {
                // version is on first line by convention
                string firstLine;
                using (var reader = new StringReader(request.downloadHandler.text))
                {
                    firstLine = reader.ReadLine();
                }

                if (String.IsNullOrEmpty(firstLine))
                {
                    yield break;
                }

                Match versionMatch = Regex.Match(firstLine, @"\d+\.\d+\.\d+(\.\d+)?");
                if (!versionMatch.Success)
                {
                    yield break;
                }

                // store the latest known versions so we can alert players on the main menu screen if the running version is outdated
                LatestKnownVersion = new Version(versionMatch.Value);
                uConsole.Log($"Retrieved latest olmod version number: {LatestKnownVersion}");
            }
        }



    }
}
