using System;
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
            Language = "en";
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
                return value ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(AppDirectory);
            Directory.CreateDirectory(SaveDirectory);
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(SettingsPath, serializer.Serialize(this));
        }
    }
}
