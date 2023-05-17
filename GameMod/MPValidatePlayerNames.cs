using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Overload;

namespace GameMod {
    /// <summary>
    /// Prevents players to use invalid / empty names
    /// - spaces are allowed, but not at the beginning and end, and not exclusively
    /// - other sorts of whitespaces are converted to space
    /// - Non-ASCII characters are still allowed
    /// - Only Uppercase characters are allowed, convert to uppercase in any case
    /// - forbidden characters (as per IsValidPilotNameChar) and characters not
    ///   in the font are replaced by a $ sign
    /// - At least one non-forbidden character must be present
    /// - maximum length is 17 characters, longer names are cut
    /// </summary>
    [HarmonyPatch(typeof(Overload.Server), "ResolvePotentialNameCollision")]
    class MPValidatePlayerNames {
        // Helper function: get the player name without the "<X>" number suffix, and the number
        // The suffix can be one or two digits.
        private static string GetBaseName(string name, out int number)
        {
            number = 0;
            if (String.IsNullOrEmpty(name)) {
                return "";
            }
            if (name.EndsWith(">")) {
                int idxStart = name.LastIndexOf(" <");
                if (idxStart > 0) {
                    int len = name.Length - idxStart - 3;
                    if (len >= 1 && len <= 2) {
                        string potentialNumber = name.Substring(idxStart+2, len);
                        if (int.TryParse(potentialNumber, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) {
                            if (number > 0) {
                                name = name.Substring(0, idxStart);
                            } else {
                                number = 0;
                            }
                        }
                    }
                }
            }
            return name;
        }

        // completely replace ResolvePotentialNameCollision() by a fixed and improved version
        public static bool Prefix(PlayerLobbyData pld) {
            if (pld != null) {
                // Step 1: validate name
                bool nameValid = false;
                if (!String.IsNullOrEmpty(pld.m_name)) {
                    pld.m_name = pld.m_name.ToUpper(); // only uppercase
                    pld.m_name = pld.m_name.Trim(); // replace any leading or trailing whitespaces
                    if (pld.m_name.Length > 17) {     // at most 17 characters
                        pld.m_name = pld.m_name.Substring(0,17);
                    }
                    for (int i=0; i<pld.m_name.Length; i++) {
                        Char ch = pld.m_name[i];
                        if ((Char.IsWhiteSpace(ch) || Char.IsSeparator(ch)) && ch != ' ')  {
                            // all whitespace and separator charachters which aren't space are converted to space
                            pld.m_name = pld.m_name.Replace(ch, ' ');
                        } else if (FontInfo.IsInCharset((int)ch) && PilotManager.IsValidPilotNameChar(ch)) {
                            nameValid = true;
                        } else {
                            // replace invalid characters by '$'
                            pld.m_name = pld.m_name.Replace(ch, '$');
                        }
                    }
                }
                if (!nameValid) {
                    pld.m_name = "<INVALID NAME>";
                }

                // Step 2: resolve any name collisions
                // Picks the lowest free number for this name, beginning from 2
                ulong collisionSet = 0;
                foreach (KeyValuePair<int, PlayerLobbyData> player in NetworkMatch.m_players) {
                    PlayerLobbyData otherPlayerValue = player.Value;
                    if (pld != otherPlayerValue) {
                        int number;
                        string baseName = GetBaseName(otherPlayerValue.m_name, out number);
                        if (baseName == pld.m_name) {
                            // same base name as we, mark as used
                            collisionSet = collisionSet | (((ulong)1)<<(number&63));
                        }
                    }
                }
                if (collisionSet != 0) {
                    int idx;
                    for (idx=2; idx<64; idx++) {
                        if ( (collisionSet & (((ulong)1)<<idx)) == 0) {
                            break;
                        }
                    }
                    pld.m_name = String.Format("{0} <{1}>", pld.m_name, idx);
                }
            }
            return false; // skip the original in every case
        }
    }
}
