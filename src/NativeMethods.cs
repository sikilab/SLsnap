using System;
using System.Runtime.InteropServices;

namespace WinScreen
{
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const int HOTKEY_ID = 0x5142;
        private const int HOTKEY_TEST_ID = 0x5143;
        private static readonly IntPtr DpiAwareContextPerMonitorAwareV2 = new IntPtr(-4);
        private static readonly IntPtr DpiAwareContextPerMonitorAware = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        public static void EnableDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(DpiAwareContextPerMonitorAwareV2))
                {
                    return;
                }

                if (SetProcessDpiAwarenessContext(DpiAwareContextPerMonitorAware))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
        }

        public static bool CanRegisterHotkey(uint modifiers, uint keyCode)
        {
            if (!RegisterHotKey(IntPtr.Zero, HOTKEY_TEST_ID, modifiers, keyCode))
            {
                return false;
            }

            UnregisterHotKey(IntPtr.Zero, HOTKEY_TEST_ID);
            return true;
        }
    }
}
