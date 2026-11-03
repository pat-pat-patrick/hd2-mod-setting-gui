// The window's own text: one file per language (src\Hd2ModSettingGui\Lang\<tag>.json).
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hd2ModSettingGui.Core;
using Xunit;

namespace Hd2ModSettingGui.Tests
{
    public class TextsTests
    {
        [Fact]
        public void A_tag_is_looked_up_exactly_then_by_its_language_then_in_English()
        {
            Assert.Equal("设置", Texts.In("zh-CN", "settings"));
            Assert.Equal("设置", Texts.In("ZH-cn", "settings"));                 // tags in any case
            Assert.Equal("Settings", Texts.In("en", "settings"));
            Assert.Equal("Settings", Texts.In("en-GB", "settings"));             // en-GB: English, by its language
            // no file for these, nor for their language alone: English
            Assert.Equal("Settings", Texts.In("zh-TW", "settings"));
            Assert.Equal("Settings", Texts.In("de", "settings"));
            Assert.Equal("Settings", Texts.In("de-AT", "settings"));
            Assert.Equal("Settings", Texts.In(null, "settings"));
            Assert.Equal("no_such_key", Texts.In("zh-CN", "no_such_key"));      // a key missing everywhere: itself

            Assert.Equal("zh-CN", Texts.Resolve("zh-CN"));
            Assert.Equal("zh-CN", Texts.Resolve("zh-cn"));
            Assert.Equal("en", Texts.Resolve("en-US"));
            Assert.Equal("en", Texts.Resolve("zh-TW"));
            Assert.Equal("en", Texts.Resolve("de"));
            Assert.Equal("en", Texts.Resolve(""));
        }

        [Fact]
        public void A_language_kept_by_an_older_window_is_read_as_a_tag()
        {
            // windows before 1.16.0 kept "zh" (Simplified Chinese) or "en" in config "lang"
            Assert.Equal("zh-CN", Texts.FromSaved("zh"));
            Assert.Equal("en", Texts.FromSaved("en"));
            Assert.Equal("zh-CN", Texts.FromSaved("zh-CN"));
            Assert.Equal("en", Texts.FromSaved(null));
        }

        [Fact]
        public void The_first_choice_takes_a_file_of_the_same_language()
        {
            // at start (Windows's language, as Quick Deploy's window): Traditional Chinese
            // Windows gets the Chinese file, as before 1.16.0; a language without a file, English
            Assert.Equal("zh-CN", Texts.FromSaved("zh-TW"));
            Assert.Equal("zh-CN", Texts.FromSaved("zh-HK"));
            Assert.Equal("en", Texts.FromSaved("en-US"));
            Assert.Equal("en", Texts.FromSaved("de-AT"));
            Assert.Equal("en", Texts.FromSaved("de"));
            // the per-text lookup is unchanged: zh-TW texts fall back to English
            Assert.Equal("Settings", Texts.In("zh-TW", "settings"));
        }

        [Fact]
        public void The_form_for_exactly_one_where_a_language_has_it()
        {
            Assert.Equal("1 mod", Texts.In("en", "summary", 1));
            Assert.Equal("3 mods", Texts.In("en", "summary", 3));
            Assert.Equal("1 个 MOD", Texts.In("zh-CN", "summary", 1));         // Chinese has one form
            Assert.Equal("3 个 MOD", Texts.In("zh-CN", "summary", 3));
            Assert.Equal("1 mod", Texts.In("de", "summary", 1));                // English's, through the fallback
            Assert.Equal("Mouse side 1", Texts.In("en", "mouse_side", 1));      // no _1 form: the text itself
        }

        [Fact]
        public void The_languages_offered_are_the_files_each_named_in_itself()
        {
            Assert.Equal(new[] { "zh-CN", "en" }, Texts.Languages);
            Assert.Equal(new[] { "中文", "English" }, Texts.Languages.Select(t => Texts.In(t, "lang_name")));
        }

        [Fact]
        public void Every_language_file_keeps_to_the_English_keys_and_placeholders()
        {
            // keycap_ words exist only where a language has its own; everything else has an English text
            var en = Table("en");
            Assert.True(en.Count > 90);
            foreach (var tag in Texts.Languages)
                foreach (var kv in Table(tag))
                {
                    if (kv.Key.StartsWith("keycap_")) continue;
                    Assert.True(en.ContainsKey(kv.Key.EndsWith("_1") ? kv.Key.Substring(0, kv.Key.Length - 2) : kv.Key), tag + ": " + kv.Key);
                    if (en.TryGetValue(kv.Key, out var e))
                        Assert.Equal(Holes(e), Holes(kv.Value));
                }
        }

        [Fact]
        public void The_page_of_a_list_without_pages_is_named_in_every_language()
        {
            var f = Path.Combine(Path.GetTempPath(), "hd2msg_texts_" + System.Guid.NewGuid().ToString("N") + "_options.json");
            try
            {
                File.WriteAllText(f, "{\"title\":\"Flat\",\"options\":[{\"key\":\"on\",\"type\":\"toggle\",\"default\":true,\"name\":\"On\"}]}");
                var m = ModList.Load(f);
                Assert.Equal(("设置", "Settings", "Settings"), (m.Pages[0].Name.In("zh-CN"), m.Pages[0].Name.In("en"), m.Pages[0].Name.In("de")));
            }
            finally { File.Delete(f); }
        }

        static System.Collections.Generic.Dictionary<string, string> Table(string tag) =>
            typeof(Texts).Assembly.GetManifestResourceNames().Contains("Lang." + tag + ".json")
                ? ((System.Collections.Generic.Dictionary<string, object>)Json.Parse(
                        new StreamReader(typeof(Texts).Assembly.GetManifestResourceStream("Lang." + tag + ".json")).ReadToEnd()))
                    .ToDictionary(kv => kv.Key, kv => (string)kv.Value)
                : null;

        static string Holes(string s) =>
            string.Join(",", Regex.Matches(s, @"\{\d+\}").Cast<Match>().Select(m => m.Value).Distinct().OrderBy(x => x));
    }
}
