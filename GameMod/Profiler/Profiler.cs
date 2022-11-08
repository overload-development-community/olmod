using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Profiler {
    [Mod(Mods.Profiler)]
    public class PoorMansProfiler {
        private static int initMode = 0;
        private static bool isServer = false;
        private static Harmony lazyHarmony = null;
        private static Dictionary<MethodBase, MethodProfile> profileData = new Dictionary<MethodBase, MethodProfile>();
        private static Dictionary<MethodBase, MethodProfile>[] intervalData = new Dictionary<MethodBase, MethodProfile>[MethodProfileCollector.MaxEntryCount];

        public static Stopwatch timerBase = new Stopwatch();
        public static double timerBaseToMS = -1.0;
        private static long intervalStart = 0;
        private static long intervalEnd = 0;

        private static int curIdx = 0;
        private static int curFixedTick = 0;
        private static DateTime startTime = DateTime.UtcNow;
        private static int fixedTickCount = 60; // 1 second interval by default (during MP at least)
        private const long cycleLongIntervals = 60000; // >= 60 seconds long intervals force a full cycle
        private static string outputPath = null;
        private static bool useLocking = false;

        private static readonly MethodInfo pmpFrametimeDummy = AccessTools.Method(typeof(PoorMansProfiler), "PoorMansFrametimeDummy");
        private static readonly MethodInfo pmpIntervalTimeDummy = AccessTools.Method(typeof(PoorMansProfiler), "PoorMansIntervalTimeDummy");

        // Initialize and activate the Profiler via harmony
        public static void Initialize(Harmony harmony) {
            if (initMode >= 2) {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: already initialized");
                return;
            }

            if (initMode == 0 && Switches.Lazy) {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: lazy init");
                // only initialize the console command Patch
                harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPostfix"), Priority.Last));
                lazyHarmony = harmony;
                initMode = 1;
                return;
            }

            if (!string.IsNullOrEmpty(Switches.Interval)) {
                fixedTickCount = int.Parse(Switches.Interval);
            }
            if (!string.IsNullOrEmpty(Switches.OutputPath)) {
                outputPath = Path.Combine(Application.persistentDataPath, Switches.OutputPath);
            } else {
                outputPath = Application.persistentDataPath;
            }
            int lockingMode = -1;
            if (!string.IsNullOrEmpty(Switches.Locking)) {
                if (!int.TryParse(Switches.Locking, NumberStyles.Number, CultureInfo.InvariantCulture, out lockingMode)) {
                    lockingMode = -1;
                }
                fixedTickCount = int.Parse(Switches.Locking);
            }

            if (lockingMode < 0) {
                // AUTO: on for servers, off for client (?!)
                if (GameplayManager.IsDedicatedServer()) {
                    UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: server detected, enable locking");
                    lockingMode = 1;
                } else {
                    UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: client detected, not enabling locking");
                    lockingMode = 0;
                }
            }
            useLocking = (lockingMode > 0);

            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: enabled, interval: {0}, lock: {1}, output path: {2}", fixedTickCount, useLocking, outputPath);

            // Dictionary of all previously patched methods
            Dictionary<MethodBase, bool> patchedMethods = new Dictionary<MethodBase, bool>();
            // List of all methods we want to profile
            Dictionary<MethodBase, bool> targetMethods = new Dictionary<MethodBase, bool>();

            // Get the list of all fiters
            PoorMansFilterList filters = new PoorMansFilterList();
            if (!string.IsNullOrEmpty(Switches.Filter)) {
                foreach (var f in Switches.Filter.Split(';', ',', ':')) {
                    filters.LoadStandardLocation(f);
                }
            } else {
                filters.LoadStandardLocation("pmp-filters.txt");
            }
            if (filters.filters.Count < 1) {
                filters.AddDefaults();
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: using default filters");
            }
            //filters.Save("/tmp/pmpa");
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: using filter with {0} entries", filters.filters.Count);

            // apply to the previously patched methods
            foreach (var m in harmony.GetPatchedMethods()) {
                patchedMethods[m] = true;
                if (filters.Apply(m, true)) {
                    targetMethods[m] = true;
                    UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: selected {0} {1} (previously patched)", m.DeclaringType.FullName, m.ToString());
                }
            }
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: found {0} previously patched methods", patchedMethods.Count);


            Assembly ourAsm = Assembly.GetExecutingAssembly();
            Assembly overloadAsm = Assembly.GetAssembly(typeof(Overload.GameManager));
            Assembly unityAsm = Assembly.GetAssembly(typeof(Physics));

            Assembly[] assemblies = new Assembly[] { ourAsm, overloadAsm, unityAsm };
            foreach (var asm in assemblies) {
                foreach (var t in asm.GetTypes()) {
                    foreach (var m in t.GetMethods(AccessTools.all)) {
                        //UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: XXX {0} {1}", m.DeclaringType.FullName, m.ToString());
                        if (!patchedMethods.ContainsKey(m) && filters.Apply(m, false)) {
                            targetMethods[m] = true;
                            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: selected {0} {1}", m.DeclaringType.FullName, m.ToString());
                        }
                    }
                }
            }

            // Patch the methods with the profiler prefix and postfix
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: applying to {0} methods", targetMethods.Count);
            MethodInfo mPrefix;
            if (useLocking) {
                mPrefix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPrefixLock");
            } else {
                mPrefix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPrefix");
            }

            MethodInfo mPostfix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPostfix");
            var hmPrefix = new HarmonyMethod(mPrefix, Priority.First);
            var hmPostfix = new HarmonyMethod(mPostfix, Priority.Last);
            foreach (KeyValuePair<MethodBase, bool> pair in targetMethods) {
                if (pair.Value) {
                    harmony.Patch(pair.Key, hmPrefix, hmPostfix);
                }
            }

            // Additional Patches for management of the Profiler itself
            if (initMode < 1) {
                harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPostfix"), Priority.Last));
            }
            if (GameplayManager.IsDedicatedServer()) {
                isServer = true;
                harmony.Patch(AccessTools.Method(typeof(NetworkMatch), "ExitMatch"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("MatchEndPostfix"), Priority.Last));
            } else {
                harmony.Patch(AccessTools.Method(typeof(Overload.GameplayManager), "DoneLevel"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("MatchEndPostfix"), Priority.Last));
            }
            harmony.Patch(AccessTools.Method(typeof(Overload.Client), "OnStartPregameCountdown"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPregamePostfix"), Priority.Last));
            harmony.Patch(AccessTools.Method(typeof(Overload.GameManager), "FixedUpdate"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("FixedUpdatePostfix"), Priority.Last));
            if (useLocking) {
                harmony.Patch(AccessTools.Method(typeof(Overload.GameManager), "Update"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("UpdatePostfixLock"), Priority.Last));
            } else {
                harmony.Patch(AccessTools.Method(typeof(Overload.GameManager), "Update"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("UpdatePostfix"), Priority.Last));
            }

            startTime = DateTime.UtcNow;
            timerBaseToMS = 1000.0 / (double)Stopwatch.Frequency;
            timerBase.Reset();
            timerBase.Start();
            intervalStart = timerBase.ElapsedTicks;
            initMode = 2;
        }

        // The Prefix run at the start of every target method
        public static void PoorMansProfilerPrefix(MethodBase __originalMethod, out MethodProfile __state) {
            MethodProfile mp;
            try {
                mp = profileData[__originalMethod];
            } catch (KeyNotFoundException) {
                mp = new MethodProfile(__originalMethod);
                profileData[__originalMethod] = mp;
            }
            __state = mp.Start();
        }

        // The Prefix run at the start of every target method, version with locking
        public static void PoorMansProfilerPrefixLock(MethodBase __originalMethod, out MethodProfile __state) {
            MethodProfile mp;
            lock (profileData) {
                try {
                    mp = profileData[__originalMethod];
                } catch (KeyNotFoundException) {
                    mp = new MethodProfile(__originalMethod);
                    profileData[__originalMethod] = mp;
                }
            }
            __state = mp.Start();
        }

        // The Postfix run at the end of every target method
        public static void PoorMansProfilerPostfix(MethodProfile __state) {
            if (__state != null) {
                __state.End();
            }
        }

        // This is an additional Postfix to GameManager.Start() to registe our console commands
        public static void StartPostfix() {
            uConsole.RegisterCommand("pmpinit", "Initialize Poor Man's Profiler", CmdInit);
            uConsole.RegisterCommand("pmpcycle", "Cycle Poor Man's Profiler data", CmdCycle);
            uConsole.RegisterCommand("pmpinterval", "Set Poor Man's Profiler interval", CmdInterval);
        }

        // This is an additional Postfix to cycle the profiler data at match end
        public static void MatchEndPostfix() {
            Cycle("match");
        }

        // This is an additional Postfix to Overload.Client.OnStartPregameCountdown() to cycle the profiler data
        public static void StartPregamePostfix() {
            Cycle("pregame");
        }

        // This is an additional Postfix to Overload.GameManager.FixedUpdate() to cycle the internal profiler data
        public static void FixedUpdatePostfix() {
            if (cycleLongIntervals > 0 && (timerBase.ElapsedMilliseconds - intervalStart > cycleLongIntervals)) {
                Cycle("long");
                return;
            }
            if (++curFixedTick >= fixedTickCount) {
                CycleInterval();
                curFixedTick = 0;
            }
        }

        // This is an additional Postfix to Overload.GameManager.Update() to gather frame statistics
        public static void UpdatePostfix() {
            MethodProfile mp;
            try {
                mp = profileData[pmpFrametimeDummy];
            } catch (KeyNotFoundException) {
                mp = new MethodProfile("+++PMP-Frametime", -7777);
                profileData[pmpFrametimeDummy] = mp;
            }
            mp.ImportFrametime(Time.unscaledDeltaTime);
        }

        // This is an additional Postfix to Overload.GameManager.Update() to gather frame statistics, version with locking
        public static void UpdatePostfixLock() {
            MethodProfile mp;
            lock (profileData) {
                try {
                    mp = profileData[pmpFrametimeDummy];
                } catch (KeyNotFoundException) {
                    mp = new MethodProfile("+++PMP-Frametime", -7777);
                    profileData[pmpFrametimeDummy] = mp;
                }
            }
            mp.ImportFrametime(Time.unscaledDeltaTime);
        }

        // This is a dummy method only used for FPS statistic as key in the Dictionaries...
        public static void PoorMansFrametimeDummy() {
        }

        // This is a dummy method only used for FPS statistic as key in the Dictionaries...
        public static void PoorMansIntervalTimeDummy() {
        }

        // Collect the current profile Data into the collector
        public static void Collect(Dictionary<MethodBase, MethodProfileCollector> pdc, Dictionary<MethodBase, MethodProfile> data, int idx) {
            if (idx > MethodProfileCollector.MaxEntryCount) {
                return;
            }

            if (pdc.Count < 1) {
                foreach (KeyValuePair<MethodBase, MethodProfile> pair in data) {
                    MethodProfileCollector coll = new MethodProfileCollector();
                    coll.entry[MethodProfileCollector.MaxEntryCount] = pair.Value;
                    coll.entry[idx] = pair.Value;
                    pdc[pair.Key] = coll;
                }
            } else {
                foreach (KeyValuePair<MethodBase, MethodProfile> pair in data) {
                    MethodProfileCollector coll;
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
        static void CmdCycle() {
            Cycle("manual");
        }

        // Console command pmpinit
        static void CmdInit() {
            if (lazyHarmony != null) {
                Initialize(lazyHarmony);
            }
        }

        // Console command pmpinterval
        static void CmdInterval() {
            if (!uConsole.NextParameterIsInt()) {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: interval is {0} fixed ticks", fixedTickCount);
                return;
            }
            int val = uConsole.GetInt();
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: setting interval from {0} to {1} fixed ticks", fixedTickCount, val);
            fixedTickCount = val;
        }

        public static string GetTimestamp(DateTime ts) {
            return ts.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Function to write the statistics to a file
        public static void WriteResults(Dictionary<MethodBase, MethodProfile> data, string filename, string timestamp) {
            var sw = new StreamWriter(filename, false);
            sw.Write("+++ OLMOD - Poor Man's Profiler v1\n");
            sw.Write("+++ run at {0}, server: {1}\n", timestamp, isServer);
            foreach (KeyValuePair<MethodBase, MethodProfile> pair in data) {
                //UnityEngine.Debug.LogFormat("XXX {0}", pair.Value.method);
                pair.Value.WriteResults(sw);
            }
            sw.Write("+++ Dump ends here\n");
            sw.Dispose();
        }


        // Function to write the statistics info file
        public static void WriteResultsInfo(Dictionary<MethodBase, MethodProfileCollector> pdc, string baseFilename, int cnt, DateTime tsBegin, DateTime tsEnd) {
            var sw = new StreamWriter(baseFilename + "info.csv", false);
            sw.Write("+++ OLMOD - Poor Man's Profiler v1\n");
            sw.Write("+++ run {0} to {1}, {2} intervals, {3} methods, server: {4}\n", GetTimestamp(tsBegin), GetTimestamp(tsEnd), cnt, pdc.Count, isServer);

            int idx = 0;
            foreach (KeyValuePair<MethodBase, MethodProfileCollector> pair in pdc) {
                MethodProfile lmp = pair.Value.entry[MethodProfileCollector.MaxEntryCount];
                sw.Write("{0}\t{1}\t{2}\n", idx, lmp.GetHash(), lmp.GetInfo(MethodProfile.Info.Name));
                idx++;
            }
            sw.Dispose();
        }


        // Function to write one result channel
        public static void WriteResultsValue(Dictionary<MethodBase, MethodProfileCollector> pdc, string baseFilename, int cnt, MethodProfile.Info inf) {
            MethodProfile dummy = new MethodProfile();
            dummy.Reset();

            var sw = new StreamWriter(baseFilename + inf.ToString() + ".csv", false);
            foreach (KeyValuePair<MethodBase, MethodProfileCollector> pair in pdc) {
                MethodProfile lmp = pair.Value.entry[MethodProfileCollector.MaxEntryCount];
                sw.Write("{0}", lmp.GetHash());
                for (int i = 0; i < cnt; i++) {
                    MethodProfile mp = pair.Value.entry[i];
                    if (mp == null) {
                        mp = dummy;
                    }
                    sw.Write("\t{0}", mp.GetInfo(inf));
                }
                sw.Write("\t{0}\n", lmp.GetInfo(MethodProfile.Info.Name));
            }
            sw.Dispose();
        }

        // Function to write the statistics to a files
        public static void WriteResults(Dictionary<MethodBase, MethodProfileCollector> pdc, string baseFilename, int cnt, DateTime tsBegin, DateTime tsEnd) {
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: cycle: {0} intervals, {1} methods to {2}*.csv", cnt, pdc.Count, baseFilename);
            WriteResultsInfo(pdc, baseFilename, cnt, tsBegin, tsEnd);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.Count);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.AvgTime);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.MinTime);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.MaxTime);
            WriteResultsValue(pdc, baseFilename, cnt, MethodProfile.Info.TotalTime);
        }

        // Reset the current interval Data
        public static Dictionary<MethodBase, MethodProfile> ResetInterval() {
            // create a new Dict so in-fly operations are still well-defined
            Dictionary<MethodBase, MethodProfile> newProfileData = new Dictionary<MethodBase, MethodProfile>();
            Dictionary<MethodBase, MethodProfile> data = Interlocked.Exchange(ref profileData, newProfileData);
            intervalStart = timerBase.ElapsedTicks;
            return data;
        }

        /*
        public static void CycleInterval() {
            Dictionary<MethodBase,MethodProfile> data = profileData;
			string curDateTime = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string ftemplate = String.Format("olmod_pmp_{0}.csv", curDateTime);
            string fn = Path.Combine(outputPath, ftemplate);
            WriteResults(data, fn, curDateTime);
            ResetInterval();
        }
        */

        // Cycle a single profiler interval
        public static void CycleInterval() {
            intervalEnd = timerBase.ElapsedTicks;
            long previousStart = intervalStart;
            Dictionary<MethodBase, MethodProfile> data = ResetInterval();
            MethodProfile intervalTime = new MethodProfile("+++PMP-Interval", -7778);
            intervalTime.ImportTicks(intervalEnd - previousStart);
            if (useLocking) {
                lock (data) {
                    data[pmpIntervalTimeDummy] = intervalTime;
                }
            } else {
                data[pmpIntervalTimeDummy] = intervalTime;
            }
            intervalData[curIdx] = data;
            //Collect(profileDataCollector, data, curIdx);
            curIdx++;
            if (curIdx >= MethodProfileCollector.MaxEntryCount) {
                Cycle("flush");
            }
        }

        // Cycle Profile Data Collection: flush to disk and start new
        public static void Cycle(string reason) {
            DateTime tsEnd = DateTime.UtcNow;
            int intervalCnt = Interlocked.Exchange(ref curIdx, 0);
            Dictionary<MethodBase, MethodProfile>[] intervals = Interlocked.Exchange(ref intervalData, new Dictionary<MethodBase, MethodProfile>[MethodProfileCollector.MaxEntryCount]);
            Dictionary<MethodBase, MethodProfileCollector> pdc = new Dictionary<MethodBase, MethodProfileCollector>();
            for (int i = 0; i < intervalCnt; i++) {
                Collect(pdc, intervals[i], i);
            }
            string curDateTime = GetTimestamp(tsEnd);
            string ftemplate = String.Format("olmod_pmp{0}_{1}_{2}_", ((isServer) ? "_srv" : ""), curDateTime, reason);
            string fn = Path.Combine(outputPath, ftemplate);
            WriteResults(pdc, fn, intervalCnt, startTime, tsEnd);
            startTime = DateTime.UtcNow;
            ResetInterval();
        }
    }
}
