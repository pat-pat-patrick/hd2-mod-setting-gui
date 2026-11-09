"""Packs a release into one zip with its version in the name, for sharing it
anywhere (a cloud drive, a mod site) as a single file:

    release\\<version>\\HD2 Mod Setting GUI <version>.zip
        HD2 Mod Setting GUI <version>\\
            HD2 Mod Setting GUI.exe                  the window
            Quick Deploy plugin\\HD2QD.Settings.dll   the same window as a plugin
            Release notes <version>.txt               from Release notes <version>.md
            LICENSE.txt

Run it after build.bat and after writing release\\<version>\\Release notes
<version>.md:

    python package.py            (the version in the .csproj)
    python package.py 1.20.0

The binaries are taken from release\\<version>\\ when they are there (the ones
released), else from release\\ (and copied into release\\<version>\\). Both must
carry the version asked for, or nothing is written. Standard library only.
"""

import ctypes
import os
import re
import shutil
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
NAME = "HD2 Mod Setting GUI"
EXE = NAME + ".exe"
DLL = "HD2QD.Settings.dll"
GITHUB_EXE = "HD2.Mod.Setting.GUI.exe"   # the name GitHub gives the exe on a release page


def csproj_version():
    path = os.path.join(HERE, "src", "Hd2ModSettingGui", "Hd2ModSettingGui.csproj")
    with open(path, encoding="utf-8") as f:
        return re.search(r"<Version>([^<]+)</Version>", f.read()).group(1).strip()


def file_version(path):
    """The file's version resource (FileVersion, as major.minor.build)."""
    v = ctypes.windll.version
    size = v.GetFileVersionInfoSizeW(path, None)
    if not size:
        return None
    buf = ctypes.create_string_buffer(size)
    v.GetFileVersionInfoW(path, 0, size, buf)
    ptr, n = ctypes.c_void_p(), ctypes.c_uint()
    v.VerQueryValueW(buf, "\\", ctypes.byref(ptr), ctypes.byref(n))
    ms, ls = ctypes.cast(ptr.value + 8, ctypes.POINTER(ctypes.c_uint32 * 2)).contents
    return "%d.%d.%d" % (ms >> 16, ms & 0xFFFF, ls >> 16)


def plain(md):
    """The GitHub release notes as plain text for Notepad: no ** or `, links as
    'text (url)', and the exe called by its name in the zip."""
    s = md
    # the download lines of the release page, said of the zip's own files
    for a, b in (
        (r"Download `%s` and run it\." % re.escape(GITHUB_EXE), "Run `%s`." % EXE),
        (r"download `%s` instead and add it" % re.escape(DLL), "add `Quick Deploy plugin\\\\%s`" % DLL),
        (r"下载 `%s` 直接运行即可" % re.escape(GITHUB_EXE), "直接运行 `%s` 即可" % EXE),
        (r"改为下载 `%s`，" % re.escape(DLL), "把 `Quick Deploy plugin\\\\%s` " % DLL),
    ):
        s, n = re.subn(a, b, s)
        if n != 1:
            raise SystemExit("release notes: the download line %r was not found once" % a)
    s = s.replace(GITHUB_EXE, EXE)
    s = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"\1 (\2)", s)
    s = s.replace("**", "").replace("`", "")
    s = re.sub(r"^- ", "• ", s, flags=re.M)
    s = re.sub(r"^---$", "-" * 40, s, flags=re.M)
    return s.replace("\r\n", "\n").replace("\n", "\r\n")


def main():
    ver = sys.argv[1] if len(sys.argv) > 1 else csproj_version()
    rel = os.path.join(HERE, "release")
    out_dir = os.path.join(rel, ver)
    notes = os.path.join(out_dir, "Release notes %s.md" % ver)
    if not os.path.isfile(notes):
        raise SystemExit("missing: " + notes)
    files = {}
    for fname in (EXE, DLL):
        src = os.path.join(out_dir, fname)
        if not os.path.isfile(src):
            built = os.path.join(rel, fname)
            if not os.path.isfile(built):
                raise SystemExit("missing: " + built + " (run build.bat)")
            shutil.copy2(built, src)
        got = file_version(src)
        if got != ver:
            raise SystemExit("%s is version %s, not %s" % (src, got, ver))
        files[fname] = src

    top = "%s %s/" % (NAME, ver)
    zpath = os.path.join(out_dir, "%s %s.zip" % (NAME, ver))
    if os.path.exists(zpath):
        raise SystemExit("already there, not overwritten: " + zpath)
    with open(notes, encoding="utf-8") as f:
        text = plain(f.read())
    with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        z.write(files[EXE], top + EXE)
        z.write(files[DLL], top + "Quick Deploy plugin/" + DLL)
        z.writestr(top + "Release notes %s.txt" % ver, b"\xef\xbb\xbf" + text.encode("utf-8"))
        z.write(os.path.join(HERE, "LICENSE"), top + "LICENSE.txt")
    print(zpath)
    for i in zipfile.ZipFile(zpath).infolist():
        print("  %-60s %8d" % (i.filename, i.file_size))


if __name__ == "__main__":
    main()
