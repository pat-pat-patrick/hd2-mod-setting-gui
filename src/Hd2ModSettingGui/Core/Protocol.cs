// The files a mod and this window share (README.md, "For mod authors"):
//   <name>_options.json  the mod's list of settings, written at each launch
//   <name>_settings.txt  key=value lines, written by this window whole, in
//                        place, its last line "#end"; the mod keeps it open
//                        and reads it again each second
//   <name>.log           read at its end: "settings window: applied" when
//                        the game has taken a change
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hd2ModSettingGui.Core
{
    public enum Kind { Toggle, Choice, Slider, Color, Key, Multi, Heading, Text, Unknown }

    /// <summary>A text of the mod's: its own, as the list writes it (English
    /// unless the mod wrote it in another language), and its translations by
    /// language tag ("translations" in the list; old lists: [en, zh]). Shown in
    /// the window's language; without a translation, the mod's own text (the
    /// user, 10-24: as CowboyBingus's mods carry theirs).</summary>
    public sealed class Pair
    {
        public string En = "";
        public readonly Dictionary<string, string> Tr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The Simplified Chinese text: "zh-Hans" (or "zh", "zh-CN").</summary>
        public string Zh
        {
            get => Find(Protocol.ChineseTags) ?? "";
            set { if (string.IsNullOrEmpty(value)) Tr.Remove("zh-Hans"); else Tr["zh-Hans"] = value; }
        }

        string Find(string[] tags)
        {
            foreach (var t in tags)
                if (Tr.TryGetValue(t, out var v) && v != "") return v;
            return null;
        }

        public string In(bool zh) =>
            (zh ? Find(Protocol.ChineseTags) : Find(Protocol.EnglishTags))
            ?? (En != "" ? En : Tr.Values.FirstOrDefault(v => v != "") ?? "");

        /// <summary>Every language of it, for the search.</summary>
        public string All => En + " " + string.Join(" ", Tr.Values);

        public static Pair From(object o)
        {
            var p = new Pair();
            if (o is string one) p.En = one;
            else if (o is IList l)
            {
                if (l.Count > 0 && l[0] is string a) p.En = a;
                if (l.Count > 1 && l[1] is string b) p.Zh = b;
            }
            return p;
        }
    }

    /// <summary>One setting as the mod's list gives it.</summary>
    public sealed class OptionSpec
    {
        public string Key;
        public Kind Kind;
        public object Default;
        public double Min, Max = 1, Step;
        /// <summary>The mod asks for the peek on this setting ("peek": true): while it
        /// changes the window keeps only its control, so the player sees the game.</summary>
        public bool Peek;
        /// <summary>A space above this setting ("gap": true, Mod Options Menu's own
        /// gap): it and the rows after it in a frame of their own, as after a
        /// heading, with no title.</summary>
        public bool Gap;

        /// <summary>A setting with a value; a heading (a small group's title, the
        /// rows after it in a frame of their own) and a text (a line of the
        /// author's) have none: not written, searched, reset or undone.</summary>
        public bool HasValue => Kind != Kind.Heading && Kind != Kind.Text;
        public Pair Name = new Pair(), Hint = new Pair();
        /// <summary>A choice's names; a colour's presets (Value its #rrggbb).</summary>
        public List<(string Value, Pair Name)> Choices = new List<(string, Pair)>();

        /// <summary>A slider's decimals: as many as its step has.</summary>
        public int Digits => Step < 1 ? Math.Max(0, -(int)Math.Floor(Math.Log10(Step) + 1e-9)) : 0;

        /// <summary>A slider's value inside min..max, on its nearest step.</summary>
        public double Snap(double v)
        {
            v = Math.Max(Min, Math.Min(Max, v));
            return Math.Min(Max, Math.Round(Min + Math.Round((v - Min) / Step) * Step, Digits + 2));
        }

        public object DefaultValue
        {
            get
            {
                switch (Kind)
                {
                    case Kind.Toggle: return Default is bool b && b;
                    case Kind.Color: return Default is string s && Protocol.IsHex(s) ? s.ToLowerInvariant() : "#ffffff";
                    case Kind.Key: return Protocol.KeyName(Default as string);
                    case Kind.Multi: return Protocol.MultiValue(Default, Choices.Count);
                    default: return Protocol.ToDouble(Default) ?? 0.0;
                }
            }
        }
    }

    public sealed class Page
    {
        public Pair Name = new Pair();
        public List<string> Keys = new List<string>();
    }

    /// <summary>A mod's list of settings (its _options.json).</summary>
    public sealed class ModList
    {
        /// <summary>Title: the list's "title" as written (the same title is the same mod).</summary>
        public string Name, Title, Version, SettingsFile, Folder;
        /// <summary>The title with its translations ("title_zh" in old lists).</summary>
        public Pair TitleText = new Pair();
        public string TitleZh => TitleText.Zh == "" ? null : TitleText.Zh;

        /// <summary>The mod's name in the window's language, else its "title".</summary>
        public string TitleIn(bool zh) => TitleText.In(zh) is var t && t != "" ? t : Title;
        public List<OptionSpec> Options = new List<OptionSpec>();
        public List<Page> Pages = new List<Page>();
        public HashSet<string> MenuOwned = new HashSet<string>();
        /// <summary>"profile": the key of a choice setting that picks the mod's
        /// profile; the window shows it as a switcher at the top. null: none.</summary>
        public string ProfileKey;
        /// <summary>"profiles": true: the mod asks for the window's own profiles
        /// (the user, 10-24: only a mod that declares it gets the bar).</summary>
        public bool Profiles;

        public string SettingsPath => Path.Combine(Folder, SettingsFile);
        /// <summary>The mod's log: of <name>.log and <name>_v*.log in the folder
        /// (Wheres My Shit names its log by its version) and <name>.log in the
        /// Bingus loader's log folder (Super Beacon logs there), the newest.</summary>
        public string LogPath
        {
            get
            {
                var found = new List<string> { Path.Combine(Folder, Name + ".log") };
                try { found.AddRange(Directory.GetFiles(Folder, Name + "_v*.log")); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { }
                found.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "CowboyBingus", "Helldivers2", "Logs", Name + ".log"));
                return found.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? found[0];
            }
        }

        public static ModList Load(string path)
        {
            var file = Path.GetFileName(path);
            var name = file.EndsWith(Protocol.OptionsSuffix, StringComparison.OrdinalIgnoreCase)
                ? file.Substring(0, file.Length - Protocol.OptionsSuffix.Length) : file;
            var root = Json.Parse(File.ReadAllText(path, Encoding.UTF8)) as IDictionary<string, object>;
            if (root == null) throw new InvalidDataException("not a settings list: " + file);
            var m = new ModList
            {
                Name = name,
                Folder = Path.GetDirectoryName(path),
                Title = root.TryGetValue("title", out var t) && t is string ts && ts != "" ? ts : name,
                Version = root.TryGetValue("version", out var v) ? Convert.ToString(v, CultureInfo.InvariantCulture) : "",
                SettingsFile = root.TryGetValue("settings", out var s) && s is string ss && ss != "" ? ss : name + "_settings.txt",
            };
            m.TitleText.En = m.Title;
            if (root.TryGetValue("title_zh", out var tz) && tz is string tzs) m.TitleText.Zh = tzs;
            if (root.TryGetValue("options", out var opts) && opts is IList ol)
            {
                foreach (var o in ol.OfType<IDictionary<string, object>>())
                {
                    if (!(o.TryGetValue("key", out var k) && k is string key)) continue;
                    var spec = new OptionSpec { Key = key, Kind = Protocol.KindOf(o.TryGetValue("type", out var ty) ? ty as string : null) };
                    if (o.TryGetValue("default", out var d)) spec.Default = d;
                    if (o.TryGetValue("min", out var mn)) spec.Min = Protocol.ToDouble(mn) ?? 0;
                    if (o.TryGetValue("max", out var mx)) spec.Max = Protocol.ToDouble(mx) ?? 1;
                    spec.Step = o.TryGetValue("step", out var st) ? Protocol.ToDouble(st) ?? 0 : 0;
                    if (spec.Step <= 0) spec.Step = (spec.Max - spec.Min) / 100 > 0 ? (spec.Max - spec.Min) / 100 : 1;
                    spec.Peek = o.TryGetValue("peek", out var pk) && pk is bool pb && pb;
                    spec.Gap = o.TryGetValue("gap", out var gp) && gp is bool gb && gb;
                    var en = o.TryGetValue("en", out var e) ? e as IList : null;
                    var zh = o.TryGetValue("zh", out var z) ? z as IList : null;
                    spec.Name = new Pair { En = o.TryGetValue("name", out var nm) && nm is string nms ? nms : Item(en, 0), Zh = Item(zh, 0) };
                    spec.Hint = new Pair { En = o.TryGetValue("hint", out var ht) && ht is string hts ? hts : Item(en, 1), Zh = Item(zh, 1) };
                    if (o.TryGetValue("choices", out var ch) && ch is IList cl)
                    {
                        // a choice: its name ("Off"; old lists ["Off", "关"]); a colour: [#rrggbb, name]
                        int i = 1;
                        foreach (var c in cl)
                        {
                            if (spec.Kind == Kind.Color)
                            {
                                if (c is IList cc && cc.Count > 0 && cc[0] is string hex && Protocol.IsHex(hex))
                                    spec.Choices.Add((hex.ToLowerInvariant(), new Pair { En = Item(cc, 1), Zh = Item(cc, 2) }));
                            }
                            else if (c is string || c is IList)
                                spec.Choices.Add((i.ToString(CultureInfo.InvariantCulture), Pair.From(c)));
                            else continue;
                            i++;
                        }
                    }
                    m.Options.Add(spec);
                }
            }
            var pageAt = new Dictionary<int, Page>();      // a page by its place in the list, 1 on
            if (root.TryGetValue("pages", out var pages) && pages is IList pl)
            {
                int at = 0;
                foreach (var p in pl.OfType<IDictionary<string, object>>())
                {
                    var page = new Page
                    {
                        Name = new Pair
                        {
                            En = p.TryGetValue("name", out var pn) && pn is string pns ? pns : p.TryGetValue("en", out var pe) ? pe as string ?? "" : "",
                            Zh = p.TryGetValue("zh", out var pz) ? pz as string ?? "" : "",
                        },
                    };
                    pageAt[++at] = page;
                    if (p.TryGetValue("keys", out var ks) && ks is IList kl)
                        page.Keys.AddRange(kl.OfType<string>().Where(x => m.Options.Any(op => op.Key == x)));
                    if (page.Keys.Count > 0) m.Pages.Add(page);
                }
            }
            if (m.Pages.Count == 0)
                m.Pages.Add(new Page { Name = new Pair { En = "Settings", Zh = "设置" }, Keys = m.Options.Select(x => x.Key).ToList() });
            if (root.TryGetValue("menu_owned", out var mo) && mo is IList ml)
                foreach (var k in ml.OfType<string>()) m.MenuOwned.Add(k);
            if (root.TryGetValue("profile", out var pf) && pf is string pfk && m.Spec(pfk)?.Kind == Kind.Choice)
                m.ProfileKey = pfk;
            m.Profiles = root.TryGetValue("profiles", out var ps) && ps is bool psb && psb;
            if (root.TryGetValue("translations", out var trs) && trs is IDictionary<string, object> all)
                foreach (var lang in all)
                    if (lang.Value is IDictionary<string, object> table)
                        foreach (var kv in table)
                            if (kv.Value is string text && text.Trim() != "") m.Translate(lang.Key, kv.Key, text, pageAt);
            return m;
        }

        // "translations": { "<language tag>": { "<what>": "<text>" } }, <what> being
        // "title", "page.<n>" (the nth page), "<key>" (a setting's name),
        // "<key>.hint", "<key>.<n>" (its nth choice or colour). A key a list
        // does not have is left out.
        void Translate(string lang, string what, string text, Dictionary<int, Page> pageAt)
        {
            Pair target = null;
            int dot = what.IndexOf('.');
            string head = dot < 0 ? what : what.Substring(0, dot), tail = dot < 0 ? null : what.Substring(dot + 1);
            if (what == "title") target = TitleText;
            else if (head == "page" && tail != null && int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var pn))
                target = pageAt.TryGetValue(pn, out var pg) ? pg.Name : null;
            else if (Spec(head) is OptionSpec o)
            {
                if (tail == null) target = o.Name;
                else if (tail == "hint") target = o.Hint;
                else if (int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= o.Choices.Count)
                    target = o.Choices[n - 1].Name;
            }
            if (target != null) target.Tr[lang] = text;
        }

        static string Item(IList l, int i) => l != null && l.Count > i && l[i] is string s ? s : "";

        public OptionSpec Spec(string key) => Options.FirstOrDefault(o => o.Key == key);
    }

    public static class Protocol
    {
        public const string OptionsSuffix = "_options.json";
        /// <summary>The language tags the window looks for, in order (CowboyBingus's tags).</summary>
        public static readonly string[] ChineseTags = { "zh-Hans", "zh", "zh-CN" }, EnglishTags = { "en" };
        public const string EndMark = "#end";
        public const string AppliedMark = "settings window: applied";
        static readonly Regex Hex = new Regex("^#[0-9a-fA-F]{6}$");
        static readonly Regex Clock = new Regex("^[0-2][0-9]:[0-5][0-9]:[0-5][0-9]");

        public static bool IsHex(string s) => s != null && Hex.IsMatch(s);

        /// <summary>A multi-select's value (the user, 10-24: a choice of 8 that was
        /// every mix of 3 parts): the numbers of the choices taken, ascending, each
        /// once, joined by "," ("1,3"; "" none). Read from that text (spaces, any
        /// order, a number out of 1..count dropped) or from a list of numbers.</summary>
        public static string MultiValue(object v, int count)
        {
            IEnumerable<double?> parts;
            if (v is string s) parts = s.Split(',').Select(p => ToDouble(p.Trim()));
            else if (v is IList l) parts = l.Cast<object>().Select(ToDouble);
            else return "";
            var taken = new SortedSet<int>();
            foreach (var d in parts)
                if (d.HasValue && d.Value % 1 == 0 && d.Value >= 1 && d.Value <= count) taken.Add((int)d.Value);
            return string.Join(",", taken);
        }

        /// <summary>A key setting's value: the key's name, lower case; "" not set.</summary>
        public static string KeyName(string s) => (s ?? "").Trim().ToLowerInvariant();

        public static Kind KindOf(string t)
        {
            switch (t)
            {
                case "toggle": return Kind.Toggle;
                case "choice": return Kind.Choice;
                case "slider": return Kind.Slider;
                case "color": return Kind.Color;
                case "key": return Kind.Key;
                case "multi": return Kind.Multi;
                case "heading": return Kind.Heading;
                case "text": return Kind.Text;
                default: return Kind.Unknown;
            }
        }

        public static double? ToDouble(object o)
        {
            switch (o)
            {
                case int i: return i;
                case long l: return l;
                case double d: return d;
                case decimal m: return (double)m;
                case float f: return f;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r): return r;
                default: return null;
            }
        }

        /// <summary>Every mod's list in a folder: (name, path).</summary>
        public static List<(string Name, string Path)> FindMods(string folder)
        {
            var list = new List<(string, string)>();
            try
            {
                foreach (var f in Directory.GetFiles(folder, "*" + OptionsSuffix).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (IsStale(f)) continue;
                    var n = Path.GetFileName(f);
                    list.Add((n.Substring(0, n.Length - OptionsSuffix.Length), f));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { }
            return list;
        }

        /// <summary>A list its writer has given up ("stale": true; Boot Mod Loader 0.6.0
        /// writes {"stale": true, "options": []} for a mod no longer taking part
        /// where it cannot rename the file): not a mod. Only a small file can be one,
        /// so the lists in use are not read at every look.</summary>
        public static bool IsStale(string path)
        {
            try
            {
                if (new FileInfo(path).Length > 1024) return false;
                return Json.Parse(File.ReadAllText(path, Encoding.UTF8)) is IDictionary<string, object> root
                       && root.TryGetValue("stale", out var s) && s is bool b && b;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is ArgumentException) { return false; }
        }

        /// <summary>key -> raw text, from a key=value file (missing: empty).</summary>
        public static Dictionary<string, string> ReadValues(string path)
        {
            var d = new Dictionary<string, string>();
            try
            {
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq > 0) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            return d;
        }

        /// <summary>A number as the mod's Lua writes it: 1, 0.8, 0.05.</summary>
        public static string Number(double v)
        {
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static string Text(object v)
        {
            switch (v)
            {
                case bool b: return b ? "true" : "false";
                case double d: return Number(d);
                case int i: return Number(i);
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>The whole file in place, in one write, its last line
        /// #end. In place: the mod keeps it open while the game runs (a
        /// replace is refused then); a read caught half way finds no #end
        /// and waits for the next.</summary>
        public static void WriteValues(string path, IDictionary<string, object> values, IList<string> order)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<string>();
            foreach (var k in order.Concat(values.Keys.OrderBy(x => x, StringComparer.Ordinal)))
            {
                if (!values.ContainsKey(k) || !seen.Add(k)) continue;
                sb.Append(k).Append('=').Append(Text(values[k])).Append('\n');
            }
            sb.Append(EndMark).Append('\n');
            var bytes = new UTF8Encoding(false).GetBytes(sb.ToString());
            using (var f = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                f.Write(bytes, 0, bytes.Length);
        }

        /// <summary>A file's text as the setting's value; null when it does not fit.</summary>
        public static object Parse(OptionSpec spec, string raw)
        {
            if (raw == null || !spec.HasValue) return null;
            switch (spec.Kind)
            {
                case Kind.Toggle: return raw == "true";
                case Kind.Color: return IsHex(raw) ? raw.ToLowerInvariant() : null;
                case Kind.Key: return KeyName(raw);
                case Kind.Multi: return MultiValue(raw, spec.Choices.Count);
                default: return ToDouble(raw);
            }
        }

        /// <summary>The time ("HH:mm:ss") of the newest "applied" line in a log's last bytes; null none.</summary>
        public static string LastApplied(string logPath, int tailBytes = 16384)
        {
            try
            {
                using (var f = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, f.Length - tailBytes);
                    f.Seek(start, SeekOrigin.Begin);
                    var buf = new byte[f.Length - start];
                    int n = f.Read(buf, 0, buf.Length);
                    var lines = Encoding.UTF8.GetString(buf, 0, n).Split('\n');
                    for (int i = lines.Length - 1; i >= 0; i--)
                        if (lines[i].Contains(AppliedMark))
                            // a line without its own time: the log's last write (the game took it by then)
                            return Clock.IsMatch(lines[i]) ? lines[i].Substring(0, 8) : File.GetLastWriteTime(logPath).ToString("HH:mm:ss");
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            return null;
        }
    }
}
