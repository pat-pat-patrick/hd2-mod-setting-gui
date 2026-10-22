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
    }
}
