using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hd2ModSettingGui.Core;

namespace Hd2ModSettingGui
{
    // (the user, 2026-11-08: "让它原生支持 HellForge。界面上有个切换"; the switch says
    // "Official" and "HellForge", shown only where HellForge is found; it follows the game
    // that runs; "插件也可以支持 HellForge，虽然现在部署不了": hosted by Quick Deploy too, the
    // library kept to the official side) The mods' folder of the side shown: the official
    // game's, or HellForge's (Core\HellForge.cs).
    public partial class MainWindow
    {
        string hfInstall;               // the installation; null: none found, or --folder given
        bool onHellForge;               // the side shown
        HellForge.Status hfStatus;      // as HellForge.Cli.exe status last said; null: not yet
        bool hfReading, hfGameWas;
        DateTime hfNext;                // the status read again no sooner (2 s; 30 s after it failed)

        void FindHellForge(bool fixedFolder)
        {
            if (fixedFolder) return;
            hfLook = true;
            var cfg = Environs.LoadConfig();
            var chosen = cfg.TryGetValue("hellforge_dir", out var d) ? d as string : null;
            hfInstall = HellForge.Find(chosen);
            if (hfInstall == null) return;
            if (hfInstall != chosen) Remember(hfInstall);
            onHellForge = cfg.TryGetValue("side", out var s) && s as string == "hellforge";
            if (onHellForge)
            {
                folder = SideFolder();
                HellForge.Prepare(folder);
            }
            ReadHellForge();
        }

        bool hfLook;                    // HellForge looked for (no --folder)
        int hfLookTick;

        // kept, so a place found once (from a HellForge program that ran) is known at the next start
        static void Remember(string install)
        {
            var cfg = Environs.LoadConfig();
            cfg["hellforge_dir"] = install;
            Environs.SaveConfig(cfg);
        }

        /// <summary>Not found at start: looked for every 10 s among the programs that run, so
        /// the switch shows once HellForge is started.</summary>
        void LookForHellForge()
        {
            if (!hfLook || hfInstall != null || ++hfLookTick % 10 != 0) return;
            var found = HellForge.FromRunning();
            if (found == null) return;
            hfInstall = found;
            Remember(found);
            ReadHellForge();
            Build();
        }

        string SideFolder() => onHellForge ? HellForge.SettingsFolder(hfInstall, hfStatus) : Environs.GameFolder;

        /// <summary>The folder whose saves\ hold the game's language for the side shown.</summary>
        string LanguageFolder() => onHellForge ? HellForge.LanguageFolder(hfInstall, hfStatus) : folder;

        /// <summary>Whether the game of the side shown runs. Both games are helldivers2.exe;
        /// while HellForge plays one, its HellForge.Session.exe runs beside it (seen 11-08). By
        /// the programs' names only: the game's process is not opened (not even for its path).
        /// follow: the side shown follows the game that runs.</summary>
        bool GameRunningHere(bool follow)
        {
            bool game = Environs.Running("helldivers2");
            if (hfInstall == null) return game;
            bool hf = game && Environs.Running("HellForge.Session");
            bool official = game && !hf;
            // HellForge's status read when its game starts or ends, then every 2 s while it does
            // not agree yet (a run in the sandbox is copied back after the game ends)
            if (hf != hfGameWas || (hf != (hfStatus?.Running == true) && DateTime.UtcNow >= hfNext)) ReadHellForge();
            hfGameWas = hf;
            if (follow && game && hf != onHellForge) SetSide(hf);
            return onHellForge ? hf : official;
        }

        async void ReadHellForge()
        {
            if (hfReading || hfInstall == null) return;
            hfReading = true;
            var install = hfInstall;
            HellForge.Status s;
            try { s = await Task.Run(() => HellForge.ReadStatus(install)); }
            finally { hfReading = false; }
            hfNext = DateTime.UtcNow + TimeSpan.FromSeconds(s == null ? 30 : 2);
            if (s == null || install != hfInstall) return;
            hfStatus = s;
            FollowFolder();
        }

        /// <summary>The window on the side's folder: made when missing (a mod's Lua cannot make
        /// it, and without it reads no settings), changes still waiting written where they were
        /// made, then everything read from the new one.</summary>
        void FollowFolder()
        {
            var want = SideFolder();
            if (onHellForge)
            {
                HellForge.Prepare(HellForge.UserFolder(hfInstall));
                HellForge.Prepare(want);
            }
            if (Protocol.SamePath(want, folder)) return;
            writeTimer.Stop();
            Write();
            folder = want;
            Load(texts: true);
        }

        void SetSide(bool hf)
        {
            if (onHellForge == hf || (hf && hfInstall == null)) return;
            onHellForge = hf;
            var cfg = Environs.LoadConfig();
            cfg["side"] = hf ? "hellforge" : "official";
            Environs.SaveConfig(cfg);
            running = GameRunningHere(follow: false);
            FollowFolder();
            if (hf) ReadHellForge();
        }

        // in the title bar, beside the window's name, where HellForge is found: which game's
        // mods are shown (the user, 11-08: in the title bar, not the mods' column)
        void PaintSide()
        {
            SideBox.Visibility = hfInstall == null ? Visibility.Collapsed : Visibility.Visible;
            if (hfInstall == null) { SideBox.Child = null; return; }
            var seg = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1 };
            foreach (var (hf, key) in new[] { (false, "side_official"), (true, "side_hellforge") })
            {
                var rb = new RadioButton { Style = S("Segment"), Content = Texts.Get(key), GroupName = "side", IsChecked = onHellForge == hf };
                rb.Checked += (s, e) => SetSide(hf);
                seg.Children.Add(rb);
            }
            SideBox.Child = seg;
        }

        // in the settings page: the installation, or that none was found, and a folder to choose
        FrameworkElement HellForgeRow()
        {
            var row = new DockPanel();
            var choose = Ghost(Texts.Get("hf_choose"), (s, e) => ChooseHellForge());
            choose.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(choose, Dock.Right);
            row.Children.Add(choose);
            row.Children.Add(new Border
            {
                Background = B("Ground700"), BorderBrush = B("Grid"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 9, 12, 9),
                Child = hfInstall == null ? (FrameworkElement)Text(Texts.Get("hf_none"), "Hint")
                    : new TextBlock { Text = hfInstall, FontFamily = (System.Windows.Media.FontFamily)Theme.Res["Mono"], FontSize = 13, TextWrapping = TextWrapping.Wrap },
            });
            return row;
        }

        void ChooseHellForge()
        {
            var d = FolderPicker.Pick(this, hfInstall, Texts.Get("hf_choose"));
            if (d == null) return;
            if (!HellForge.IsInstall(d))
            {
                Dialog.Tell(this, Texts.Get("hf_not"));
                return;
            }
            Remember(d);
            hfInstall = d;
            hfStatus = null;
            ReadHellForge();
            FollowFolder();
            Build();
        }
    }
}
