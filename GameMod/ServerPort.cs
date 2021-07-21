using HarmonyLib;
using Overload;

namespace GameMod
{
    [HarmonyPatch(typeof(Server), "Listen")]
    class ServerPort
    {
        static int PortArg = 0;

        private static bool Prepare(){
            if (!Core.GameMod.FindArgVal("-port", out string arg) || !int.TryParse(arg, out int val))
                return false;
            PortArg = val;
            return true;
        }

        private static void Prefix(ref int port)
        {
            if (port == 0)
                port = PortArg;
        }
    }
}
