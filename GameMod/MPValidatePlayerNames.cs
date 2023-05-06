using System;
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
        public static void Prefix(PlayerLobbyData pld) {
            if (pld != null) {
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
            }
        }
    }
}
