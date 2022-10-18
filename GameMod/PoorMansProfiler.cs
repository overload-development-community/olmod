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
        public string overrideName = null;
        public int overrideHash = 0;
        public ulong count = 0;
        public long ticksTotal;
        public long ticksMin;
        public long ticksMax;
        public Stopwatch watch = null;
        public double scaleFactor = 1.0;
        public int depth = 0;

        public MethodProfile()
        {
            method = null;
            overrideName = null;
            watch = null;
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
            depth = 0;
        }

        public MethodProfile(MethodBase mb)
        {
            method = mb;
            watch = new Stopwatch();
            scaleFactor = 1000.0 / (double)Stopwatch.Frequency;
            Reset();
        }

        public MethodProfile(string ovName, int ovHash)
        {
            method = null;
            watch = null;
            overrideName = ovName;
            overrideHash = ovHash;
            scaleFactor = 1000.0 / (double)Stopwatch.Frequency;
            Reset();
        }

        public void Reset()
        {
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
        }

        public void Start()
        {
            //UnityEngine.Debug.LogFormat("Prefix called {0}", method );
            depth++;
            if (depth == 1) {
              watch.Reset();
              watch.Start();
            }
        }

        public void End()
        {
            depth--;
            if (depth <= 0) {
                watch.Stop();
                long ticks = watch.ElapsedTicks;
                if (count == 0) {
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
                count++;
                depth = 0;
            }
            //UnityEngine.Debug.LogFormat("Postfix called {0} {1} {2} {3}", method, count, ticksTotal, ticksTotal/(double)count);
        }

        public void ImportTicks(long ticks)
        {
            if (count == 0) {
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
            count++;
        }

        public void ImportFrametime(float f)
        {
            long ticks = (long)(f * Stopwatch.Frequency);
            ImportTicks(ticks);
        }

        public enum Info {
            Name,
            AvgTime,
            TotalTime,
            MinTime,
            MaxTime,
            Count
        }

        public int GetHash()
        {
            if (String.IsNullOrEmpty(overrideName)) {
                return method.GetHashCode();
            }
            return overrideHash;
        }

        public double GetValueD(Info inf)
        {
            double cnt = (count > 0)?(double)count:1.0;
            double res = -1.0;
            switch(inf) {
                case Info.AvgTime:
                    res = ((double)ticksTotal * scaleFactor)/cnt;
                    break;
                case Info.TotalTime:
                    res = ((double)ticksTotal * scaleFactor);
                    break;
                case Info.MinTime:
                    res = ((double)ticksMin * scaleFactor);
                    break;
                case Info.MaxTime:
                    res = ((double)ticksMax * scaleFactor);
                    break;
                case Info.Count:
                    res = count;
                    break;
            }
            return res;
        }

        public string GetInfo(Info inf)
        {
            string result;

            if (inf == Info.Name) {
                if (String.IsNullOrEmpty(overrideName)) {
                    result = method.DeclaringType.Name + " " + method.ToString();
                } else {
                    result = overrideName;
                }
            } else if (inf == Info.Count) {
                result = count.ToString();
            } else {
                double val = GetValueD(inf);
                result = val.ToString();
            }
            return result;
        }

        public void WriteResults(StreamWriter sw)
        {
            sw.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\n", GetValueD(Info.AvgTime), GetInfo(Info.Count), GetValueD(Info.TotalTime), GetValueD(Info.MinTime), GetValueD(Info.MaxTime), GetInfo(Info.Name));
        }
    }


    public class MethodProfileCollector
    {
        public const int MaxEntryCount = 1500;
        public MethodProfile[] entry;

        public MethodProfileCollector() {
            entry = new MethodProfile[MaxEntryCount+1];
        }
    }


    public class PoorMansProfiler
    {
        private static Dictionary<MethodBase,MethodProfile> profileData = new Dictionary<MethodBase, MethodProfile>();
        private static Dictionary<MethodBase,MethodProfileCollector> profileDataCollector = new Dictionary<MethodBase, MethodProfileCollector>();
        private static Stopwatch IntervalWatch = new Stopwatch();

        private static int curIdx = 0;
        private static int curFixedTick = 0;
        private static DateTime startTime = DateTime.UtcNow;
        private static int fixedTickCount = 180; // 3 second interval by default
        private static long cycleLongIntervals = 60000; // >= 60 seconds long intervals force a full cycle

        private static MethodInfo pmpFrametimeDummy = AccessTools.Method(typeof(PoorMansProfiler),"PoorMansFrametimeDummy");
        private static MethodInfo pmpIntervalTimeDummy = AccessTools.Method(typeof(PoorMansProfiler),"PoorMansIntervalTimeDummy");

        public static bool LooksLikeMessageHander(MethodInfo m)
        {
            if (m != null && !String.IsNullOrEmpty(m.Name)) {
                var p = m.GetParameters();
                if (p.Length == 1 && (p[0].ParameterType.Name == "NetworkMessage")) {
                    if (m.Name.Length > 3 && m.Name[0] == 'O' && m.Name[1] == 'n' &&
                        m.Name != "OnSerialize" && m.Name != "OnDeserialize" && m.Name != "OnNetworkDestroy") {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool LooksLikeUpdateFunc(MethodInfo m)
        {
            if (m != null && !String.IsNullOrEmpty(m.Name) && m.Name == "Update") {
                var p = m.GetParameters();
                if (p.Length == 0 && m.ReturnType == typeof(void)) {
                    // ignore uninteresting or problematic functions...
                    if (m.DeclaringType.FullName.IndexOf("Rewired.") < 0 &&
                        m.DeclaringType.FullName.IndexOf("Smooth.") < 0 && 
                        m.DeclaringType.FullName.IndexOf("Window") < 0 && 
                        m.DeclaringType.FullName.IndexOf("Xbox") < 0 && 
                        m.DeclaringType.FullName.IndexOf("DonetwoSimpleCamera") < 0 && 
                        m.DeclaringType.FullName.IndexOf("SteamManager") < 0 && 
                        m.DeclaringType.FullName.IndexOf("uConsole") < 0 &&
                        m.DeclaringType.FullName.IndexOf("TrackIRComponent") < 0 &&
                        m.DeclaringType.FullName.IndexOf("Overload.SFXCueManager") < 0 &&
                        m.DeclaringType.FullName.IndexOf("UnityEngine.") < 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        // Initialize and activate the Profiler via harmony
        public static void Initialize(Harmony harmony)
        {
            string intervalLength;
            if (GameMod.Core.GameMod.FindArgVal("-pmp-interval", out intervalLength) && !String.IsNullOrEmpty(intervalLength)) {
                fixedTickCount = Int32.Parse(intervalLength);
            }
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: enabled, using intervals of {0} tixed Ticks", fixedTickCount);

            // Dictionary of all methods we want to profile
            Dictionary<MethodBase,bool> targetMethods = new Dictionary<MethodBase,bool>();

            // Get the list of all methods which were patched so far
            foreach(var m in harmony.GetPatchedMethods()) {
                targetMethods[m] = true;
            }

            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: found {0} patched methods", targetMethods.Count);

            // Get all olmod methods which look like a Message Handler (!)
            Assembly ourAsm = Assembly.GetExecutingAssembly();
            Assembly overloadAsm = Assembly.GetAssembly(typeof(Overload.GameManager));

            foreach (var t in ourAsm.GetTypes()) {
                foreach(var m in t.GetMethods(AccessTools.all)) {
                    if (LooksLikeMessageHander(m)) {
                        UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: additionally hooking {0} {1} (appears as message handler)", m.DeclaringType.Name, m);
                        targetMethods[m] = true;
                    }
                }
            }

            foreach (var t in overloadAsm.GetTypes()) {
                foreach(var m in t.GetMethods(AccessTools.all)) {
                    if (LooksLikeMessageHander(m)) {
                        UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: additionally hooking {0} {1} (appears as message handler)", m.DeclaringType.Name, m);
                        targetMethods[m] = true;
                    }
                    if (LooksLikeUpdateFunc(m)) {
                        UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: additionally hooking {0} {1} (appears as Update method)", m.DeclaringType, m);
                        targetMethods[m] = true;
                    }
                }
            }
            // NOTE: we could add other functions of interest here as well, up to iterating through the full assembly(!)
            
            // Patch the methods with the profiler prefix and postfix
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: applying to {0} methods", targetMethods.Count);
            MethodInfo mPrefix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPrefix");
            MethodInfo mPostfix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPostfix");
            var hmPrefix = new HarmonyMethod(mPrefix, Priority.First);
            var hmPostfix = new HarmonyMethod(mPostfix, Priority.Last);
            foreach (KeyValuePair<MethodBase,bool> pair in targetMethods) {
                if (pair.Value) {
                    harmony.Patch(pair.Key, hmPrefix, hmPostfix);
                }
            }

            // Additional Patches for management of the Profiler itself
            harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPostfix"), Priority.Last));
            harmony.Patch(AccessTools.Method(typeof(Overload.Client), "OnMatchEnd"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("MatchEndPostfix"), Priority.Last));
            harmony.Patch(AccessTools.Method(typeof(Overload.Client), "OnStartPregameCountdown"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPregamePostfix"), Priority.Last));
            harmony.Patch(AccessTools.Method(typeof(Overload.GameManager), "FixedUpdate"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("FixedUpdatePostfix"), Priority.Last));
            harmony.Patch(AccessTools.Method(typeof(Overload.GameManager), "Update"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("UpdatePostfix"), Priority.Last));

            startTime = DateTime.UtcNow;
            IntervalWatch.Reset();
            IntervalWatch.Start();
        }

        // The Prefix run at the start of every target method
        public static void PoorMansProfilerPrefix(MethodBase __originalMethod, out MethodProfile __state)
        {
            MethodProfile mp;
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
            Cycle("match");
        }

        // This is an additional Postfix to Overload.Client.OnStartPregameCountdown() to cycle the profiler data
        public static void StartPregamePostfix()
        {
            Cycle("pregame");
        }

        // This is an additional Postfix to Overload.GameManager.FixedUpdate() to cycle the internal profiler data
        public static void FixedUpdatePostfix()
        {
            if (cycleLongIntervals > 0 && (IntervalWatch.ElapsedMilliseconds > cycleLongIntervals )) {
                Cycle("long");
                return;
            }
            if (++curFixedTick >= fixedTickCount) {
                CycleInterval();
                curFixedTick = 0;
            }
        }

        // This is an additional Postfix to Overload.GameManager.Update() to gather frame statistics
        public static void UpdatePostfix()
        {
            MethodProfile mp;
            try {
                mp = profileData[pmpFrametimeDummy];
            } catch (KeyNotFoundException) {
                mp = new MethodProfile("+++PMP-Frametime",-7777);
                profileData[pmpFrametimeDummy] = mp;
            }
            mp.ImportFrametime(Time.unscaledDeltaTime);
        }

        // This is a dummy method only used for FPS statistic as key in the Dictionaries...
        public static void PoorMansFrametimeDummy()
        {
        }

        // This is a dummy method only used for FPS statistic as key in the Dictionaries...
        public static void PoorMansIntervalTimeDummy()
        {
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
                    coll.entry[MethodProfileCollector.MaxEntryCount] = pair.Value;
                    coll.entry[idx] = pair.Value;
                    pdc[pair.Key] = coll;
                }
            } else {
                foreach( KeyValuePair<MethodBase,MethodProfile> pair in data) {
                    MethodProfileCollector coll = null;
                    try {
                        coll = pdc[pair.Key];
                    } catch (KeyNotFoundException) {
                        coll = new MethodProfileCollector();
                        coll.entry[MethodProfileCollector.MaxEntryCount] = pair.Value;
                        pdc[pair.Key] = coll;
                    }
                    coll.entry[idx] = pair.Value;
                }
            }
        }

        // Console command pmpcycle
        static void CmdCycle()
        {
            Cycle("manual");
        }

        public static string GetTimestamp(DateTime ts)
        {
			return ts.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
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


        // Function to write the statistics info file
        public static void WriteResultsInfo(Dictionary<MethodBase,MethodProfileCollector> pdc, string baseFilename, int cnt, DateTime tsBegin, DateTime tsEnd)
        {
            var sw = new StreamWriter(baseFilename + "info.csv", false);
            sw.Write("+++ OLMOD - Poor Man's Profiler v1\n");
            sw.Write("+++ run {0} to {1}, {2} intervals, {3} methods\n",GetTimestamp(tsBegin), GetTimestamp(tsEnd), cnt, pdc.Count);

            foreach( KeyValuePair<MethodBase,MethodProfileCollector> pair in pdc) {
                MethodProfile lmp = pair.Value.entry[MethodProfileCollector.MaxEntryCount];
                sw.Write("{0}\t{1}\n",lmp.GetHash(),lmp.GetInfo(MethodProfile.Info.Name));
            }
            sw.Dispose();
        }


        // Function to write one result channel
        public static void WriteResultsValue(Dictionary<MethodBase,MethodProfileCollector> pdc, string baseFilename, int cnt, MethodProfile.Info inf)
        {
            MethodProfile dummy = new MethodProfile();
            dummy.Reset();

            var sw = new StreamWriter(baseFilename + inf.ToString() + ".csv", false);
            foreach( KeyValuePair<MethodBase,MethodProfileCollector> pair in pdc) {
                MethodProfile lmp = pair.Value.entry[MethodProfileCollector.MaxEntryCount];
                sw.Write("{0}",lmp.GetHash());
                for (int i=0; i<cnt; i++) {
                    MethodProfile mp = pair.Value.entry[i];
                    if (mp == null) {
                        mp = dummy;
                    }
                    sw.Write("\t{0}", mp.GetInfo(inf));
                }
                sw.Write("\t{0}\n",lmp.GetInfo(MethodProfile.Info.Name));
            }
            sw.Dispose();
        }

        // Function to write the statistics to a files
        public static void WriteResults(Dictionary<MethodBase,MethodProfileCollector> pdc, string baseFilename, int cnt, DateTime tsBegin, DateTime tsEnd)
        {
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: cycle: {0} intervals, {1} methods to {2}*.csv",cnt,pdc.Count,baseFilename);
            WriteResultsInfo(pdc, baseFilename, cnt, tsBegin, tsEnd);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.Count);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.AvgTime);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.MinTime);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.MaxTime);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.TotalTime);
        }

        // Reset the current interval Data
        public static void ResetInterval()
        {
            // create a new Dict so in-fly operations are still well-defined
            profileData = new Dictionary<MethodBase,MethodProfile>();
            IntervalWatch.Reset();
            IntervalWatch.Start();
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

        // Cycle a single profiler interval
        public static void CycleInterval() {
            IntervalWatch.Stop();
            Dictionary<MethodBase,MethodProfile> data = profileData;
            MethodProfile intervalTime = new MethodProfile("+++PMP-Interval", -7778);
            intervalTime.ImportTicks(IntervalWatch.ElapsedTicks);
            data[pmpIntervalTimeDummy] = intervalTime;
            Collect(profileDataCollector, data, curIdx);
            curIdx++;
            if (curIdx >= MethodProfileCollector.MaxEntryCount) {
                Cycle("flush");
            }
            ResetInterval();
        }

        // Cycle Profile Data Collection: flush to disk and start new
        public static void Cycle(string reason)
        {
            Dictionary<MethodBase,MethodProfileCollector> pdc = profileDataCollector;
            profileDataCollector = new Dictionary<MethodBase,MethodProfileCollector>();
            DateTime tsEnd = DateTime.UtcNow;
			string curDateTime = GetTimestamp(tsEnd);
            string ftemplate = String.Format("olmod_pmp_{0}_{1}_", curDateTime, reason);
            string fn = Path.Combine(Application.persistentDataPath, ftemplate);
            WriteResults(pdc, fn, curIdx, startTime, tsEnd);
            startTime = DateTime.UtcNow;
            ResetInterval();
            curIdx = 0;
        }

    }
}
