using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hd2ModSettingGui.Core;

namespace Hd2ModSettingGui
{
    /// <summary>The window's own words, one file per language: Lang\&lt;tag&gt;.json, a flat
    /// object of keys and texts, embedded as Lang.&lt;tag&gt;.json. Adding a language is adding a
    /// file. A language is named by its tag (BCP 47: "en", "zh-CN"); a text is looked up in
    /// that language, then in its language alone ("de" for "de-AT"), then in English; a key
    /// missing there is shown as itself. A text with a count can have a key_1, the form for
    /// exactly one, in the languages that need it. (Mods' own texts are not here: they come
    /// with their lists and _translations.json, Core\Protocol.cs.)</summary>
    public static class Texts
    {
        public const string English = "en";

        static readonly Dictionary<string, Dictionary<string, string>> ByTag = Load();

        static Dictionary<string, Dictionary<string, string>> Load()
        {
            var asm = typeof(Texts).Assembly;
            var all = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("Lang.") && n.EndsWith(".json")))
                using (var r = new StreamReader(asm.GetManifestResourceStream(name)))
                    if (Json.Parse(r.ReadToEnd()) is Dictionary<string, object> d)
                        all[name.Substring(5, name.Length - 10)] = d.Where(kv => kv.Value is string)
                            .ToDictionary(kv => kv.Key, kv => (string)kv.Value);
            return all;
        }

        /// <summary>The window's language: the tag of one of the files.</summary>
        public static string Lang = English;

        /// <summary>The languages that have a file, as the window's settings list them: in the
        /// order the window had (中文, English), the others between them by tag.</summary>
        public static List<string> Languages =>
            ByTag.Keys.Where(t => t != English).OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Concat(ByTag.ContainsKey(English) ? new[] { English } : new string[0]).ToList();

        /// <summary>The file a tag is shown in: its own, else its language's alone, else English.</summary>
        public static string Resolve(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                if (ByTag.ContainsKey(tag)) return ByTag.Keys.First(k => string.Equals(k, tag, StringComparison.OrdinalIgnoreCase));
                var dash = tag.IndexOf('-');
                if (dash > 0 && ByTag.ContainsKey(tag.Substring(0, dash)))
                    return ByTag.Keys.First(k => string.Equals(k, tag.Substring(0, dash), StringComparison.OrdinalIgnoreCase));
            }
            return English;
        }

        /// <summary>The window's language at start, from --lang, the config's "lang" or Windows's
        /// language, as a file's tag: the tag's own file, else a file of the same language
        /// (zh-TW, zh-HK and the "zh" windows before 1.16.0 kept: zh-CN), else English. As
        /// HD2 Mod Quick Deploy's window chooses (its Texts.Nearest).</summary>
        public static string FromSaved(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return English;
            if (ByTag.ContainsKey(saved)) return ByTag.Keys.First(k => string.Equals(k, saved, StringComparison.OrdinalIgnoreCase));
            var language = saved.Split('-')[0];
            return ByTag.Keys.Where(k => string.Equals(k.Split('-')[0], language, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault() ?? English;
        }

        static IEnumerable<Dictionary<string, string>> Chain(string lang)
        {
            if (!string.IsNullOrEmpty(lang))
            {
                if (ByTag.TryGetValue(lang, out var exact)) yield return exact;
                var dash = lang.IndexOf('-');
                if (dash > 0 && ByTag.TryGetValue(lang.Substring(0, dash), out var language)) yield return language;
            }
            if (ByTag.TryGetValue(English, out var en)) yield return en;
        }

        /// <summary>A text in a language, or null when no file of its chain has it.</summary>
        public static string Find(string lang, string key)
        {
            foreach (var t in Chain(lang))
                if (t.TryGetValue(key, out var s)) return s;
            return null;
        }

        public static string In(string lang, string key) => Find(lang, key) ?? key;

        /// <summary>A text with arguments; a first argument of 1 (a number) picks key_1 where
        /// the language has it.</summary>
        public static string In(string lang, string key, params object[] args)
        {
            if (args.Length > 0 && IsOne(args[0]))
                foreach (var t in Chain(lang))
                {
                    if (t.TryGetValue(key + "_1", out var one)) return string.Format(one, args);
                    if (t.ContainsKey(key)) break;            // this language has no form for one
                }
            return string.Format(In(lang, key), args);
        }

        static bool IsOne(object o) =>
            o is int i ? i == 1 : o is long l ? l == 1 : o is double d && d == 1;

        /// <summary>A text in the window's language.</summary>
        public static string Get(string key) => In(Lang, key);
        public static string Get(string key, params object[] args) => In(Lang, key, args);

        /// <summary>A window text as a mod's text is kept (Core.Pair): English as its own,
        /// every language's file as a translation.</summary>
        public static Pair AsPair(string key)
        {
            var p = new Pair { En = In(English, key) };
            foreach (var tag in ByTag.Keys)
                if (tag != English && ByTag[tag].TryGetValue(key, out var s)) p.Tr[tag] = s;
            return p;
        }
    }
}
