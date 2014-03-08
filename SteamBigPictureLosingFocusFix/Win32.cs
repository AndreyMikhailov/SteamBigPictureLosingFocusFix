using System;
using System.Runtime.InteropServices;

namespace SteamBigPictureLosingFocusFix
{
    internal static class Win32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    }
}
