using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using GameMod.Metadata;

namespace GameMod.Profiler {
    [Mod(Mods.Profiler)]
    public class MethodProfile {
        public MethodBase method = null;
        public string overrideName = null;
        public int overrideHash = 0;
        public ulong count = 0;
        public long ticksTotal;
        public long ticksMin;
        public long ticksMax;
        public long ticksStart;
        public int depth = 0;

        public MethodProfile() {
            method = null;
            overrideName = null;
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
            depth = 0;
        }

        public MethodProfile(MethodBase mb) {
            method = mb;
            Reset();
        }

        public MethodProfile(string ovName, int ovHash) {
            method = null;
            overrideName = ovName;
            overrideHash = ovHash;
            Reset();
        }

        public void Reset() {
            count = 0;
            ticksTotal = 0;
            ticksMin = 0;
            ticksMax = 0;
            depth = 0;
        }

        public MethodProfile Start() {
            //UnityEngine.Debug.LogFormat("Prefix called {0}", method );
            if (Interlocked.Exchange(ref depth, 1) > 0) {
                return null;
            }
            ticksStart = PoorMansProfiler.timerBase.ElapsedTicks;
            return this;
        }

        public void End() {
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

        public void ImportTicks(long ticks) {
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

        public void ImportFrametime(float f) {
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

        public int GetHash() {
            if (String.IsNullOrEmpty(overrideName)) {
                return method.GetHashCode();
            }
            return overrideHash;
        }

        public double GetValueD(Info inf) {
            double cnt = (count > 0) ? (double)count : 1.0;
            double res = -1.0;
            switch (inf) {
                case Info.AvgTime:
                    res = ((double)ticksTotal * PoorMansProfiler.timerBaseToMS) / cnt;
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

        public string GetInfo(Info inf) {
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

        public void WriteResults(StreamWriter sw) {
            sw.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\n", GetValueD(Info.AvgTime), GetInfo(Info.Count), GetValueD(Info.TotalTime), GetValueD(Info.MinTime), GetValueD(Info.MaxTime), GetInfo(Info.Name));
        }
    }
}
