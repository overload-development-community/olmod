using System.Collections.Generic;
using GameMod.Messages;
using GameMod.Metadata;
using UnityEngine.Networking;

namespace GameMod.Objects {
    /// <summary>
    /// A class to keep track of the current match's capabilities, and allows for those capabilities to be reset after the match.
    /// </summary>
    [Mod(Mods.Tweaks)]
    public static class Settings {
        private static readonly Dictionary<string, string> oldSettings = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> settings = new Dictionary<string, string>();

        /// <summary>
        /// Applies the set settings to the current match.
        /// </summary>
        public static void Apply() {
            foreach (var x in oldSettings)
                ApplySetting(x.Key, x.Value);

            oldSettings.Clear();

            foreach (var x in settings)
                oldSettings[x.Key] = ApplySetting(x.Key, x.Value);
        }

        /// <summary>
        /// Resets the current settings.
        /// </summary>
        public static void Reset() {
            settings.Clear();
        }

        /// <summary>
        /// Sends the current settings to the specified client, or to all clients if none is specified.
        /// </summary>
        /// <param name="conn_id"></param>
        public static void Send(int conn_id = -1) {
            var msg = new TweaksMessage { m_settings = settings };
            if (conn_id == -1) {
                foreach (var conn in NetworkServer.connections)
                    if (conn != null && Tweaks.ClientHasMod(conn.connectionId))
                        conn.Send(MessageTypes.MsgMPTweaksSet, msg);
            } else if (Tweaks.ClientHasMod(conn_id))
                NetworkServer.SendToClient(conn_id, MessageTypes.MsgMPTweaksSet, msg);
        }

        /// <summary>
        /// Sets the specified settings.
        /// </summary>
        /// <param name="newSettings"></param>
        public static void Set(Dictionary<string, string> newSettings) {
            settings.Clear();
            foreach (var x in newSettings)
                settings.Add(x.Key, x.Value);

            if (NetworkMatch.GetMatchState() == MatchState.PLAYING)
                Apply();
        }

        /// <summary>
        /// Applies the specific setting.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>
        /// What the setting's value should be set to after this match.
        /// </returns>
        private static string ApplySetting(string key, string value) {
            if (key == "item.pickupcheck" && bool.TryParse(value, out bool valBool)) {
                PickupCheck.Enabled = valBool;
                return bool.TrueString;
            }

            return null;
        }
    }
}
