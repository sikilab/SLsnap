using System;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace WinScreen
{
    internal sealed class AppSettings
    {
        public string CaptureHotkey { get; set; }
        public string SaveDirectory { get; set; }
        public bool LaunchAtStartup { get; set; }
        public string Language { get; set; }

        public AppSettings()
        {
            CaptureHotkey = "Ctrl+Alt+A";
            SaveDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                AppInfo.ProductName);
            LaunchAtStartup = false;
            Language = GetDefaultLanguage();
        }

        public static string AppDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppInfo.ProductName);
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(AppDirectory, "settings.json"); }
        }

        public static AppSettings Load()
        {
            Directory.CreateDirectory(AppDirectory);
            Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                AppInfo.ProductName));

            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            try
            {
                var serializer = new JavaScriptSerializer();
                var value = serializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (value == null)
                {
                    return new AppSettings();
                }
                value.Normalize();
                return value;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            Normalize();
            Directory.CreateDirectory(AppDirectory);
            Directory.CreateDirectory(SaveDirectory);
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(SettingsPath, serializer.Serialize(this));
        }

        private void Normalize()
        {
            if (string.IsNullOrWhiteSpace(CaptureHotkey))
            {
                CaptureHotkey = "Ctrl+Alt+A";
            }
            if (string.IsNullOrWhiteSpace(SaveDirectory))
            {
                SaveDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    AppInfo.ProductName);
            }
            if (string.IsNullOrWhiteSpace(Language))
            {
                Language = GetDefaultLanguage();
            }
        }

        private static string GetDefaultLanguage()
        {
            var name = CultureInfo.InstalledUICulture.Name;
            if (!string.IsNullOrEmpty(name) && name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return "zh";
            }
            return "en";
        }
    }
}
