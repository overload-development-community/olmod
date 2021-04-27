using Overload;
using System.Collections;
using UnityEngine.Networking;
using Version = System.Version;

namespace GameMod.VersionHandling
{

    class OlmodVersion
    {
        public Version LatestKnownVersion { get; set; }
        public Version RunningVersion { get; set; }

        public OlmodVersion()
        {
            LatestKnownVersion = RunningVersion = typeof(OlmodVersion).Assembly.GetName().Version;
            TryRefreshLatestKnownVersion();
        }

        public void TryRefreshLatestKnownVersion() 
        {
            GameManager.m_gm.StartCoroutine(versionRequest());
        }

        IEnumerator versionRequest() 
        {
            string url = "https://github.com/overload-development-community/olmod-stable-binaries/blob/master/bin/README.txt";
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                uConsole.Log(request.error);
            }
            else
            {
                string test = request.downloadHandler.text;
            }
        }

    }
}
