using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly AppSettings _settings;
        private readonly CaptureOverlayForm _overlay;
        private readonly List<StickerForm> _stickers = new List<StickerForm>();
        private readonly Icon _icon;
        private readonly MessageWindow _messageWindow;
        private readonly StartupSplashForm _startupSplash;
        private AppLanguage _language;

        public TrayApplicationContext()
        {
            _icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            _settings = AppSettings.Load();
            _language = ParseLanguage(_settings.Language);
            StartupManager.Apply(_settings.LaunchAtStartup);

            _overlay = new CaptureOverlayForm();
            _overlay.CaptureConfirmed += OnCaptureConfirmed;

            _notifyIcon = new NotifyIcon
            {
                Icon = _icon,
                Visible = true,
                Text = AppInfo.ProductName,
                ContextMenuStrip = BuildMenu()
            };
            _notifyIcon.DoubleClick += (s, e) => StartCapture();

            _messageWindow = new MessageWindow(this);
            RegisterHotkeyOrShowWarning();

            _startupSplash = new StartupSplashForm(_icon, _language);
            _startupSplash.Show();
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(Localization.Get(_language, "MenuCapture"), null, (s, e) => StartCapture());
            menu.Items.Add(Localization.Get(_language, "MenuSettings"), null, (s, e) => OpenSettings());
            var languageMenu = new ToolStripMenuItem(Localization.Get(_language, "MenuLanguage"));
            var englishItem = new ToolStripMenuItem(Localization.Get(_language, "LanguageEnglish"));
            var chineseItem = new ToolStripMenuItem(Localization.Get(_language, "LanguageChinese"));
            englishItem.Checked = _language == AppLanguage.English;
            chineseItem.Checked = _language == AppLanguage.Chinese;
            englishItem.Click += (s, e) => ChangeLanguage(AppLanguage.English);
            chineseItem.Click += (s, e) => ChangeLanguage(AppLanguage.Chinese);
            languageMenu.DropDownItems.Add(englishItem);
            languageMenu.DropDownItems.Add(chineseItem);
            menu.Items.Add(languageMenu);
            menu.Items.Add(Localization.Get(_language, "MenuAbout"), null, (s, e) => OpenAbout());
            menu.Items.Add(Localization.Get(_language, "MenuCloseAll"), null, (s, e) => CloseAllStickers());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localization.Get(_language, "MenuExit"), null, (s, e) => ExitThread());
            return menu;
        }

        private void RegisterHotkeyOrShowWarning()
        {
            uint modifiers;
            uint keyCode;
            if (!HotkeyParser.TryParse(_settings.CaptureHotkey, out modifiers, out keyCode) ||
                !NativeMethods.RegisterHotKey(_messageWindow.Handle, NativeMethods.HOTKEY_ID, modifiers, keyCode))
            {
                MessageBox.Show(
                    Localization.Get(_language, "HotkeyRegisterFailed") + _settings.CaptureHotkey,
                    AppInfo.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        public void StartCapture()
        {
            var bounds = SystemInformation.VirtualScreen;
            var snapshot = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(snapshot))
            {
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            _overlay.BeginCapture(snapshot, bounds, _language);
        }

        public void OnHotkeyPressed()
        {
            StartCapture();
        }

        private void OnCaptureConfirmed(object sender, Bitmap bitmap)
        {
            var sticker = new StickerForm(bitmap, _settings.SaveDirectory, _icon, _language);
            sticker.StartPosition = FormStartPosition.Manual;
            var cursor = Cursor.Position;
            sticker.Location = new Point(cursor.X + 20, cursor.Y + 20);
            sticker.FormClosed += (s, e) => _stickers.Remove(sticker);
            _stickers.Add(sticker);
            sticker.Show();
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_settings, _language))
            {
                if (form.ShowDialog() == DialogResult.OK && form.ResultSettings != null)
                {
                    NativeMethods.UnregisterHotKey(_messageWindow.Handle, NativeMethods.HOTKEY_ID);
                    _settings.CaptureHotkey = form.ResultSettings.CaptureHotkey;
                    _settings.SaveDirectory = form.ResultSettings.SaveDirectory;
                    _settings.LaunchAtStartup = form.ResultSettings.LaunchAtStartup;
                    _settings.Language = form.ResultSettings.Language;
                    _language = ParseLanguage(_settings.Language);
                    _settings.Save();
                    StartupManager.Apply(_settings.LaunchAtStartup);
                    _notifyIcon.ContextMenuStrip = BuildMenu();
                    _overlay.RefreshLanguage(_language);
                    RefreshStickerLanguage();
                    RegisterHotkeyOrShowWarning();
                }
            }
        }

        private void OpenAbout()
        {
            using (var form = new AboutForm(_icon, _language))
            {
                form.ShowDialog();
            }
        }

        private void ChangeLanguage(AppLanguage language)
        {
            if (_language == language)
            {
                return;
            }

            _language = language;
            _settings.Language = language == AppLanguage.Chinese ? "zh" : "en";
            _settings.Save();
            _notifyIcon.ContextMenuStrip = BuildMenu();
            _overlay.RefreshLanguage(_language);
            RefreshStickerLanguage();
        }

        private void RefreshStickerLanguage()
        {
            foreach (var sticker in _stickers)
            {
                sticker.RefreshLanguage(_language);
            }
        }

        private static AppLanguage ParseLanguage(string value)
        {
            if (!string.IsNullOrEmpty(value) && value.ToLowerInvariant().StartsWith("zh"))
            {
                return AppLanguage.Chinese;
            }
            return AppLanguage.English;
        }

        private void CloseAllStickers()
        {
            foreach (var sticker in _stickers.ToArray())
            {
                sticker.Close();
            }
        }

        protected override void ExitThreadCore()
        {
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, NativeMethods.HOTKEY_ID);
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_startupSplash != null && !_startupSplash.IsDisposed)
            {
                _startupSplash.Close();
                _startupSplash.Dispose();
            }
            _overlay.Dispose();
            _messageWindow.DestroyHandle();
            base.ExitThreadCore();
        }

        private sealed class MessageWindow : NativeWindow
        {
            private readonly TrayApplicationContext _owner;

            public MessageWindow(TrayApplicationContext owner)
            {
                _owner = owner;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == NativeMethods.HOTKEY_ID)
                {
                    _owner.OnHotkeyPressed();
                }
                base.WndProc(ref m);
            }
        }
    }
}
