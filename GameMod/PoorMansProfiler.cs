using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.VersionHandling;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class MethodProfile
    {
        public MethodBase method = null;
        public ulong count;
        public long ticksTotal;
        public long ticksMin;
        public long ticksMax;
        public Stopwatch watch = null;

        public MethodProfile(MethodBase mb)
        {
            method = mb;
            watch = new Stopwatch();
            Reset();
        }

        public void Reset()
        {
            count = 0;
            watch.Reset();
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
        }

        public void Start()
        {
            //UnityEngine.Debug.LogFormat("Prefix called {0}", method );
            watch.Reset();
            watch.Start();
        }

        public void End()
        {
            watch.Stop();
            long ticks = watch.ElapsedTicks;
            count++;
            if (count == 1) {
                ticksMin = ticks;
                ticksMax = ticks;
            } else {
                if (ticks < ticksMin) {
                    ticksMin = ticks;
                } else if (ticks > ticksMax) {
                    ticksMax = ticks;
                }
            }
            ticksTotal += ticks;
            //UnityEngine.Debug.LogFormat("Postfix called {0} {1} {2} {3}", method, count, ticksTotal, ticksTotal/(double)count);
        }

        public void WriteResults(StreamWriter sw)
        {
            double s = 1000.0/(double)Stopwatch.Frequency; 
            double cnt = (count > 0)?(double)count:1.0;

            sw.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\n", (ticksTotal * s)/cnt, count, (ticksTotal*s), (ticksMin *s), (ticksMax *s), method.DeclaringType.Name, method);
        }
    }


    public class PoorMansProfiler
    {
        private static Dictionary<MethodBase,MethodProfile> profileData = new Dictionary<MethodBase, MethodProfile>();

        // Initialize and activate the Profiler via harmony
        public static void Initialize(Harmony harmony)
        {
            // Get the list of all methods which were patched so far
            List<MethodBase> origMethods = harmony.GetPatchedMethods().ToList();
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: will apply to {0} patched methods", origMethods.Count);

            // Get all olmod methods which look like a Message Handler (!)
            Assembly ourAsm = Assembly.GetExecutingAssembly();
            foreach (var t in ourAsm.GetTypes()) {
                foreach(var m in t.GetMethods(AccessTools.all)) {
                    if (m != null && !String.IsNullOrEmpty(m.Name)) {
                        var p = m.GetParameters();
                        if (p.Length == 1 && (p[0].ParameterType.Name == "NetworkMessage")) {
                            if (m.Name.Length > 3 && m.Name[0] == 'O' && m.Name[1] == 'n' &&
                                m.Name != "OnSerialize" && m.Name != "OnDeserialize" && m.Name != "OnNetworkDestroy") {
                                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: additionally hooking {0} (appears as message handler)", m);
                                origMethods.Add(m);
                            }
                        }
                    }
                }
            }
            // NOTE: we could add other functions of interest here as well, up to iterating through the full assembly(!)
            
            // Patch the methods with the profiler prefix and postfix
            MethodInfo mPrefix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPrefix");
            MethodInfo mPostfix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPostfix");
            var hmPrefix = new HarmonyMethod(mPrefix, Priority.First);
            var hmPostfix = new HarmonyMethod(mPostfix, Priority.Last);
            foreach (var m in origMethods) {
                harmony.Patch(m, hmPrefix, hmPostfix);
            }

            // Additional Patches for management of the Profiler itself
            harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPostfix"), Priority.Last));
            harmony.Patch(AccessTools.Method(typeof(Overload.Client), "OnMatchEnd"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("MatchEndPostfix"), Priority.Last));
        }

        // The Prefix run at the start of every target method
        public static void PoorMansProfilerPrefix(MethodBase __originalMethod, out MethodProfile __state)
        {
            MethodProfile mp = null;
            try {
                mp = profileData[__originalMethod];
            } catch (KeyNotFoundException) {
                mp = new MethodProfile(__originalMethod);
                profileData[__originalMethod] = mp;
            }
            __state = mp;
            __state.Start();
        }

        // The Postfix run at the end of every target method
        public static void PoorMansProfilerPostfix(MethodProfile __state)
        {
            __state.End();
        }

        // This is an additional Postfix to GameManager.Start() to registe our console commands
        public static void StartPostfix()
        {
            uConsole.RegisterCommand("pmpcycle", "Cycle Poor Man's Profiler data", CmdCycle);
        }

        // This is an additional Postfix to Overload.Client.OnMatchEnd() to cycle the profiler data
        public static void MatchEndPostfix()
        {
            Cycle();
        }

        // Console command pmpcycle
        static void CmdCycle()
        {
            Cycle();
        }

        // Function to write the statistics to a file
        public static void WriteResults(Dictionary<MethodBase,MethodProfile> data, string filename, string timestamp)
        {
            var sw = new StreamWriter(filename, false);
            sw.Write("+++ OLMOD - Poor Man's Profiler v1\n");
            sw.Write("+++ run at {0}\n",timestamp);
            foreach( KeyValuePair<MethodBase,MethodProfile> pair in data) {
                //UnityEngine.Debug.LogFormat("XXX {0}", pair.Value.method);
                pair.Value.WriteResults(sw);
            }
            sw.Write("+++ Dump ends here\n");
            sw.Dispose();
        }

        // Reset all profile data
        public static void Reset()
        {
            // create a new Dict so in-fly operations are still well-defined
            profileData = new Dictionary<MethodBase,MethodProfile>();
        }

        public static void Cycle() {
            Dictionary<MethodBase,MethodProfile> data = profileData;
			string curDateTime = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string ftemplate = String.Format("olmod_pmp_{0}.csv", curDateTime);
            string fn = Path.Combine(Application.persistentDataPath, ftemplate);
            WriteResults(data, fn, curDateTime);
            Reset();
        }
    }
}
