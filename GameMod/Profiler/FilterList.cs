using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameMod.Metadata;
using UnityEngine;

namespace GameMod.Profiler {
    [Mod(Mods.Profiler)]
    public class PoorMansFilterList {
        public List<PoorMansFilter> filters = new List<PoorMansFilter>();

        public bool Load(string filename, bool warnIfNotFound = true) {
            if (File.Exists(filename)) {
                StreamReader sr = new StreamReader(filename, new System.Text.UTF8Encoding());
                string line;
                int cnt = 0;
                while ((line = sr.ReadLine()) != null) {
                    if (line[0] != '#') {
                        PoorMansFilter f = new PoorMansFilter(line);
                        if (f.op != PoorMansFilter.Operation.None) {
                            Add(f);
                            cnt++;
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

        public bool LoadStandardLocation(string filename) {
            if (Path.IsPathRooted(filename)) {
                return Load(filename);
            }
            string[] paths = new string[] { Application.persistentDataPath, Config.OLModDir };
            foreach (var path in paths) {
                string fn = Path.Combine(path, filename);
                if (Load(fn, false)) {
                    return true;
                }
            }
            UnityEngine.Debug.LogFormat("POOR MAN's PROFILER: can't find filter list file {0}", filename);
            return false;
        }

        public void Save(string filename) {
            var sw = new StreamWriter(filename, false);
            sw.Write("# POOR MAN's PROFILER Filter File v1\n");
            foreach (var f in filters) {
                f.Write(sw);
            }

            sw.Dispose();
        }

        public void Add(PoorMansFilter f) {
            filters.Add(f);
        }

        public void Add(MethodBase m, bool isPreviouslyPatched) {
            Add(new PoorMansFilter(PoorMansFilter.Operation.Include,
                                    PoorMansFilter.Select.Exact,
                                    (isPreviouslyPatched) ? PoorMansFilter.Mode.PreviouslyPatched : PoorMansFilter.Mode.All,
                                    0,
                                    m.DeclaringType.FullName,
                                    m.ToString()));
        }

        public void AddDefaults() {
            // Add all previously patched Methods
            Add(new PoorMansFilter(PoorMansFilter.Operation.Include,
                                    PoorMansFilter.Select.Always,
                                    PoorMansFilter.Mode.PreviouslyPatched,
                                    0,
                                    null,
                                    null));
        }

        public bool Apply(MethodBase m, bool isPreviouslyPatched) {
            foreach (var f in filters) {
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
}
