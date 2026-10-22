using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Hd2ModSettingGui
{
    /// <summary>The system's folder picker (the Explorer-style one, IFileOpenDialog
    /// with FOS_PICKFOLDERS; .NET Framework's own FolderBrowserDialog is the old tree).</summary>
    static class FolderPicker
    {
        public static string Pick(Window owner, string start, string title)
        {
            var dlg = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                dlg.GetOptions(out var opts);
                dlg.SetOptions(opts | 0x20 | 0x40 | 0x1000);           // PICKFOLDERS | FORCEFILESYSTEM | PATHMUSTEXIST
                if (title != null) dlg.SetTitle(title);
                if (start != null && SHCreateItemFromParsingName(start, IntPtr.Zero, typeof(IShellItem).GUID, out var item) == 0)
                    dlg.SetFolder(item);
                if (dlg.Show(new WindowInteropHelper(owner).Handle) != 0) return null;
                dlg.GetResult(out var result);
                result.GetDisplayName(0x80058000, out var path);         // SIGDN_FILESYSPATH
                return path;
            }
            catch (COMException) { return null; }
            finally { Marshal.ReleaseComObject(dlg); }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        class FileOpenDialogRCW { }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint c, IntPtr specs);
            void SetFileTypeIndex(uint i);
            void GetFileTypeIndex(out uint i);
            void Advise(IntPtr sink, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint fos);
            void GetOptions(out uint fos);
            void SetDefaultFolder(IShellItem si);
            void SetFolder(IShellItem si);
            void GetFolder(out IShellItem si);
            void GetCurrentSelection(out IShellItem si);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem si);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem si);
            void GetDisplayName(uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);
            void GetAttributes(uint mask, out uint attrs);
            void Compare(IShellItem si, uint hint, out int order);
        }
    }
}
