using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private readonly InfoCardForm _startupSplash;
        private SlsnapContextMenuStrip _activeTrayMenu;
        private DateTime _activeTrayMenuOpenedAt;
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
            _notifyIcon.MouseClick += OnNotifyIconMouseClick;
            _notifyIcon.DoubleClick += (s, e) => StartCapture();

            _messageWindow = new MessageWindow(this);
            RegisterHotkeyOrShowWarning();

            _startupSplash = new InfoCardForm(_icon, _language, true);
            _startupSplash.Show();
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new SlsnapContextMenuStrip();
            menu.Opened += (s, e) =>
            {
                _activeTrayMenu = menu;
                _activeTrayMenuOpenedAt = DateTime.UtcNow;
            };
            menu.Closed += (s, e) =>
            {
                if (ReferenceEquals(_activeTrayMenu, menu))
                {
                    _activeTrayMenu = null;
                }
            };
            menu.Items.Add(new SlsnapMenuHeader(AppInfo.ProductName));
            menu.Items.Add(new SlsnapMenuSeparator());
            menu.Items.Add(new SlsnapMenuItem("A", Localization.Get(_language, "MenuCapture"), _settings.CaptureHotkey, BuildMenuAction(StartCapture)));
            menu.Items.Add(new SlsnapMenuItem("B", Localization.Get(_language, "MenuSettings"), string.Empty, BuildMenuAction(OpenSettings)));
            menu.Items.Add(new SlsnapMenuSeparator());
            menu.Items.Add(new SlsnapMenuItem("C", Localization.Get(_language, "MenuCloseAll"), string.Empty, BuildMenuAction(CloseAllStickers)));
            menu.Items.Add(new SlsnapMenuItem("D", Localization.Get(_language, "MenuSaveAll"), string.Empty, BuildMenuAction(SaveAllStickers)));
            menu.Items.Add(new SlsnapMenuItem("E", Localization.Get(_language, "MenuAbout"), string.Empty, BuildMenuAction(OpenAbout)));
            menu.Items.Add(new SlsnapMenuSeparator());
            menu.Items.Add(new SlsnapMenuItem("F", Localization.Get(_language, "MenuExit"), string.Empty, BuildMenuAction(ExitThread)));
            return menu;
        }

        private EventHandler BuildMenuAction(Action action)
        {
            return (s, e) =>
            {
                CloseActiveTrayMenu();
                action();
            };
        }

        private void OnNotifyIconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _activeTrayMenu != null && _activeTrayMenu.Visible)
            {
                if ((DateTime.UtcNow - _activeTrayMenuOpenedAt).TotalMilliseconds < 350)
                {
                    return;
                }
                CloseActiveTrayMenu();
            }
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
                foreach (var screen in Screen.AllScreens)
                {
                    var source = screen.Bounds.Location;
                    var destination = new Point(screen.Bounds.X - bounds.X, screen.Bounds.Y - bounds.Y);
                    graphics.CopyFromScreen(source, destination, screen.Bounds.Size);
                }
            }
            CloseActiveTrayMenu();
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
            sticker.CopyToClipboard();
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
            using (var form = new InfoCardForm(_icon, _language, false))
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

        private void SaveAllStickers()
        {
            foreach (var sticker in _stickers.ToArray())
            {
                sticker.SaveToDefaultDirectory();
            }
        }

        private void CloseActiveTrayMenu()
        {
            if (_activeTrayMenu != null && !_activeTrayMenu.IsDisposed)
            {
                _activeTrayMenu.Close(ToolStripDropDownCloseReason.CloseCalled);
            }
            _activeTrayMenu = null;
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

        private sealed class SlsnapContextMenuStrip : ContextMenuStrip
        {
            public const int MenuWidth = 320;
            public const int MenuContentMargin = 20;
            public const int ItemInnerPadding = 16;
            private OutsideClickFilter _outsideClickFilter;

            public SlsnapContextMenuStrip()
            {
                AutoSize = true;
                BackColor = Color.White;
                ForeColor = Color.FromArgb(20, 31, 48);
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
                Padding = new Padding(14, 24, 14, 24);
                ShowCheckMargin = false;
                ShowImageMargin = false;
                AutoClose = true;
                Renderer = new ToolStripProfessionalRenderer(new SlsnapMenuColorTable());
            }

            public override Rectangle DisplayRectangle
            {
                get
                {
                    return new Rectangle(
                        Padding.Left, Padding.Top,
                        Math.Max(0, Width - Padding.Horizontal),
                        Math.Max(0, Height - Padding.Vertical));
                }
            }

            protected override void OnOpened(EventArgs e)
            {
                base.OnOpened(e);
                var availableWidth = DisplayRectangle.Width;
                foreach (ToolStripItem item in Items)
                {
                    item.Width = availableWidth;
                }
                _outsideClickFilter = new OutsideClickFilter(this);
                Application.AddMessageFilter(_outsideClickFilter);
            }

            protected override void OnClosing(ToolStripDropDownClosingEventArgs e)
            {
                if (e.CloseReason != ToolStripDropDownCloseReason.CloseCalled &&
                    Bounds.Contains(Control.MousePosition))
                {
                    e.Cancel = true;
                    return;
                }

                base.OnClosing(e);
            }

            protected override void OnClosed(ToolStripDropDownClosedEventArgs e)
            {
                if (_outsideClickFilter != null)
                {
                    Application.RemoveMessageFilter(_outsideClickFilter);
                    _outsideClickFilter = null;
                }
                base.OnClosed(e);
            }

            protected override void OnVisibleChanged(EventArgs e)
            {
                base.OnVisibleChanged(e);
                UpdateRoundedRegion();
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                UpdateRoundedRegion();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var borderPen = new Pen(Color.FromArgb(221, 228, 238)))
                using (var path = CreateRoundRectPath(new Rectangle(1, 1, Width - 2, Height - 2), 10))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }
            }

            private void UpdateRoundedRegion()
            {
                if (Width <= 0 || Height <= 0)
                {
                    return;
                }

                using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width, Height), 10))
                {
                    Region = new Region(path);
                }
            }

            private sealed class OutsideClickFilter : IMessageFilter
            {
                private const int WM_KEYDOWN = 0x0100;
                private readonly SlsnapContextMenuStrip _menu;

                public OutsideClickFilter(SlsnapContextMenuStrip menu)
                {
                    _menu = menu;
                }

                public bool PreFilterMessage(ref Message m)
                {
                    if (m.Msg == WM_KEYDOWN && ((Keys)m.WParam.ToInt32()) == Keys.Escape)
                    {
                        _menu.Close(ToolStripDropDownCloseReason.CloseCalled);
                        return true;
                    }
                    return false;
                }
            }
        }

        private sealed class SlsnapMenuHeader : ToolStripItem
        {
            public SlsnapMenuHeader(string text)
            {
                Text = text;
                AutoSize = false;
                Size = new Size(SlsnapContextMenuStrip.MenuWidth, 38);
            }

            public override Size GetPreferredSize(Size constrainingSize)
            {
                return Size;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);
                var menuWidth = Owner != null ? Owner.Width : Width;
                var left = SlsnapContextMenuStrip.MenuContentMargin - Bounds.Left + SlsnapContextMenuStrip.ItemInnerPadding;
                var right = menuWidth - SlsnapContextMenuStrip.MenuContentMargin - Bounds.Left - SlsnapContextMenuStrip.ItemInnerPadding;
                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    new Font("Microsoft YaHei UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
                    new Rectangle(left, 0, right - left, Height),
                    Color.FromArgb(20, 31, 48),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        private sealed class SlsnapMenuSeparator : ToolStripItem
        {
            public SlsnapMenuSeparator()
            {
                AutoSize = false;
                Size = new Size(SlsnapContextMenuStrip.MenuWidth, 14);
            }

            public override Size GetPreferredSize(Size constrainingSize)
            {
                return Size;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var menuWidth = Owner != null ? Owner.Width : Width;
                var left = SlsnapContextMenuStrip.MenuContentMargin - Bounds.Left + SlsnapContextMenuStrip.ItemInnerPadding;
                var right = menuWidth - SlsnapContextMenuStrip.MenuContentMargin - Bounds.Left - SlsnapContextMenuStrip.ItemInnerPadding;
                using (var pen = new Pen(Color.FromArgb(226, 232, 242)))
                {
                    e.Graphics.DrawLine(pen, left, Height / 2, right, Height / 2);
                }
            }
        }

        private sealed class SlsnapMenuItem : ToolStripMenuItem
        {
            private readonly string _badge;
            private readonly string _rightText;

            public SlsnapMenuItem(string badge, string text, string rightText, EventHandler onClick)
                : base(text)
            {
                _badge = badge;
                _rightText = rightText;
                AutoSize = false;
                Size = new Size(SlsnapContextMenuStrip.MenuWidth, 39);
                Padding = Padding.Empty;
                Margin = Padding.Empty;
                if (onClick != null)
                {
                    Click += onClick;
                }
            }

            public override Size GetPreferredSize(Size constrainingSize)
            {
                return Size;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);

                var menuWidth = Owner != null ? Owner.Width : Width;
                var itemX = Bounds.Left;
                var highlightLeft = SlsnapContextMenuStrip.MenuContentMargin - itemX;
                var highlightRight = menuWidth - SlsnapContextMenuStrip.MenuContentMargin - itemX;
                var innerLeft = highlightLeft + SlsnapContextMenuStrip.ItemInnerPadding;
                var innerRight = highlightRight - SlsnapContextMenuStrip.ItemInnerPadding;

                if (Selected)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(232, 240, 255)))
                    using (var path = CreateRoundRectPath(new Rectangle(innerLeft, 3, innerRight - innerLeft, Height - 6), 8))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }

                if (!string.IsNullOrEmpty(_badge))
                {
                    var badgeDiameter = 18;
                    var badgeX = innerLeft;
                    var badgeY = (Height - badgeDiameter) / 2;
                    using (var badgeBrush = new SolidBrush(Color.FromArgb(48, 106, 235)))
                    {
                        e.Graphics.FillEllipse(badgeBrush, badgeX, badgeY, badgeDiameter, badgeDiameter);
                    }
                    using (var badgeFont = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold))
                    using (var sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        e.Graphics.DrawString(_badge, badgeFont, Brushes.White,
                            new RectangleF(badgeX, badgeY, badgeDiameter, badgeDiameter), sf);
                    }
                }

                var textLeft = string.IsNullOrEmpty(_badge)
                    ? innerLeft
                    : innerLeft + 30;
                var hasRightText = !string.IsNullOrEmpty(_rightText);
                var rightColumnWidth = 106;
                var rightColumnLeft = hasRightText ? innerRight - rightColumnWidth : innerRight;
                var textRightGap = 8;
                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                    new Rectangle(textLeft, 0, rightColumnLeft - textLeft - textRightGap, Height),
                    Color.FromArgb(20, 31, 48),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (hasRightText)
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        _rightText,
                        new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                        new Rectangle(rightColumnLeft, 0, rightColumnWidth, Height),
                        Color.FromArgb(112, 127, 148),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private sealed class SlsnapMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground
            {
                get { return Color.White; }
            }

            public override Color ImageMarginGradientBegin
            {
                get { return Color.White; }
            }

            public override Color ImageMarginGradientMiddle
            {
                get { return Color.White; }
            }

            public override Color ImageMarginGradientEnd
            {
                get { return Color.White; }
            }

            public override Color MenuBorder
            {
                get { return Color.Transparent; }
            }
        }

        private static GraphicsPath CreateRoundRectPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
