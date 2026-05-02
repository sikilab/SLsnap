using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class SettingsForm : Form
    {
        private const int CornerRadius = 14;
        private readonly HotkeyRecorderTextBox _hotkeyTextBox;
        private readonly TextBox _saveDirectoryTextBox;
        private readonly ToggleSwitch _startupSwitch;
        private readonly SegmentedLanguageControl _languageControl;
        private readonly uint _originalModifiers;
        private readonly uint _originalKeyCode;
        private readonly bool _hasOriginalHotkey;

        public AppSettings ResultSettings { get; private set; }

        public SettingsForm(AppSettings settings, AppLanguage language)
        {
            _hasOriginalHotkey = HotkeyParser.TryParse(settings.CaptureHotkey, out _originalModifiers, out _originalKeyCode);

            Text = AppInfo.ProductName + " " + Localization.Get(language, "SettingsTitle");
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(250, 252, 255);
            ForeColor = Color.FromArgb(19, 31, 49);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(610, 510);
            Padding = new Padding(0);

            var titleBar = BuildTitleBar(language);
            Controls.Add(titleBar);

            var titleLabel = new Label
            {
                Text = Localization.Get(language, "SettingsBasicTitle"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 22F, FontStyle.Bold, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(13, 27, 45),
                Location = new Point(46, 70)
            };
            Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = Localization.Get(language, "SettingsSubtitle"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(113, 126, 146),
                Location = new Point(47, 104)
            };
            Controls.Add(subtitleLabel);

            var hotkeyLabel = BuildFieldLabel(Localization.Get(language, "SettingsHotkey"), 146);
            _hotkeyTextBox = new HotkeyRecorderTextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(247, 250, 254),
                ForeColor = Color.FromArgb(66, 82, 104),
                Font = new Font(Font.FontFamily, 15F, FontStyle.Regular, GraphicsUnit.Pixel),
                Text = settings.CaptureHotkey
            };
            Controls.Add(hotkeyLabel);
            Controls.Add(new RoundedInputPanel(_hotkeyTextBox, new Rectangle(260, 136, 298, 43)));
            Controls.Add(BuildDivider(196));

            var dirLabel = BuildFieldLabel(Localization.Get(language, "SettingsSaveDir"), 222);
            _saveDirectoryTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(247, 250, 254),
                ForeColor = Color.FromArgb(66, 82, 104),
                Font = new Font(Font.FontFamily, 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                Text = settings.SaveDirectory
            };
            var browseButton = new RoundedButton
            {
                Text = Localization.Get(language, "SettingsBrowse"),
                Bounds = new Rectangle(496, 217, 55, 31),
                BackColor = Color.FromArgb(47, 105, 235),
                HoverBackColor = Color.FromArgb(38, 92, 218),
                ForeColor = Color.White,
                CornerRadius = 8
            };
            browseButton.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.SelectedPath = _saveDirectoryTextBox.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _saveDirectoryTextBox.Text = dialog.SelectedPath;
                    }
                }
            };
            var directoryInputPanel = new RoundedInputPanel(_saveDirectoryTextBox, new Rectangle(260, 212, 298, 43));
            _saveDirectoryTextBox.Width = 220;
            Controls.Add(dirLabel);
            Controls.Add(directoryInputPanel);
            Controls.Add(browseButton);
            Controls.Add(BuildDivider(272));

            var startupLabel = BuildFieldLabel(Localization.Get(language, "SettingsStartup"), 304);
            _startupSwitch = new ToggleSwitch
            {
                Checked = settings.LaunchAtStartup,
                Location = new Point(442, 292)
            };
            var enabledLabel = new Label
            {
                Text = Localization.Get(language, "SettingsEnabled"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(66, 82, 104),
                Location = new Point(512, 303)
            };
            Controls.Add(startupLabel);
            Controls.Add(_startupSwitch);
            Controls.Add(enabledLabel);
            Controls.Add(BuildDivider(347));

            var languageLabel = BuildFieldLabel(Localization.Get(language, "SettingsLanguage"), 379);
            _languageControl = new SegmentedLanguageControl
            {
                SelectedLanguage = language,
                Location = new Point(394, 365)
            };
            var noticeLabel = new RoundedNoticeLabel
            {
                Text = Localization.Get(language, "SettingsSavedNotice"),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(43, 106, 235),
                BackColor = Color.FromArgb(234, 242, 255),
                Bounds = new Rectangle(50, 402, 318, 34)
            };
            Controls.Add(languageLabel);
            Controls.Add(_languageControl);
            Controls.Add(noticeLabel);
            Controls.Add(BuildDivider(423));

            var saveButton = new RoundedButton
            {
                Text = Localization.Get(language, "ButtonSave"),
                Bounds = new Rectangle(408, 446, 84, 36),
                BackColor = Color.FromArgb(47, 105, 235),
                HoverBackColor = Color.FromArgb(38, 92, 218),
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 15F, FontStyle.Bold, GraphicsUnit.Pixel),
                CornerRadius = 8
            };
            var cancelButton = new RoundedButton
            {
                Text = Localization.Get(language, "ButtonCancel"),
                Bounds = new Rectangle(506, 446, 66, 36),
                BackColor = Color.FromArgb(247, 67, 67),
                HoverBackColor = Color.FromArgb(226, 52, 52),
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 15F, FontStyle.Bold, GraphicsUnit.Pixel),
                CornerRadius = 8
            };
            saveButton.Click += (s, e) => SaveSettings();
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var borderPen = new Pen(Color.FromArgb(219, 228, 240)))
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius))
            {
                e.Graphics.DrawPath(borderPen, path);
            }
            base.OnPaint(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width, Height), CornerRadius))
            {
                Region = new Region(path);
            }
        }

        private Panel BuildTitleBar(AppLanguage language)
        {
            var titleBar = new Panel
            {
                BackColor = Color.FromArgb(245, 249, 253),
                Bounds = new Rectangle(0, 0, ClientSize.Width, 45)
            };
            titleBar.MouseDown += DragWindow;

            var icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            var iconBox = new PictureBox
            {
                Image = icon == null ? null : icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Bounds = new Rectangle(22, 14, 20, 20)
            };
            var title = new Label
            {
                Text = AppInfo.ProductName + " " + Localization.Get(language, "SettingsTitle"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 15F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(27, 39, 57),
                Location = new Point(54, 15)
            };
            var minimizeButton = BuildTitleButton("-", 545, () => WindowState = FormWindowState.Minimized);
            var closeButton = BuildTitleButton("x", 579, Close);

            titleBar.Controls.Add(iconBox);
            titleBar.Controls.Add(title);
            titleBar.Controls.Add(minimizeButton);
            titleBar.Controls.Add(closeButton);
            return titleBar;
        }

        private Button BuildTitleButton(string text, int x, Action action)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 249, 253),
                ForeColor = Color.FromArgb(70, 84, 104),
                Font = new Font(Font.FontFamily, 15F, FontStyle.Bold, GraphicsUnit.Pixel),
                Bounds = new Rectangle(x, 8, 28, 28),
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(231, 238, 248);
            button.Click += (s, e) => action();
            return button;
        }

        private Label BuildFieldLabel(string text, int y)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(Font.FontFamily, 15F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(20, 32, 50),
                Location = new Point(64, y)
            };
        }

        private Control BuildDivider(int y)
        {
            return new Panel
            {
                BackColor = Color.FromArgb(227, 234, 244),
                Bounds = new Rectangle(50, y, 522, 1)
            };
        }

        private void SaveSettings()
        {
            var hotkey = _hotkeyTextBox.Text.Trim();
            var dir = _saveDirectoryTextBox.Text.Trim();
            uint modifiers;
            uint keyCode;
            if (!HotkeyParser.TryParse(hotkey, out modifiers, out keyCode))
            {
                MessageBox.Show(this, Localization.Get(_languageControl.SelectedLanguage, "SettingsInvalidHotkey"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if ((!_hasOriginalHotkey || modifiers != _originalModifiers || keyCode != _originalKeyCode) &&
                !NativeMethods.CanRegisterHotkey(modifiers, keyCode))
            {
                MessageBox.Show(this, Localization.Get(_languageControl.SelectedLanguage, "SettingsHotkeyConflict"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(dir))
            {
                MessageBox.Show(this, Localization.Get(_languageControl.SelectedLanguage, "SettingsDirRequired"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(dir);
            ResultSettings = new AppSettings
            {
                CaptureHotkey = hotkey,
                SaveDirectory = dir,
                LaunchAtStartup = _startupSwitch.Checked,
                Language = _languageControl.SelectedLanguage == AppLanguage.Chinese ? "zh" : "en"
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, 0xA1, new IntPtr(0x2), IntPtr.Zero);
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

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private sealed class RoundedInputPanel : Panel
        {
            private readonly TextBox _textBox;

            public RoundedInputPanel(TextBox textBox, Rectangle bounds)
            {
                _textBox = textBox;
                Bounds = bounds;
                BackColor = Color.FromArgb(247, 250, 254);
                Padding = new Padding(16, 12, 16, 8);
                Controls.Add(_textBox);
                _textBox.Location = new Point(16, 12);
                _textBox.Width = bounds.Width - 32;
                _textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(BackColor))
                using (var pen = new Pen(Color.FromArgb(200, 211, 225)))
                using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
                base.OnPaint(e);
            }
        }

        private sealed class RoundedButton : Control, IButtonControl
        {
            private bool _hovered;

            public Color HoverBackColor { get; set; }
            public int CornerRadius { get; set; }
            public DialogResult DialogResult { get; set; }

            public RoundedButton()
            {
                Cursor = Cursors.Hand;
                CornerRadius = 8;
                HoverBackColor = Color.Empty;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hovered = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hovered = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var fill = _hovered && HoverBackColor != Color.Empty ? HoverBackColor : BackColor;
                using (var brush = new SolidBrush(fill))
                using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width, Height), CornerRadius))
                {
                    pevent.Graphics.FillPath(brush, path);
                }
                TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
                if (Parent != null)
                {
                    using (var brush = new SolidBrush(Parent.BackColor))
                    {
                        pevent.Graphics.FillRectangle(brush, ClientRectangle);
                    }
                }
            }

            public void NotifyDefault(bool value)
            {
            }

            public void PerformClick()
            {
                OnClick(EventArgs.Empty);
            }
        }

        private sealed class RoundedNoticeLabel : Label
        {
            public RoundedNoticeLabel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(BackColor))
                using (var path = CreateRoundRectPath(new Rectangle(0, 0, Width, Height), Height / 2))
                {
                    e.Graphics.FillPath(brush, path);
                }
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private sealed class ToggleSwitch : Control
        {
            private bool _checked;

            public bool Checked
            {
                get { return _checked; }
                set
                {
                    if (_checked == value)
                    {
                        return;
                    }
                    _checked = value;
                    Invalidate();
                }
            }

            public ToggleSwitch()
            {
                Cursor = Cursors.Hand;
                Size = new Size(58, 32);
                TabStop = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnClick(EventArgs e)
            {
                Checked = !Checked;
                base.OnClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var trackColor = Checked ? Color.FromArgb(47, 105, 235) : Color.FromArgb(200, 211, 225);
                using (var trackBrush = new SolidBrush(trackColor))
                using (var knobBrush = new SolidBrush(Color.White))
                using (var track = CreateRoundRectPath(new Rectangle(0, 0, Width, Height), Height / 2))
                {
                    e.Graphics.FillPath(trackBrush, track);
                    var knobX = Checked ? Width - 29 : 3;
                    e.Graphics.FillEllipse(knobBrush, knobX, 3, 26, 26);
                }
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
                if (Parent != null)
                {
                    using (var brush = new SolidBrush(Parent.BackColor))
                    {
                        pevent.Graphics.FillRectangle(brush, ClientRectangle);
                    }
                }
            }
        }

        private sealed class SegmentedLanguageControl : Control
        {
            public AppLanguage SelectedLanguage { get; set; }

            public SegmentedLanguageControl()
            {
                Size = new Size(165, 40);
                Cursor = Cursors.Hand;
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Regular, GraphicsUnit.Pixel);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                SelectedLanguage = e.X < Width / 2 ? AppLanguage.Chinese : AppLanguage.English;
                Invalidate();
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var backgroundBrush = new SolidBrush(Color.FromArgb(239, 244, 251)))
                using (var borderPen = new Pen(Color.FromArgb(200, 211, 225)))
                using (var background = CreateRoundRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                {
                    e.Graphics.FillPath(backgroundBrush, background);
                    e.Graphics.DrawPath(borderPen, background);
                }

                var selectedBounds = SelectedLanguage == AppLanguage.Chinese
                    ? new Rectangle(3, 3, 80, 34)
                    : new Rectangle(82, 3, 80, 34);
                using (var selectedBrush = new SolidBrush(Color.FromArgb(47, 105, 235)))
                using (var selected = CreateRoundRectPath(selectedBounds, 7))
                {
                    e.Graphics.FillPath(selectedBrush, selected);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    "\u4e2d\u6587",
                    Font,
                    new Rectangle(3, 0, 80, Height),
                    SelectedLanguage == AppLanguage.Chinese ? Color.White : Color.FromArgb(66, 82, 104),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(
                    e.Graphics,
                    "English",
                    Font,
                    new Rectangle(82, 0, 80, Height),
                    SelectedLanguage == AppLanguage.English ? Color.White : Color.FromArgb(66, 82, 104),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
