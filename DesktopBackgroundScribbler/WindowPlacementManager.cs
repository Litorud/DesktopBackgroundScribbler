using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace DesktopBackgroundScribbler
{
    public static class WindowPlacementManager
    {
        const int SW_SHOWNORMAL = 1;
        const int SW_SHOWMINIMIZED = 2;

        const string fileName = "Window.dat";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool SetWindowPlacement(IntPtr hWnd, [In]ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        public static void Save(IntPtr hWnd)
        {
            WINDOWPLACEMENT wp;
            // https://docs.microsoft.com/en-us/windows/desktop/api/winuser/ns-winuser-tagwindowplacement
            // には、GetWindowPlacement を呼び出す前にも length をセットせよと書いてある。
            wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));

            GetWindowPlacement(hWnd, out wp);

            var formatter = new BinaryFormatter();
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                formatter.Serialize(stream, wp);
            }
        }

        public static void Restore(IntPtr hWnd)
        {
            var fileInfo = new FileInfo(fileName);

            if (!fileInfo.Exists)
            {
                return;
            }

            var formatter = new BinaryFormatter();
            object settings;
            try
            {
                using (var stream = fileInfo.OpenRead())
                {
                    settings = formatter.Deserialize(stream);
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }

            if (!(settings is WINDOWPLACEMENT wp))
            {
                return;
            }

            wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            wp.flags = 0;
            if (wp.showCmd == SW_SHOWMINIMIZED)
            {
                wp.showCmd = SW_SHOWNORMAL;
            }

            SetWindowPlacement(hWnd, ref wp);
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public uint flags;
        public uint showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
        // https://docs.microsoft.com/en-us/windows/desktop/api/winuser/ns-winuser-tagwindowplacement
        // には RECT rcDevice というのも載っているが、このフィールドがあると期待通りに動作しない。
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
