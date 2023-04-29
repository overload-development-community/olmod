using System;
using HarmonyLib;
using Overload;

namespace GameMod {
    /// <summary>
    /// Prevents players to use invalid / empty names
    /// - all names must have at least one printable ASCII character
    /// - spaces are allowed, but not at the beginning and end, and not exclusively
    /// - Non-ASCII characters are still allowed
    /// - Only Uppercase characters are allowed, convert to uppercase in any case
    /// - forbidden characters are the following set: "*/:<>?\|
    ///   as well as all non-prinable values, we replace all of these by $
    /// - maximum length is 17 characters, longer names are cut
    /// </summary>
    [HarmonyPatch(typeof(Overload.Server), "ResolvePotentialNameCollision")]
    class MPValidatePlayerNames {
        public static void Prefix(PlayerLobbyData pld) {
            if (pld != null) {
                bool nameValid = false;
                if (!String.IsNullOrEmpty(pld.m_name)) {
                    pld.m_name = pld.m_name.ToUpper(); // only uppercase
                    if (pld.m_name.Length > 17) {     // at most 17 characters
                        pld.m_name = pld.m_name.Substring(0,17);
                    }
                    for (int i=0; i<pld.m_name.Length; i++) {
                        Char ch = pld.m_name[i];
                        if (Char.IsControl(ch) || ch == '"' || ch == '*' || ch == '/' || ch == ':' || ch == '<' || ch == '>' || ch == '?' ||  ch == '\\' || ch =='|') {
                            // ininvisible control characters or forbidden characters are replaced  by `$`
                            pld.m_name = pld.m_name.Replace(ch, '$');
                            nameValid = true;
                        } else if ((Char.IsWhiteSpace(ch) || Char.IsSeparator(ch)) && ch != ' ')  {
                            // all whitespace and separator charachters which aren't space are converted to space
                            pld.m_name = pld.m_name.Replace(ch, ' ');
                        } else if (ch > 32 && ch < 127) {
                            nameValid = true;
                        }
                    }
                    pld.m_name = pld.m_name.Trim(); // replace any leading or trailing whitespaces
                }
                if (!nameValid) {
                    pld.m_name = "<INVALID NAME>";
                }
            }
        }
    }
}
