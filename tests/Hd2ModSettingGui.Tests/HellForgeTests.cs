// HellForge: its installation, its status (HellForge.Cli.exe status, as 0.4.0-beta writes
// it) and the folder the window uses on its side.
using System.IO;
using Hd2ModSettingGui.Core;
using Xunit;

namespace Hd2ModSettingGui.Tests
{
    public class HellForgeTests
    {
        const string Install = @"X:\Games\HellForge";
        const string Run = Install + @"\logs\sessions\0123456789abcdef0123456789abcdef";

        static string StatusJson(bool running, bool sandbox, string profile = "default") =>
            "{\"install\":\"X:\\\\Games\\\\HellForge\",\"gameBuild\":\"25480438\",\"hellforge\":\"0.4.0-beta (d8a8b46)\"," +
            "\"settings\":{\"profile\":\"" + profile + "\",\"war\":\"live\",\"sandbox\":" + (sandbox ? "true" : "false") + ",\"gameOptions\":\"\"}," +
            "\"session\":{\"running\":" + (running ? "true" : "false") + ",\"lastRun\":\"" + Run.Replace(@"\", @"\\") + "\",\"lastStatus\":\"observing\",\"lastFailure\":null}}";

        [Fact]
        public void The_status_fields_the_window_uses()
        {
            var s = HellForge.ParseStatus(StatusJson(true, true, "second"));
            Assert.True(s.Running);
            Assert.True(s.Sandbox);
            Assert.Equal(Run, s.LastRun);
            Assert.Equal("second", s.Profile);
            Assert.Null(HellForge.ParseStatus("[]"));
        }

        [Fact]
        public void A_run_in_the_sandbox_is_written_in_its_own_copy_and_userdata_otherwise()
        {
            string user = Install + @"\userdata\environment\Roaming\Arrowhead\Helldivers2";
            Assert.Equal(Run + @"\game\environment\Roaming\Arrowhead\Helldivers2",
                         HellForge.SettingsFolder(Install, HellForge.ParseStatus(StatusJson(true, true))));
            Assert.Equal(user, HellForge.SettingsFolder(Install, HellForge.ParseStatus(StatusJson(false, true))));   // ended: copied back
            Assert.Equal(user, HellForge.SettingsFolder(Install, HellForge.ParseStatus(StatusJson(true, false))));  // no sandbox: in place
            Assert.Equal(user, HellForge.SettingsFolder(Install, null));                                            // not read yet
        }

        [Fact]
        public void The_language_from_the_profile_or_the_run()
        {
            Assert.Equal(Install + @"\userdata\profiles\second",
                         HellForge.LanguageFolder(Install, HellForge.ParseStatus(StatusJson(false, true, "second"))));
            Assert.Equal(Run + @"\game\userdata\profiles\default",
                         HellForge.LanguageFolder(Install, HellForge.ParseStatus(StatusJson(true, true, "second"))));
        }

        [Fact]
        public void An_installation_is_a_folder_with_its_file()
        {
            using (var f = new Folder(false))
            {
                Assert.False(HellForge.IsInstall(f.Dir));
                File.WriteAllText(Path.Combine(f.Dir, "bts-install.json"), "{}");       // before 0.4
                Assert.True(HellForge.IsInstall(f.Dir));
                Assert.Equal(f.Dir, HellForge.Find(f.Dir));                               // the one chosen
                var made = Path.Combine(f.Dir, "userdata", "environment", "Roaming", "Arrowhead", "Helldivers2");
                Assert.Equal(made, HellForge.UserFolder(f.Dir));
                Assert.True(HellForge.Prepare(made));
                Assert.True(Directory.Exists(made));
                // a program that runs from it: its exe some folders down
                Assert.Equal(f.Dir, HellForge.InstallAbove(Path.Combine(f.Dir, "app", "launcher", "HellForge.Launcher.exe")));
                Assert.Equal(f.Dir, HellForge.InstallAbove(Path.Combine(f.Dir, "HellForge.exe")));
                Assert.Null(HellForge.InstallAbove(Path.Combine(Path.GetTempPath(), "elsewhere", "helldivers2.exe")));
                Assert.Null(HellForge.InstallAbove(""));
            }
            Assert.False(HellForge.IsInstall(null));
        }
    }
}
