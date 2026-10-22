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
            ["waiting_list"] = ("Waiting for the game to write a settings list", "等待游戏写入设置列表"),
            ["lang"] = ("中文", "English"),
            ["minimize"] = ("Minimize", "最小化"),
            ["maximize"] = ("Maximize", "最大化"),
            ["restore"] = ("Restore", "向下还原"),
            ["close"] = ("Close", "关闭"),
            ["filter"] = ("Filter mods", "筛选 MOD"),
            ["mod_ph"] = ("Mod name", "Mod 名称"),
            ["installed"] = ("Installed mods", "已安装的 MOD"),
            ["n_changed"] = ("{0} changed", "{0} 项改动"),
            ["all_default"] = ("All defaults", "全部默认"),
            ["groups"] = ("Setting groups", "设置分组"),
            ["search"] = ("Search settings", "搜索设置"),
            ["results"] = ("Search results", "搜索结果"),
            ["matches"] = ("{0} matching", "{0} 项匹配"),
            ["no_match"] = ("No matching settings", "没有匹配的设置"),
            ["count"] = ("{0} settings", "{0} 项设置"),
            ["undo"] = ("Undo last change", "撤销上一步"),
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
            ["empty_nav"] = ("Mods appear here once their settings list is found", "找到设置列表后，mod 会出现在这里"),
            ["tape"] = ("无信号 // NO SIGNAL // ", "无信号 // NO SIGNAL // "),
            ["empty_title"] = ("No mod's settings found yet", "还没有找到任何 mod 的设置"),
            ["empty_body"] = ("A mod that supports this window writes its settings list into the folder below when the game starts. "
                              + "It is still empty: the game has not started with such a mod yet.",
                              "支持本窗口的 mod 会在游戏启动时，把自己的设置列表写到下面这个文件夹。这里还是空的，说明游戏还没带着这类 mod 启动过。"),
            ["folder"] = ("Settings list folder", "设置列表所在文件夹"),
            ["copy"] = ("Copy path", "复制路径"),
            ["copied"] = ("Copied", "已复制"),
            ["open_folder"] = ("Open folder", "打开文件夹"),
            ["how"] = ("How to make settings appear", "怎么让设置出现"),
            ["s1"] = ("Install a supported mod", "安装支持的 mod"),
            ["s1d"] = ("e.g. Super Holo HUD 1.127.0 or later", "例如 Super Holo HUD 1.127.0 或更高版本"),
            ["s2"] = ("Start the game once", "启动一次游戏"),
            ["s2d"] = ("The main menu is enough: the mod writes its list at launch", "进到主菜单即可，mod 会在启动时写入列表"),
            ["s3"] = ("Come back here", "回到这里"),
            ["s3d"] = ("No need to reopen this window: the list appears by itself", "不用重开本窗口，列表会自动出现"),
            ["watching"] = ("Watching this folder; new settings lists load automatically", "正在监视此文件夹，出现列表会自动载入"),
            ["rescanned"] = ("Scanned again: nothing yet · still watching", "已重新扫描，仍未找到 · 继续监视此文件夹"),
            ["change_folder"] = ("Change folder", "更改文件夹"),
            ["rescan"] = ("Scan again", "立即重新扫描"),
        };

        public static string Get(string key) => T.TryGetValue(key, out var p) ? (Zh ? p.Zh : p.En) : key;
        public static string Get(string key, params object[] args) => string.Format(Get(key), args);
    }
}
