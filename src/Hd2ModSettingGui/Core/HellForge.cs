// HellForge (SkyeShade/HellForge): an offline HD2 of its own, which runs the same Lua mods
// with its own APPDATA, so their lists and values are not in the game's usual folder
// (the user, 2026-11-08: "让它原生支持 HellForge。界面上有个切换"). What is read here is
// what HellForge's own docs give launchers: docs/external-launchers.md (finding an
// installation; app\cli\HellForge.Cli.exe status as JSON) and its sandbox (each run plays
// on a copy of userdata\environment, copied back when the game ends).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Hd2ModSettingGui.Core
{
    public static class HellForge
    {
        /// <summary>The file an installation's folder holds (bts-install.json before 0.4).</summary>
        public static readonly string[] Markers = { "hellforge-install.json", "bts-install.json" };

        public static bool IsInstall(string dir)
        {
            try { return !string.IsNullOrEmpty(dir) && Markers.Any(m => File.Exists(Path.Combine(dir, m))); }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException) { return false; }
        }

        /// <summary>The installation: the one chosen (when it still is one), else where the
        /// installer puts it (Documents\HellForge; %USERPROFILE%\HellForge when OneDrive has
        /// Documents), else where a HellForge.lnk on the desktop or in the Start menu points,
        /// else the one a HellForge program that runs is in. null: none found. HellForge writes nothing to the registry.</summary>
        public static string Find(string chosen)
        {
            if (IsInstall(chosen)) return chosen;
            foreach (var dir in DefaultPlaces().Concat(FromShortcuts()))
                if (IsInstall(dir)) return dir;
            return FromRunning();
        }

        /// <summary>The installation of a HellForge program that runs (its launcher or its
        /// session; never the game, whose process is not opened): the folder above its exe that
        /// holds the installation's file. null: none runs.</summary>
        public static string FromRunning()
        {
            foreach (var name in new[] { "HellForge.Launcher", "HellForge", "HellForge.Session" })
                foreach (var exe in Environs.ExesOf(name))
                {
                    var found = InstallAbove(exe);
                    if (found != null) return found;
                }
            return null;
        }

        /// <summary>The nearest folder above a file that is an installation (up to 6 up).</summary>
        public static string InstallAbove(string file)
        {
            try
            {
                var dir = string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);
                for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++, dir = Path.GetDirectoryName(dir))
                    if (IsInstall(dir)) return dir;
            }
            catch (Exception e) when (e is ArgumentException || e is PathTooLongException) { }
            return null;
        }

        static IEnumerable<string> DefaultPlaces()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home != "") yield return Path.Combine(home, "Documents", "HellForge");
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (docs != "") yield return Path.Combine(docs, "HellForge");
            if (home != "") yield return Path.Combine(home, "HellForge");
        }

        static IEnumerable<string> FromShortcuts()
        {
            var places = new[]
            {
                Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory,
                Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms,
            };
            foreach (var sf in places)
            {
                var dir = Environment.GetFolderPath(sf);
                if (dir == "") continue;
                var links = new List<string>();
                try
                {
                    links.AddRange(Directory.GetFiles(dir, "HellForge.lnk"));
                    foreach (var sub in Directory.GetDirectories(dir)) links.AddRange(Directory.GetFiles(sub, "HellForge.lnk"));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                foreach (var link in links)
                {
                    var target = LinkTarget(link);
                    if (target != null) yield return Path.GetDirectoryName(target);
                }
            }
        }

        // a shortcut's target, read by Windows's own shell object; null when it cannot be read
        static string LinkTarget(string link)
        {
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return null;
                var shell = Activator.CreateInstance(type);
                var sc = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                var target = sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) as string;
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch (Exception) { return null; }
        }

        /// <summary>What HellForge.Cli.exe status says (the fields used).</summary>
        public sealed class Status
        {
            public bool Running, Sandbox;
            /// <summary>The folder of the run going on, or the last one.</summary>
            public string LastRun;
            public string Profile = "default";
        }

        public static Status ParseStatus(string json)
        {
            if (!(Json.Parse(json) is IDictionary<string, object> root)) return null;
            var s = new Status();
            if (root.TryGetValue("session", out var se) && se is IDictionary<string, object> session)
            {
                s.Running = session.TryGetValue("running", out var r) && r is bool rb && rb;
                s.LastRun = session.TryGetValue("lastRun", out var lr) ? lr as string : null;
            }
            if (root.TryGetValue("settings", out var st) && st is IDictionary<string, object> settings)
            {
                s.Sandbox = settings.TryGetValue("sandbox", out var sb) && sb is bool sbb && sbb;
                if (settings.TryGetValue("profile", out var p) && p is string ps && ps != "") s.Profile = ps;
            }
            return s;
        }

        /// <summary>The status from the installation's own command line (read only, ~0.3 s:
        /// not on the window's thread); null when it cannot be had.</summary>
        public static Status ReadStatus(string install)
        {
            var exe = Path.Combine(install, "app", "cli", "HellForge.Cli.exe");
            if (!File.Exists(exe)) return null;
            try
            {
                var psi = new ProcessStartInfo(exe, "status")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = install,
                };
                using (var p = Process.Start(psi))
                {
                    var read = p.StandardOutput.ReadToEndAsync();
                    if (!p.WaitForExit(10000)) { try { p.Kill(); } catch (Exception) { } return null; }
                    return ParseStatus(read.Result);
                }
            }
            catch (Exception) { return null; }
        }

        const string Settings = @"Arrowhead\Helldivers2";

        /// <summary>The mods' folder when no run uses a copy of it: the game's APPDATA is
        /// userdata\environment\Roaming.</summary>
        public static string UserFolder(string install) =>
            Path.Combine(install, "userdata", "environment", "Roaming", Settings);

        /// <summary>The folder of a run in the sandbox: its copy of userdata\environment.</summary>
        public static string RunFolder(string lastRun) =>
            Path.Combine(lastRun, "game", "environment", "Roaming", Settings);

        /// <summary>The folder the window reads and writes. While a run in the sandbox goes on,
        /// that run's copy: the game reads it now, and it is copied over userdata's when the
        /// game ends (a change written to userdata's meanwhile would be lost).</summary>
        public static string SettingsFolder(string install, Status s) =>
            InRun(s) ? RunFolder(s.LastRun) : UserFolder(install);

        /// <summary>The folder that holds saves\*_user_settings.config (the game's language).</summary>
        public static string LanguageFolder(string install, Status s) =>
            InRun(s) ? Path.Combine(s.LastRun, "game", "userdata", "profiles", "default")
                     : Path.Combine(install, "userdata", "profiles", s?.Profile ?? "default");

        static bool InRun(Status s) => s != null && s.Running && s.Sandbox && !string.IsNullOrEmpty(s.LastRun);

        /// <summary>The folder made when it is missing: a mod's Lua cannot make folders, and
        /// without this one it reads no settings and writes no log. false: it could not be.</summary>
        public static bool Prepare(string folder)
        {
            try { Directory.CreateDirectory(folder); return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException) { return false; }
        }
    }
}
