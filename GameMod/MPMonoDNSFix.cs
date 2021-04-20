using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GameMod
{
    // do not call GetIPv4Properties on Windows, it crashes with >2 DNS servers
    // it's only used for LoopbackInterfaceIndex which is a stub on Windows
    // https://github.com/Unity-Technologies/mono/blob/unity-2017.4/mcs/class/System/System.Net.NetworkInformation/NetworkInterface.cs#L101
    [HarmonyPatch(typeof(BroadcastState), "EnumerateNetworkInterfaces")]
    class MPMonoDNSFix
    {
        // only patch on Windows
        private static bool Prepare() {
            // https://github.com/Unity-Technologies/mono/blob/unity-2017.4/mcs/class/corlib/System/Environment.cs#L742
            var runningOnWindows = ((int) System.Environment.OSVersion.Platform < 4);
            return runningOnWindows;
        }

        private static System.Net.NetworkInformation.IPv4InterfaceProperties GetIPv4Properties(object ipProperties) {
            return null;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int n = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "GetIPv4Properties") {
                    code.operand = typeof(MPMonoDNSFix).GetMethod("GetIPv4Properties", BindingFlags.NonPublic | BindingFlags.Static);
                    n++;
                }
                yield return code;
            }
        }
    }
}
