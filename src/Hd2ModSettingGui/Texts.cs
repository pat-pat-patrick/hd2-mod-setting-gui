using System.Collections.Generic;

namespace Hd2ModSettingGui
{
    /// <summary>The window's own words, (English, Chinese).</summary>
    public static class Texts
    {
        public static bool Zh;

        static readonly Dictionary<string, (string En, string Zh)> T = new Dictionary<string, (string, string)>
        {
            ["window"] = ("HD2 Mod Setting GUI", "HD2 Mod 控制台"),
            ["title"] = ("HD2 Mod Console", "HD2 MOD 控制台"),
            ["summary"] = ("{0} mods", "{0} 个 MOD"),
            ["summary_one"] = ("1 mod", "1 个 MOD"),
            ["running"] = ("Game running · changes apply at once", "已连接游戏 · 修改即时生效"),
            ["not_running"] = ("Game not running · changes apply at the next launch", "游戏未运行 · 下次进游戏生效"),
            ["settings"] = ("Settings", "设置"),
            ["press_key"] = ("Press a key…", "按下一个键…"),
            ["no_key"] = ("Not set", "未设置"),
            ["unbind"] = ("Clear this key", "清除这个按键"),
            ["key_same"] = ("Same key as “{0}”", "与「{0}」是同一个键"),
            ["mouse_middle"] = ("Mouse middle", "鼠标中键"),
            ["mouse_side"] = ("Mouse side {0}", "鼠标侧键 {0}"),
            ["back"] = ("Back", "返回"),
            ["settings_title"] = ("Window settings", "窗口设置"),
            ["language"] = ("Language", "语言"),
            ["peek"] = ("Hide the window while changing a setting", "改设置时隐藏窗口"),
            ["peek_d"] = ("Only for the settings a mod marks for it. While one changes, the window keeps only its control, so you see the game take the change; it comes back 2.5 s after the last change or when you let go. Only while the game runs, in borderless windowed mode.",
                          "只对模组标注了需要的设置生效。改动时窗口只留下正在改的控件，可以直接看到游戏里的效果；最后一次改动 2.5 秒后，或松开滑条后恢复。只在游戏运行时生效，游戏需要用无边框窗口模式。"),
            ["overlay"] = ("Bring up over the game", "在游戏中呼出"),
            ["overlay_d"] = ("While the game runs, this key brings the window up over the game; pressing it again puts the window away and returns to the game. The window has to stay open (minimized is enough), and the game has to run in borderless windowed mode.",
                             "游戏运行时，按这个键可以把窗口呼出到游戏画面上方；再按一次收起窗口，回到游戏。本窗口需要保持打开（最小化即可），游戏需要使用无边框窗口模式。"),
            ["overlay_busy"] = ("Another program already uses this key. Choose another one.", "这个按键已被其他程序占用，请换一个。"),
            ["about"] = ("About", "关于"),
            ["reload"] = ("Refresh", "刷新"),
            ["reload_d"] = ("Read every mod's settings list and values again", "重新读取所有模组的设置清单和数值"),
            ["check_update"] = ("Check for updates", "检查更新"),
            ["checking"] = ("Checking…", "正在检查…"),
            ["up_to_date"] = ("You have the latest version ({0}).", "已是最新版本（{0}）。"),
            ["update_found"] = ("A new version is out: {0}", "有新版本：{0}"),
            ["download"] = ("Download {0}", "下载 {0}"),
            ["check_failed"] = ("Could not check: {0}", "检查失败：{0}"),
            ["auto_update"] = ("Check for updates automatically", "自动检查更新"),
            ["auto_update_d"] = ("At start and every 6 hours. It only asks GitHub for the latest version number; nothing is downloaded.",
                                 "启动时和之后每 6 小时检查一次，只向 GitHub 查询最新版本号，不会下载任何东西。"),
            ["about_d"] = ("Open source, MIT licence. Source code, releases and the notes for mod authors:", "开源，MIT 许可。源码、下载和给模组作者的说明："),
            ["minimize"] = ("Minimize", "最小化"),
            ["maximize"] = ("Maximize", "最大化"),
            ["restore"] = ("Restore", "向下还原"),
            ["close"] = ("Close", "关闭"),
            ["filter"] = ("Filter mods", "筛选 MOD"),
            ["mod_ph"] = ("Mod name", "Mod 名称"),
            ["installed"] = ("Installed mods", "已安装的 MOD"),
            // hosted by HD2 Mod Quick Deploy: where each mod stands in its library
            ["place_on"] = ("Enabled mods", "已启用的 MOD"),
            ["place_off"] = ("Mods not enabled", "未启用的 MOD"),
            ["place_unknown"] = ("Not in the mod library", "不在 MOD 库中"),
            ["place_on_mod"] = ("{0} · enabled", "{0} · 已启用"),
            ["place_off_mod"] = ("{0} · not enabled: changes apply once it is enabled and deployed", "{0} · 未启用：启用并部署后生效"),
            ["groups"] = ("Setting groups", "设置分组"),
            ["search"] = ("Search settings", "搜索设置"),
            ["results"] = ("Search results", "搜索结果"),
            ["matches"] = ("{0} matching", "{0} 项匹配"),
            ["no_match"] = ("No matching settings", "没有匹配的设置"),
            ["count"] = ("{0} settings", "{0} 项设置"),
            ["undo"] = ("Undo last change", "撤销上一步"),
            ["profiles"] = ("Profiles", "配置"),
            ["profile_n"] = ("Profile {0}", "配置 {0}"),
            ["profile_new"] = ("New profile from the current settings", "用当前设置新建配置"),
            ["profile_none"] = ("Save the current settings as a profile to switch back to them later.", "把当前设置存成配置，之后可以随时切回来。"),
            ["profile_tip"] = ("Click to use it. Drag to reorder; right-click to rename, duplicate or delete.", "点击使用；拖动排序；右键可重命名、复制、删除。"),
            ["rename"] = ("Rename", "重命名"),
            ["duplicate"] = ("Duplicate", "复制"),
            ["delete"] = ("Delete", "删除"),
            ["cancel"] = ("Cancel", "取消"),
            ["ok"] = ("OK", "好"),
            ["reset"] = ("Reset", "恢复默认"),
            ["copy_of"] = ("{0} copy", "{0} 副本"),
            ["confirm_delete"] = ("Delete the profile “{0}”? The mod's settings stay as they are.", "删除配置「{0}」？Mod 当前的设置不会改变。"),
            ["reset_group"] = ("Reset “{0}”", "恢复「{0}」组默认"),
            ["reset_mod"] = ("Reset whole mod", "恢复整个 Mod 默认"),
            ["confirm_all"] = ("Reset every setting of {0} to its default?", "把 {0} 的所有设置恢复为默认？"),
            ["reset_one"] = ("Reset “{0}” to its default", "把「{0}」恢复默认"),
            ["idle"] = ("Changes are sent to the game at once", "修改会立即同步到游戏"),
            ["applied"] = ("Applied in game · {0}", "已即时生效 · {0}"),
            ["waiting"] = ("Saved {0} · waiting for the game", "已保存 {0} · 等待游戏应用"),
            ["saved_off"] = ("Saved {0} · applies at the next launch", "已保存 {0} · 下次进游戏生效"),
            ["owned"] = ("Set in the in-game Mod Options Menu", "由游戏内 Mod Options Menu 管理"),
            ["hex_bad"] = ("Colour format: #RRGGBB, e.g. #5EE7FF", "颜色格式：#RRGGBB，例如 #5EE7FF"),
            ["pick"] = ("Pick a colour", "取色"),
            ["write_failed"] = ("The settings could not be written:\n{0}", "设置写入失败：\n{0}"),
            // the empty page (design: "空状态 · 未找到设置列表")
            ["empty_nav"] = ("Mods appear here once they hand over their settings", "mod 交来设置后会出现在这里"),
            ["tape"] = ("无信号 // NO SIGNAL // ", "无信号 // NO SIGNAL // "),
            ["empty_title"] = ("There is nothing to set up here", "这里不需要任何设置"),
            ["empty_body"] = ("No folder needs to be chosen or made. A mod that supports this window hands its settings over by itself "
                              + "when the game starts, and they appear here. None has done so yet: the game has not started with such a mod.",
                              "不需要选择或新建任何文件夹。支持本窗口的 mod 会在游戏启动时自动把设置交给本窗口，设置随后就会出现在这里。"
                              + "目前还没有：游戏还没有带着这类 mod 启动过。"),
            ["folder"] = ("Settings list folder", "设置列表所在文件夹"),
            ["copy"] = ("Copy path", "复制路径"),
            ["copied"] = ("Copied", "已复制"),
            ["open_folder"] = ("Open folder", "打开文件夹"),
            ["how"] = ("How to make settings appear", "怎么让设置出现"),
            ["s1"] = ("Install a supported mod", "安装支持的 mod"),
            ["s1d"] = ("For example Super Holo HUD", "例如 Super Holo HUD"),
            ["s2"] = ("Start the game once", "启动一次游戏"),
            ["s2d"] = ("The main menu is enough: the mod hands its settings over at launch", "进到主菜单即可，mod 会在启动时交出设置"),
            ["s3"] = ("Come back here", "回到这里"),
            ["s3d"] = ("No need to reopen this window: the settings appear by themselves", "不用重开本窗口，设置会自动出现"),
            // (the user, 11-02: players with Mod Options Menu mods found the window empty)
            ["mom_title"] = ("Mods you set in Mod Options Menu or Mod Bindings Menu", "原本在 Mod Options Menu 或 Mod Bindings Menu 里设置的 mod"),
            ["mom_body"] = ("Where these mods' settings show depends on the loader. With Boot Mod Loader they appear here once the game "
                            + "has started with it, with the values you set in Mod Options Menu; keys set in Mod Bindings Menu have to be set again. "
                            + "With another loader, such as MDL or Chef Shared Loader, they show in that loader's own in-game menu and, at present, "
                            + "not here.",
                            "这类 mod 的设置显示在哪里，取决于使用哪个启动器。使用 Boot Mod Loader 时，启动一次游戏后它们就会出现在本窗口，"
                            + "Mod Options Menu 里原来的设置值也会带过来；Mod Bindings Menu 里的按键需要重新设置。"
                            + "使用 MDL、Chef Shared Loader 等其他启动器时，它们显示在该启动器自己的游戏内菜单里，目前不会出现在本窗口。"),
            ["watching"] = ("Waiting for a mod's settings; they load here by themselves", "正在等待 mod 交来设置，出现后会自动载入"),
            ["rescanned"] = ("Checked again: nothing yet · still waiting", "已重新检查，仍未找到 · 继续等待"),
            ["rescan"] = ("Check again", "立即重新检查"),
        };

        public static string Get(string key) => T.TryGetValue(key, out var p) ? (Zh ? p.Zh : p.En) : key;
        public static string Get(string key, params object[] args) => string.Format(Get(key), args);
    }
}
