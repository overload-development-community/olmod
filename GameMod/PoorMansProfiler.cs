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
        public ulong count = 0;
        public long ticksTotal;
        public long ticksMin;
        public long ticksMax;
        public Stopwatch watch = null;

        public MethodProfile()
        {
            method = null;
            watch = null;
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
        }

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


    public class MethodProfileCollector
    {
        public const int MaxEntryCount = 10000;
        public MethodProfile[] entry; //  = new MethodProfileEntry[MaxEntryCount];

        public MethodProfileCollector() {
            entry = new MethodProfile[MaxEntryCount];
        }
    }


    public class PoorMansProfiler
    {
        private static Dictionary<MethodBase,MethodProfile> profileData = new Dictionary<MethodBase, MethodProfile>();
        private static Dictionary<MethodBase,MethodProfileCollector> profileDataCollector = new Dictionary<MethodBase, MethodProfileCollector>();

        private static int curIdx = 0;
        private static int curFixedTick = 0;
        private static DateTime startTime = DateTime.UtcNow;
        private static int fixedTickCount = 180; // 3 second interval by default

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
            harmony.Patch(AccessTools.Method(typeof(Overload.GameManager), "FixedUpdate"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("FixedUpdatePostfix"), Priority.Last));
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

        // This is an additional Postfix to Overload.GameManager.FixedUpdate() to cycle the internal profiler data
        public static void FixedUpdatePostfix()
        {
            if (++curFixedTick >= fixedTickCount) {
                CycleInterval();
                curFixedTick = 0;
            }
        }

        // Collect the current profile Data into the collector
        public static void Collect(Dictionary<MethodBase,MethodProfileCollector> pdc, Dictionary<MethodBase,MethodProfile> data, int idx)
        {
            if (idx > MethodProfileCollector.MaxEntryCount) {
                return;
            }

            if (pdc.Count < 1) {
                foreach( KeyValuePair<MethodBase,MethodProfile> pair in data) {
                    MethodProfileCollector coll = new MethodProfileCollector();
                    coll.entry[idx] = pair.Value;
                    pdc[pair.Key] = coll;
                }
            } else {
                foreach( KeyValuePair<MethodBase,MethodProfile> pair in data) {
                    MethodProfileCollector coll = null;
                    UnityEngine.Debug.LogFormat("CXXXXXX {0} {1} {2} {3} {4}",pdc,data,coll,pair.Key,pair.Value);
                    try {
                        coll = pdc[pair.Key];
                    } catch (KeyNotFoundException) {
                        coll = new MethodProfileCollector();
                        pdc[pair.Key] = coll;
                    }
                    UnityEngine.Debug.LogFormat("XXXXXX {0} {1} {2} {3} {4}",pdc,data,coll,pair.Key,pair.Value);
                    coll.entry[idx] = pair.Value;
                }
            }
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


        // Function to write the statistics to a file
        public static void WriteResults(Dictionary<MethodBase,MethodProfileCollector> pdc, string filename, string timestamp, int cnt)
        {
            var sw = new StreamWriter(filename, false);
            sw.Write("+++ OLMOD - Poor Man's Profiler v1\n");
            sw.Write("+++ run at {0}\n",timestamp);
            int i;

            MethodProfile dummy = new MethodProfile();
            dummy.ticksTotal = 0;
            dummy.ticksMin = 0;
            dummy.ticksMax = 0;
            dummy.count = 0;

            for (i=0; i<cnt; i++) {
              sw.Write("{0}", i);
              foreach( KeyValuePair<MethodBase,MethodProfileCollector> pair in pdc) {
                  MethodProfile mp = pair.Value.entry[i];
                  if (mp == null) {
                      mp = dummy;
                  }
                  sw.Write("\t{0}", mp.ticksTotal);
              }
              sw.Write("\n");
            }
            sw.Write("+++ Dump ends here\n");
            sw.Dispose();
        }

        // Reset all profile data
        public static void ResetInterval()
        {
            // create a new Dict so in-fly operations are still well-defined
            profileData = new Dictionary<MethodBase,MethodProfile>();
        }

        /*
        public static void CycleInterval() {
            Dictionary<MethodBase,MethodProfile> data = profileData;
			string curDateTime = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string ftemplate = String.Format("olmod_pmp_{0}.csv", curDateTime);
            string fn = Path.Combine(Application.persistentDataPath, ftemplate);
            WriteResults(data, fn, curDateTime);
            ResetInterval();
        }
        */

        public static void CycleInterval() {
            Dictionary<MethodBase,MethodProfile> data = profileData;
            Collect(profileDataCollector, data, curIdx);
            curIdx++;
            ResetInterval();
        }

        public static void Cycle()
        {
            Dictionary<MethodBase,MethodProfileCollector> pdc = profileDataCollector;
            profileDataCollector = new Dictionary<MethodBase,MethodProfileCollector>();
			string curDateTime = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string ftemplate = String.Format("olmod_pmp_{0}.csv", curDateTime);
            string fn = Path.Combine(Application.persistentDataPath, ftemplate);
            WriteResults(pdc, fn, curDateTime, curIdx);
            startTime = DateTime.UtcNow;
            curIdx = 0;
        }

    }
}
