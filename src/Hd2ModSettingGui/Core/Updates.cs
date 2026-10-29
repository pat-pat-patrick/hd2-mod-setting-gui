using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Hd2ModSettingGui.Core
{
    /// <summary>A newer window (the user, 10-25): the latest release on GitHub,
    /// asked for its version number alone; nothing is downloaded. The only
    /// time the window goes online, and it can be turned off.</summary>
    public static class Updates
    {
        public const string Api = "https://api.github.com/repos/pat-pat-patrick/hd2-mod-setting-gui/releases/latest";
        public const string Page = "https://github.com/pat-pat-patrick/hd2-mod-setting-gui/releases/latest";

        /// <summary>A release's tag as a version ("v1.12.0" -> 1.12.0); null when it is none.</summary>
        public static Version ParseTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var t = tag.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
            return Version.TryParse(t, out var v) ? Norm(v) : null;
        }

        /// <summary>Major.minor.build only (the window's own version carries a revision 0).</summary>
        public static Version Norm(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build));

        public static bool IsNewer(Version latest, Version mine) => latest != null && mine != null && Norm(latest) > Norm(mine);

        /// <summary>The latest release's version and page; throws when it cannot be had.</summary>
        public static async Task<(Version Version, string Url)> Latest()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var web = new WebClient { Encoding = Encoding.UTF8 })
            {
                web.Headers[HttpRequestHeader.UserAgent] = "HD2-Mod-Setting-GUI";
                web.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                var text = await web.DownloadStringTaskAsync(new Uri(Api)).ConfigureAwait(false);
                if (!(Json.Parse(text) is IDictionary<string, object> root)) throw new FormatException("not a release");
                var v = ParseTag(root.TryGetValue("tag_name", out var t) ? t as string : null)
                        ?? throw new FormatException("no version in the release");
                var url = root.TryGetValue("html_url", out var u) && u is string us && us.StartsWith("https://github.com/") ? us : Page;
                return (v, url);
            }
        }
    }
}
