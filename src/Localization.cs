using System.Collections.Generic;

namespace WinScreen
{
    internal enum AppLanguage
    {
        English,
        Chinese
    }

    internal static class Localization
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "MenuCapture", "Capture" },
            { "MenuSettings", "Settings" },
            { "MenuAbout", "About" },
            { "MenuCloseAll", "Close All Stickers" },
            { "MenuExit", "Exit" },
            { "MenuLanguage", "Language" },
            { "LanguageEnglish", "English" },
            { "LanguageChinese", "\u7b80\u4f53\u4e2d\u6587" },
            { "CaptureHint", "Drag handles or move inside. Shift keeps ratio." },
            { "ButtonConfirm", "Confirm" },
            { "ButtonCancel", "Cancel" },
            { "ButtonClose", "Close" },
            { "ButtonSave", "Save" },
            { "ButtonCopy", "Copy" },
            { "SettingsTitle", "Settings" },
            { "SettingsBasicTitle", "Basic Settings" },
            { "SettingsSubtitle", "Configure capture shortcut, save directory, and startup options" },
            { "SettingsHotkey", "Capture Hotkey" },
            { "SettingsSaveDir", "Save Directory" },
            { "SettingsBrowse", "Browse" },
            { "SettingsStartup", "Launch at Windows startup" },
            { "SettingsEnabled", "Enabled" },
            { "SettingsLanguage", "Interface Language" },
            { "SettingsSavedNotice", "Changes take effect after saving" },
            { "SettingsInvalidHotkey", "Invalid hotkey." },
            { "SettingsHotkeyConflict", "This hotkey is already used by another app. Please choose a different one." },
            { "SettingsDirRequired", "Save directory is required." },
            { "MenuSaveAll", "Save All Stickers" },
            { "SplashBody", "SLSnap is running." },
            { "SplashTip1", "Use the tray icon or global hotkey to start capture." },
            { "SplashTip2", "Create topmost screenshot stickers for quick comparison." },
            { "AboutTip1", "Capture screenshots and create topmost desktop stickers for easier multi-content comparison." },
            { "AboutTip2", "Support copy and save directly to your default folder." },
            { "AboutTitle", "About SLSnap" },
            { "AboutProduct", "SLSnap" },
            { "AboutBody", "1. Capture screenshots and create topmost desktop stickers for easier multi-content comparison.\r\n2. Support copy and save." },
            { "AboutWebsite", "www.sikilab.com" },
            { "HotkeyRegisterFailed", "Failed to register hotkey: " }
        };

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            { "MenuCapture", "\u622a\u56fe" },
            { "MenuSettings", "\u8bbe\u7f6e" },
            { "MenuAbout", "\u5173\u4e8e" },
            { "MenuCloseAll", "\u5173\u95ed\u5168\u90e8\u8d34\u7247" },
            { "MenuExit", "\u9000\u51fa" },
            { "MenuLanguage", "\u8bed\u8a00" },
            { "LanguageEnglish", "English" },
            { "LanguageChinese", "\u7b80\u4f53\u4e2d\u6587" },
            { "CaptureHint", "\u62d6\u52a8\u89d2\u70b9/\u8fb9\u7ebf\u6216\u9009\u533a\u5185\u90e8\uff0cShift \u4fdd\u6301\u7b49\u6bd4" },
            { "ButtonConfirm", "\u786e\u5b9a" },
            { "ButtonCancel", "\u53d6\u6d88" },
            { "ButtonClose", "\u5173\u95ed" },
            { "ButtonSave", "\u4fdd\u5b58" },
            { "ButtonCopy", "\u590d\u5236" },
            { "SettingsTitle", "\u8bbe\u7f6e" },
            { "SettingsBasicTitle", "\u57fa\u672c\u8bbe\u7f6e" },
            { "SettingsSubtitle", "\u8c03\u6574\u622a\u56fe\u5feb\u6377\u952e\u3001\u4fdd\u5b58\u76ee\u5f55\u548c\u542f\u52a8\u9009\u9879" },
            { "SettingsHotkey", "\u622a\u56fe\u5feb\u6377\u952e" },
            { "SettingsSaveDir", "\u4fdd\u5b58\u76ee\u5f55" },
            { "SettingsBrowse", "\u9009\u62e9" },
            { "SettingsStartup", "\u5f00\u673a\u81ea\u52a8\u542f\u52a8" },
            { "SettingsEnabled", "\u5df2\u5f00\u542f" },
            { "SettingsLanguage", "\u754c\u9762\u8bed\u8a00" },
            { "SettingsSavedNotice", "\u8bbe\u7f6e\u4fdd\u5b58\u540e\u7acb\u5373\u751f\u6548" },
            { "SettingsInvalidHotkey", "\u5feb\u6377\u952e\u683c\u5f0f\u65e0\u6548\u3002" },
            { "SettingsHotkeyConflict", "\u8fd9\u4e2a\u5feb\u6377\u952e\u5df2\u88ab\u5176\u4ed6\u7a0b\u5e8f\u5360\u7528\uff0c\u8bf7\u6362\u4e00\u4e2a\u3002" },
            { "SettingsDirRequired", "\u5fc5\u987b\u8bbe\u7f6e\u4fdd\u5b58\u76ee\u5f55\u3002" },
            { "MenuSaveAll", "\u4fdd\u5b58\u5168\u90e8\u8d34\u7247" },
            { "SplashBody", "SLSnap \u5df2\u542f\u52a8\u3002" },
            { "SplashTip1", "\u53ef\u901a\u8fc7\u6258\u76d8\u56fe\u6807\u6216\u5168\u5c40\u5feb\u6377\u952e\u5f00\u59cb\u622a\u56fe\u3002" },
            { "SplashTip2", "\u53ef\u5feb\u901f\u751f\u6210\u684c\u9762\u7f6e\u9876\u8d34\u7247\uff0c\u4fbf\u4e8e\u591a\u5185\u5bb9\u5bf9\u6bd4\u3002" },
            { "AboutTip1", "\u622a\u56fe\u5e76\u751f\u6210\u684c\u9762\u7f6e\u9876\u8d34\u7247\uff0c\u65b9\u4fbf\u591a\u5185\u5bb9\u5bf9\u6bd4\u3002" },
            { "AboutTip2", "\u652f\u6301\u590d\u5236\u3001\u4fdd\u5b58\uff0c\u5e76\u53ef\u76f4\u63a5\u843d\u5230\u9ed8\u8ba4\u6587\u4ef6\u5939\u3002" },
            { "AboutTitle", "\u5173\u4e8e SLSnap" },
            { "AboutProduct", "SLSnap" },
            { "AboutBody", "1. \u622a\u56fe\u5e76\u751f\u6210\u684c\u9762\u7f6e\u9876\u8d34\u7247\uff0c\u65b9\u4fbf\u591a\u5185\u5bb9\u5bf9\u6bd4\uff1b\r\n2. \u652f\u6301\u590d\u5236\u3001\u4fdd\u5b58\u3002" },
            { "AboutWebsite", "www.sikilab.com" },
            { "HotkeyRegisterFailed", "\u6ce8\u518c\u5feb\u6377\u952e\u5931\u8d25\uff1a" }
        };

        public static string Get(AppLanguage language, string key)
        {
            Dictionary<string, string> source = language == AppLanguage.Chinese ? Zh : En;
            string value;
            if (source.TryGetValue(key, out value))
            {
                return value;
            }
            return key;
        }
    }
}
