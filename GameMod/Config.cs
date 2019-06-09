using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameMod
{
    static class Config
    {
        public static bool NoDownload;

        public static void Init()
        {
            NoDownload = Core.GameMod.FindArg("-nodownload");
        }
    }
}
