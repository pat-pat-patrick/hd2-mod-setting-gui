using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hd2ModSettingGui.Core
{
    /// <summary>Where things are, and what the window keeps between starts.</summary>
    public static class Environs
    {
        public static string GameFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arrowhead", "Helldivers2");

        /// <summary>The folder chosen with "Change folder", kept for the next start.</summary>
        public static string ConfigPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HD2 Mod Setting GUI", "config.json");

        public static Dictionary<string, object> LoadConfig()
        {
            try
            {
                return Json.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8)) as Dictionary<string, object>
                    ?? new Dictionary<string, object>();
            }
            catch (Exception) { return new Dictionary<string, object>(); }
        }

        public static void SaveConfig(Dictionary<string, object> cfg)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, Json.Write(cfg), new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
        }

        /// <summary>Whether helldivers2.exe runs (the system's list of programs, by name only).</summary>
        public static bool GameRunning()
        {
            try
            {
                var p = Process.GetProcessesByName("helldivers2");
                foreach (var x in p) x.Dispose();
                return p.Length > 0;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Chinese when Windows shows Chinese.</summary>
        public static bool UiChinese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

        // the game's own codes (seen in the file while the language was switched, 2026-09-29) -> language tags
        static readonly Dictionary<string, string> GameTags = new Dictionary<string, string>
        {
            ["cn"] = "zh-Hans", ["tc"] = "zh-Hant", ["us"] = "en", ["jp"] = "ja", ["ko"] = "ko", ["ru"] = "ru",
            ["fr"] = "fr", ["es"] = "es", ["it"] = "it", ["pl"] = "pl", ["pt"] = "pt-BR", ["ms"] = "ms", ["de"] = "de",
        };
        static readonly System.Text.RegularExpressions.Regex LanguageLine =
            new System.Text.RegularExpressions.Regex("^\\s*language\\s*=\\s*\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.Multiline);

        /// <summary>The game's text language as a language tag ("zh-Hans", "en", ...),
        /// from the newest saves\*_user_settings.config in the game's settings folder
        /// (its ".old" copies skipped; the line that starts with "language", not
        /// vo_language or platform_language). null when it cannot be read.</summary>
        public static string GameLanguageTag(string settingsFolder)
        {
            try
            {
                var saves = Path.Combine(settingsFolder, "saves");
                if (!Directory.Exists(saves)) return null;
                string newest = null;
                foreach (var f in Directory.GetFiles(saves, "*_user_settings.config"))
                    if (f.EndsWith("_user_settings.config", StringComparison.OrdinalIgnoreCase)
                        && (newest == null || File.GetLastWriteTimeUtc(f) > File.GetLastWriteTimeUtc(newest))) newest = f;
                if (newest == null) return null;
                var m = LanguageLine.Match(File.ReadAllText(newest, Encoding.UTF8));
                if (!m.Success || m.Groups[1].Value == "") return null;
                var code = m.Groups[1].Value.Trim().ToLowerInvariant();
                return GameTags.TryGetValue(code, out var tag) ? tag : code;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { return null; }
        }
    }
}
