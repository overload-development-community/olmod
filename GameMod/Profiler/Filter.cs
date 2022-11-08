using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using GameMod.Metadata;

namespace GameMod.Profiler {
    [Mod(Mods.Profiler)]
    public class PoorMansFilter {
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
        public Mode mode;
        public string typeFilter;
        public string methodFilter;
        public uint flags;

        public PoorMansFilter(Operation o, Select s, Mode m, uint f, string typeF, string methodF) {
            Set(o, s, m, f, typeF, methodF);
        }

        public PoorMansFilter(String lineDesc) {
            if (!Set(lineDesc)) {
                Set(Operation.None, Select.Contains, Mode.All, 0, null, null);
            }
        }

        public void Set(Operation o, Select s, Mode m, uint f, string typeF, string methodF) {
            op = o;
            sel = s;
            mode = m;
            flags = f;
            typeFilter = typeF;
            methodFilter = methodF;
        }

        public bool Set(string lineDesc) {
            Operation o = Operation.Include;
            Select s = Select.Contains;
            Mode m = Mode.All;
            uint f = 0;
            string typeF = null;
            string methodF;

            if (!String.IsNullOrEmpty(lineDesc)) {
                string[] parts = lineDesc.Split('\t');
                if (parts.Length < 2) {
                    methodF = lineDesc;
                } else {
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
                Set(o, s, m, f, typeF, methodF);
                return true;
            }
            return false;
        }

        private static Operation GetOp(string opts) {
            Operation o = Operation.Include;

            for (var i = 0; i < opts.Length; i++) {
                switch (opts[i]) {
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

        private static Select GetSel(string opts) {
            Select s = Select.Contains;

            for (var i = 0; i < opts.Length; i++) {
                switch (opts[i]) {
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

        private static Mode GetMode(string opts) {
            Mode m = Mode.All;

            for (var i = 0; i < opts.Length; i++) {
                switch (opts[i]) {
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

        private static uint GetFlags(string opts) {
            uint f = 0;

            for (var i = 0; i < opts.Length; i++) {
                switch (opts[i]) {
                    case '_':
                        f |= (uint)Flags.ShortTypeName;
                        break;
                }
            }
            return f;
        }

        public void Write(StreamWriter sw) {
            string o;
            string s;
            string m;
            string f = "";

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

            switch (sel) {
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

            if ((flags & (uint)Flags.ShortTypeName) != 0) {
                f += "_";
            }
            sw.Write("{0}{1}{2}\t{3}\t{4}\n", o, s, m, f, typeFilter, methodFilter);
        }

        public Operation Apply(MethodBase m, bool isPreviouslyPatched) {
            if (op == Operation.None) {
                return op;
            }

            if (mode == Mode.PreviouslyPatched && !isPreviouslyPatched) {
                return Operation.None;
            }

            string tname = ((flags & (uint)Flags.ShortTypeName) != 0) ? m.DeclaringType.Name : m.DeclaringType.FullName;
            if (Matches(tname, typeFilter) && Matches(m.ToString(), methodFilter)) {
                return op;
            }
            return Operation.None;
        }

        private bool Matches(string str, string filter) {
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
                    if (str.IndexOf(filter) >= 0) {
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
}
