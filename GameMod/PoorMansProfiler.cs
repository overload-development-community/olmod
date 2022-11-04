using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading;
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
        public long ticksStart;
        public int depth = 0;

        public MethodProfile()
        {
            method = null;
            overrideName = null;
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
            depth = 0;
        }

        public MethodProfile(MethodBase mb)
        {
            method = mb;
            Reset();
        }

        public MethodProfile(string ovName, int ovHash)
        {
            method = null;
            overrideName = ovName;
            overrideHash = ovHash;
            Reset();
        }

        public void Reset()
        {
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
            depth = 0;
        }

        public MethodProfile Start()
        {
            //UnityEngine.Debug.LogFormat("Prefix called {0}", method );
            if (Interlocked.Exchange(ref depth, 1) > 0) {
                return null;
            }
            ticksStart = PoorMansProfiler.timerBase.ElapsedTicks;
            return this;
        }

        public void End()
        {
            long ticks = PoorMansProfiler.timerBase.ElapsedTicks - ticksStart;
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
            Interlocked.Exchange(ref depth, 0);
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
                    res = ((double)ticksTotal * PoorMansProfiler.timerBaseToMS)/cnt;
                    break;
                case Info.TotalTime:
                    res = ((double)ticksTotal * PoorMansProfiler.timerBaseToMS);
                    break;
                case Info.MinTime:
                    res = ((double)ticksMin * PoorMansProfiler.timerBaseToMS);
                    break;
                case Info.MaxTime:
                    res = ((double)ticksMax * PoorMansProfiler.timerBaseToMS);
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
                    result = method.DeclaringType.FullName + " " + method.ToString();
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
        public const int MaxEntryCount = 7500;
        public MethodProfile[] entry;

        public MethodProfileCollector() {
            entry = new MethodProfile[MaxEntryCount+1];
        }
    }

    public class PoorMansFilter
    {
        public enum Operation {
            None,
            Include,
            Exclude,
        }

        public enum Select {
            Exact,
            Contains,
            RegEx,
            Always,
        }

        public enum Mode {
            All,
            PreviouslyPatched,
        }

        public enum Flags : uint {
            ShortTypeName = 0x1,
        }

        public Operation op;
        public Select sel;
        public Mode   mode;
        public string typeFilter;
        public string methodFilter;
        public uint flags;

        public PoorMansFilter(Operation o, Select s, Mode m, uint f, string typeF, string methodF)
        {
            Set(o,s,m,f,typeF,methodF);
        }

        public PoorMansFilter(String lineDesc)
        {
            if (!Set(lineDesc)) {
                Set(Operation.None, Select.Contains, Mode.All, 0, null, null);
            }
        }

        public void Set(Operation o, Select s, Mode m, uint f, string typeF, string methodF)
        {
            op = o;
            sel = s;
            mode = m;
            flags = f;
            typeFilter = typeF;
            methodFilter = methodF;
        }

        public bool Set(string lineDesc)
        {
            Operation o = Operation.Include;
            Select s = Select.Contains;
            Mode m = Mode.All;
            uint f = 0;
            string typeF = null;
            string methodF = null;

            if (!String.IsNullOrEmpty(lineDesc)) {
                string[] parts = lineDesc.Split('\t');
                if (parts.Length < 2) {
                    methodF = lineDesc;
                }  else {
                    o = GetOp(parts[0]);
                    s = GetSel(parts[0]);
                    m = GetMode(parts[0]);
                    f = GetFlags(parts[0]);
                    if (parts.Length > 2) {
                        typeF = parts[1];
                        methodF = parts[2];
                    } else {
                        methodF = parts[1];
                    }
                }
                Set(o,s,m,f,typeF,methodF);
                return true;
            }
            return false;
        }

        private static Operation GetOp(string opts)
        {
            Operation o = Operation.Include;

            for (var i=0; i<opts.Length; i++) {
                switch(opts[i]) {
                    case '+':
                        o = Operation.Include;
                        break;
                    case '-':
                        o = Operation.Exclude;
                        break;
                    case 'N':
                        o = Operation.None;
                        break;
                }
            }
            return o;
        }

        private static Select GetSel(string opts)
        {
            Select s = Select.Contains;

            for (var i=0; i<opts.Length; i++) {
                switch(opts[i]) {
                    case '=':
                        s = Select.Exact;
                        break;
                    case 'R':
                        s = Select.RegEx;
                        break;
                    case 'C':
                        s = Select.Contains;
                        break;
                    case '*':
                        s = Select.Always;
                        break;
                }
            }
            return s;
        }

        private static Mode GetMode(string opts)
        {
            Mode m = Mode.All;

            for (var i=0; i<opts.Length; i++) {
                switch(opts[i]) {
                    case 'a':
                        m = Mode.All;
                        break;
                    case 'p':
                        m = Mode.PreviouslyPatched;
                        break;
                }
            }
            return m;
        }

        private static uint GetFlags(string opts)
        {
            uint f = 0;

            for (var i=0; i<opts.Length; i++) {
                switch(opts[i]) {
                    case '_':
                        f |= (uint)Flags.ShortTypeName;
                        break;
                }
            }
            return f;
        }

        public void Write(StreamWriter sw)
        {
            string o;
            string s;
            string m;
            string f="";

            switch (op) {
                case Operation.Include:
                    o = "+";
                    break;
                case Operation.Exclude:
                    o = "-";
                    break;
                default:
                    o = "N";
                    break;
            }

            switch(sel) {
                case Select.Exact:
                    s = "=";
                    break;
                case Select.RegEx:
                    s = "R";
                    break;
                case Select.Always:
                    s = "*";
                    break;
                default:
                    s = "C";
                    break;
            }

            switch (mode) {
                case Mode.PreviouslyPatched:
                    m = "p";
                    break;
                default:
                    m = "a";
                    break;
            }

            if ( (flags & (uint)Flags.ShortTypeName) != 0) {
                f += "_";
            }
            sw.Write("{0}{1}{2}\t{3}\t{4}\n",o,s,m,f,typeFilter, methodFilter);
        }

        public Operation Apply(MethodBase m, bool isPreviouslyPatched)
        {
            if (op == Operation.None) {
                return op;
            }

            if (mode == Mode.PreviouslyPatched && !isPreviouslyPatched) {
                return Operation.None;
            }

            string tname = ((flags & (uint)Flags.ShortTypeName) != 0)?m.DeclaringType.Name:m.DeclaringType.FullName;
            if (Matches(tname, typeFilter) && Matches(m.ToString(), methodFilter)) {
                return op;
            }
            return Operation.None;
        }

        private bool Matches(string str, string filter)
        {
            if (String.IsNullOrEmpty(filter)) {
                return true;
            }
            switch (sel) {
                case Select.Exact:
                    if (str == filter) {
                        return true;
                    }
                    break;
                case Select.Contains:
                    if (str.IndexOf(filter)>=0) {
                        return true;
                    }
                    break;
                case Select.RegEx:
                    Regex rgx = new Regex(filter);
                    return rgx.IsMatch(str);
                case Select.Always:
                    return true;
            }
            return false;
        }
    }

    public class PoorMansFilterList
    {
        public List<PoorMansFilter> filters = new List<PoorMansFilter>();

        public bool Load(string filename, bool warnIfNotFound = true)
        {
            if (File.Exists(filename)) {
                StreamReader sr = new StreamReader(filename, new System.Text.UTF8Encoding());
                string line;
                int cnt = 0;
                while( (line = sr.ReadLine()) != null) {
                    if (line[0] != '#') {
                        PoorMansFilter f = new PoorMansFilter(line);
                        if (f.op != PoorMansFilter.Operation.None) {
                          Add(f);
                          cnt ++;
                        }
                    }
                }
                sr.Dispose();
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: added {0} filters from list {1}", cnt, filename);
                return true;
            }
            if (warnIfNotFound) {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: can't find filter list file {0}", filename);
            }
            return false;
        }

        public bool LoadStandardLocation(string filename)
        {
            if (Path.IsPathRooted(filename)) {
                return Load(filename);
            }
            string[] paths = new string[]{Application.persistentDataPath, Config.OLModDir};
            foreach (var path in paths) {
                string fn = Path.Combine(path, filename);
                if (Load(fn, false)) {
                    return true;
                }
            }
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: can't find filter list file {0}", filename);
            return false;
        }

        public void Save(string filename)
        {
            var sw = new StreamWriter(filename, false);
            sw.Write("# POOR MAN's PROFILER Filter File v1\n");
            foreach (var f in filters) {
                f.Write(sw);
            }

            sw.Dispose();
        }

        public void Add(PoorMansFilter f)
        {
            filters.Add(f);
        }

        public void Add(MethodBase m, bool isPreviouslyPatched)
        {
            Add (new PoorMansFilter(PoorMansFilter.Operation.Include,
                                    PoorMansFilter.Select.Exact,
                                    (isPreviouslyPatched)?PoorMansFilter.Mode.PreviouslyPatched:PoorMansFilter.Mode.All,
                                    0,
                                    m.DeclaringType.FullName,
                                    m.ToString()));
        }

        public void AddDefaults()
        {
            // Add all previously patched Methods
            Add (new PoorMansFilter(PoorMansFilter.Operation.Include,
                                    PoorMansFilter.Select.Always,
                                    PoorMansFilter.Mode.PreviouslyPatched,
                                    0,
                                    null,
                                    null));
        }

        public bool Apply(MethodBase m, bool isPreviouslyPatched)
        {
            foreach(var f in filters) {
                PoorMansFilter.Operation op = f.Apply(m, isPreviouslyPatched);
                if (op == PoorMansFilter.Operation.Include) {
                    return true;
                }
                if (op == PoorMansFilter.Operation.Exclude) {
                    return false;
                }
            }
            return false;
        }
    }


    public class PoorMansProfiler
    {
        private static int initMode = 0;
        private static bool isServer = false;
        private static Harmony lazyHarmony = null;
        private static Dictionary<MethodBase,MethodProfile> profileData = new Dictionary<MethodBase, MethodProfile>();
        private static Dictionary<MethodBase,MethodProfile>[] intervalData = new Dictionary<MethodBase, MethodProfile>[MethodProfileCollector.MaxEntryCount];

        public static Stopwatch timerBase = new Stopwatch();
        public static  double timerBaseToMS = -1.0;
        private static long intervalStart = 0;
        private static long intervalEnd = 0;

        private static int curIdx = 0;
        private static int curFixedTick = 0;
        private static DateTime startTime = DateTime.UtcNow;
        private static int fixedTickCount = 60; // 1 second interval by default (during MP at least)
        private static long cycleLongIntervals = 60000; // >= 60 seconds long intervals force a full cycle
        private static string outputPath = null;
        private static bool useLocking = false;

        private static MethodInfo pmpFrametimeDummy = AccessTools.Method(typeof(PoorMansProfiler),"PoorMansFrametimeDummy");
        private static MethodInfo pmpIntervalTimeDummy = AccessTools.Method(typeof(PoorMansProfiler),"PoorMansIntervalTimeDummy");

        // Initialize and activate the Profiler via harmony
        public static void Initialize(Harmony harmony)
        {
            if (initMode >= 2) {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: already initialized");
                return;
            }

            if (initMode == 0 && GameMod.Core.GameMod.FindArg("-pmp-lazy")) {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: lazy init");
                // only initialize the console command Patch
                harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPostfix"), Priority.Last));
                lazyHarmony = harmony;
                initMode = 1;
                return;
            }

            string intervalLength;
            if (GameMod.Core.GameMod.FindArgVal("-pmp-interval", out intervalLength) && !String.IsNullOrEmpty(intervalLength)) {
                fixedTickCount = Int32.Parse(intervalLength);
            }
            string outPath = null;
            if (GameMod.Core.GameMod.FindArgVal("-pmp-output-path", out outPath) && !String.IsNullOrEmpty(outPath)) {
                outputPath = Path.Combine(Application.persistentDataPath, outPath);
            } else {
                outputPath = Application.persistentDataPath;
            }
            string lockingModeArg;
            int lockingMode = -1;
            if (GameMod.Core.GameMod.FindArgVal("-pmp-locking", out lockingModeArg) && !String.IsNullOrEmpty(lockingModeArg)) {
                if (!int.TryParse(lockingModeArg, NumberStyles.Number, CultureInfo.InvariantCulture, out lockingMode)) {
                    lockingMode = -1;
                }
                fixedTickCount = Int32.Parse(intervalLength);
            }

            if (lockingMode < 0) {
                // AUTO: on for servers, off for client (?!)
                if (GameMod.Core.GameMod.FindArg("-batchmode")) {
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
            Dictionary<MethodBase,bool> patchedMethods = new Dictionary<MethodBase,bool>();
            // List of all methods we want to profile
            Dictionary<MethodBase,bool> targetMethods = new Dictionary<MethodBase,bool>();

            // Get the list of all fiters
            string filterFileArg = null;
            PoorMansFilterList filters = new PoorMansFilterList();
            if (GameMod.Core.GameMod.FindArgVal("-pmp-filter", out filterFileArg) && !String.IsNullOrEmpty(filterFileArg)) {
                foreach (var f in filterFileArg.Split(';',',',':')) {
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
            foreach(var m in harmony.GetPatchedMethods()) {
                patchedMethods[m] = true;
                if (filters.Apply(m,true)) {
                    targetMethods[m] = true;
                    UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: selected {0} {1} (previously patched)", m.DeclaringType.FullName, m.ToString());
                }
            }
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: found {0} previously patched methods", patchedMethods.Count);


            Assembly ourAsm = Assembly.GetExecutingAssembly();
            Assembly overloadAsm = Assembly.GetAssembly(typeof(Overload.GameManager));
            Assembly unityAsm = Assembly.GetAssembly(typeof(Physics));

            Assembly[] assemblies=new Assembly[]{ourAsm, overloadAsm, unityAsm};
            foreach (var asm in assemblies) {
                foreach (var t in asm.GetTypes()) {
                    foreach(var m in t.GetMethods(AccessTools.all)) {
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
            MethodInfo mPrefix = null;
            if (useLocking){
                mPrefix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPrefixLock");
            } else {
                mPrefix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPrefix");
            }

            MethodInfo mPostfix = typeof(PoorMansProfiler).GetMethod("PoorMansProfilerPostfix");
            var hmPrefix = new HarmonyMethod(mPrefix, Priority.First);
            var hmPostfix = new HarmonyMethod(mPostfix, Priority.Last);
            foreach (KeyValuePair<MethodBase,bool> pair in targetMethods) {
                if (pair.Value) {
                    harmony.Patch(pair.Key, hmPrefix, hmPostfix);
                }
            }

            // Additional Patches for management of the Profiler itself
            if (initMode < 1) {
                harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(PoorMansProfiler).GetMethod("StartPostfix"), Priority.Last));
            }
            if (GameMod.Core.GameMod.FindArg("-batchmode")) {
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
        public static void PoorMansProfilerPrefix(MethodBase __originalMethod, out MethodProfile __state)
        {
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
        public static void PoorMansProfilerPrefixLock(MethodBase __originalMethod, out MethodProfile __state)
        {
            MethodProfile mp;
            lock(profileData) {
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
        public static void PoorMansProfilerPostfix(MethodProfile __state)
        {
            if (__state != null) {
                __state.End();
            }
        }

        // This is an additional Postfix to GameManager.Start() to registe our console commands
        public static void StartPostfix()
        {
            uConsole.RegisterCommand("pmpinit", "Initialize Poor Man's Profiler", CmdInit);
            uConsole.RegisterCommand("pmpcycle", "Cycle Poor Man's Profiler data", CmdCycle);
            uConsole.RegisterCommand("pmpinterval", "Set Poor Man's Profiler interval", CmdInterval);
        }

        // This is an additional Postfix to cycle the profiler data at match end
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
            if (cycleLongIntervals > 0 && (timerBase.ElapsedMilliseconds - intervalStart > cycleLongIntervals )) {
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

        // This is an additional Postfix to Overload.GameManager.Update() to gather frame statistics, version with locking
        public static void UpdatePostfixLock()
        {
            MethodProfile mp;
            lock(profileData) {
                try {
                    mp = profileData[pmpFrametimeDummy];
                } catch (KeyNotFoundException) {
                    mp = new MethodProfile("+++PMP-Frametime",-7777);
                    profileData[pmpFrametimeDummy] = mp;
                }
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

        // Console command pmpinit
        static void CmdInit()
        {
            if (lazyHarmony != null) {
                Initialize(lazyHarmony);
            }
        }

        // Console command pmpinterval
        static void CmdInterval()
        {
            if (!uConsole.NextParameterIsInt())
            {
                UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: interval is {0} fixed ticks", fixedTickCount);
                return;
            }
            int val = uConsole.GetInt();
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: setting interval from {0} to {1} fixed ticks", fixedTickCount, val);
            fixedTickCount = val;
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
            sw.Write("+++ run at {0}, server: {1}\n",timestamp, isServer);
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
            sw.Write("+++ run {0} to {1}, {2} intervals, {3} methods, server: {4}\n",GetTimestamp(tsBegin), GetTimestamp(tsEnd), cnt, pdc.Count, isServer);

            int idx = 0;
            foreach( KeyValuePair<MethodBase,MethodProfileCollector> pair in pdc) {
                MethodProfile lmp = pair.Value.entry[MethodProfileCollector.MaxEntryCount];
                sw.Write("{0}\t{1}\t{2}\n",idx, lmp.GetHash(),lmp.GetInfo(MethodProfile.Info.Name));
                idx++;
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
        public static Dictionary<MethodBase,MethodProfile> ResetInterval()
        {
            // create a new Dict so in-fly operations are still well-defined
            Dictionary<MethodBase,MethodProfile> newProfileData = new Dictionary<MethodBase,MethodProfile>();
            Dictionary<MethodBase,MethodProfile> data = Interlocked.Exchange(ref profileData, newProfileData);
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
            Dictionary<MethodBase,MethodProfile> data = ResetInterval();
            MethodProfile intervalTime = new MethodProfile("+++PMP-Interval", -7778);
            intervalTime.ImportTicks(intervalEnd - intervalStart);
            if (useLocking) {
                lock(data) {
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
        public static void Cycle(string reason)
        {
            DateTime tsEnd = DateTime.UtcNow;
            int intervalCnt = Interlocked.Exchange(ref curIdx, 0);
            Dictionary<MethodBase,MethodProfile>[] intervals = Interlocked.Exchange(ref intervalData, new Dictionary<MethodBase, MethodProfile>[MethodProfileCollector.MaxEntryCount]);
            Dictionary<MethodBase,MethodProfileCollector> pdc = new Dictionary<MethodBase, MethodProfileCollector>();
            for (int i=0; i<intervalCnt; i++) {
                Collect(pdc, intervals[i], i);
            }
            string curDateTime = GetTimestamp(tsEnd);
            string ftemplate = String.Format("olmod_pmp{0}_{1}_{2}_", ((isServer)?"_srv":""),curDateTime, reason);
            string fn = Path.Combine(outputPath, ftemplate);
            WriteResults(pdc, fn, intervalCnt, startTime, tsEnd);
            startTime = DateTime.UtcNow;
            ResetInterval();
        }

    }
}
