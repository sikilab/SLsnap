using Microsoft.Win32;
using System.Reflection;

namespace WinScreen
{
    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private static readonly string ValueName = AppInfo.ProductName;

        public static void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    var exe = Assembly.GetExecutingAssembly().Location;
                    key.SetValue(ValueName, "\"" + exe + "\"");
                }
                else if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName);
                }
            }
        }
    }
}
