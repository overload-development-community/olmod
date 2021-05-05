using Newtonsoft.Json;
using Overload;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Version = System.Version;
using System.Linq;

namespace GameMod.VersionHandling
{

    public class OlmodVersion
    {
        public Version LatestKnownVersion { get; set; }
        public Version RunningVersion { get; set; }

        public bool Modded { get; set; }

        private string _fullVersionString;

        public string FullVersionString
        { 
            get 
            {
                if (_fullVersionString == null)
                {
                    // do not include revision unless explicitly set to non-zero in the assembly version
                    string maybeRevision = RunningVersion.Revision > 0 ? $".{RunningVersion.Revision}" : "";
                    _fullVersionString = $"olmod  {RunningVersion.ToString(3)}{maybeRevision} {(Modded ? "**MODDED**" : "")}";
                }
                return _fullVersionString;
            }
        }

        public const string NewVersionReleasesUrl = "https://github.com/overload-development-community/olmod/releases";

        private const string NewVersionCheckUrl = "https://api.github.com/repos/overload-development-community/olmod/releases";

        public OlmodVersion()
        {
            RunningVersion = LatestKnownVersion = typeof(OlmodVersion).Assembly.GetName().Version;
        }
        
        public void TryRefreshLatestKnownVersion() 
        {
            
            if (GameManager.m_gm != null)
            {
                GameManager.m_gm.StartCoroutine(VersionRequest());
            }
        }

        IEnumerator VersionRequest() 
        {
            UnityWebRequest request = UnityWebRequest.Get(NewVersionCheckUrl);
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                yield break;
            }
            else
            {
                GitHubRelease latestRelease;
                try
                {
                    List<GitHubRelease> releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(request.downloadHandler.text);
                    if ((latestRelease = releases?.FirstOrDefault()) == null)
                    {
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log($"Unable to parse github releases response: {e}");
                    yield break;
                }
                                
                Match versionMatch1 = Regex.Match(latestRelease.tag_name, @"\d+\.\d+\.\d+(\.\d+)?");
                if (versionMatch1.Success)
                {
                    // store the latest known versions so we can alert players on the main menu screen if the running version is outdated
                    LatestKnownVersion = new Version(versionMatch1.Value);
                }
            }
        }


        private class GitHubRelease 
        {
            public string tag_name;
        }

    }
}
