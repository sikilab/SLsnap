using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class StickerForm : Form
    {
        private readonly Bitmap _bitmap;
        private readonly string _saveDirectory;
        private readonly PictureBox _pictureBox;
        private readonly Button _closeButton;
        private readonly Button _saveButton;
        private readonly Button _copyButton;
        private float _scale = 1f;
        private Point _dragOffset;
        private AppLanguage _language;

        public StickerForm(Bitmap bitmap, string saveDirectory, Icon icon, AppLanguage language)
        {
            _bitmap = bitmap;
            _saveDirectory = saveDirectory;
            _language = language;
            Icon = icon;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            BackColor = Color.FromArgb(15, 23, 42);
            Padding = new Padding(10);

            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Padding = new Padding(6, 6, 6, 4),
                BackColor = Color.FromArgb(30, 41, 59)
            };

            _closeButton = new Button { AutoSize = true };
            _saveButton = new Button { AutoSize = true };
            _copyButton = new Button { AutoSize = true };
            _closeButton.Click += (s, e) => Close();
            _saveButton.Click += (s, e) => SaveImage();
            _copyButton.Click += (s, e) => CopyToClipboard();

            topBar.Controls.Add(_closeButton);
            topBar.Controls.Add(_saveButton);
            topBar.Controls.Add(_copyButton);

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = _bitmap,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };

            Controls.Add(_pictureBox);
            Controls.Add(topBar);

            ApplyScale();
            UpdateWindowRegion();

            MouseDown += OnDragStart;
            MouseMove += OnDragMove;
            topBar.MouseDown += OnDragStart;
            topBar.MouseMove += OnDragMove;
            _pictureBox.MouseWheel += OnMouseWheelZoom;
            _pictureBox.MouseEnter += (s, e) => _pictureBox.Focus();
            StyleActionButton(_closeButton, Color.FromArgb(239, 68, 68));
            StyleActionButton(_saveButton, Color.FromArgb(37, 99, 235));
            StyleActionButton(_copyButton, Color.FromArgb(14, 165, 233));
            RefreshLanguage(language);
        }

        public void RefreshLanguage(AppLanguage language)
        {
            _language = language;
            _closeButton.Text = Localization.Get(language, "ButtonClose");
            _saveButton.Text = Localization.Get(language, "ButtonSave");
            _copyButton.Text = Localization.Get(language, "ButtonCopy");
        }

        public void CopyToClipboard()
        {
            Clipboard.SetImage(_bitmap);
        }

        private void OnDragStart(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragOffset = e.Location;
            }
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var screenPoint = PointToScreen(e.Location);
                Location = new Point(screenPoint.X - _dragOffset.X, screenPoint.Y - _dragOffset.Y);
            }
        }

        private void OnMouseWheelZoom(object sender, MouseEventArgs e)
        {
            _scale += e.Delta > 0 ? 0.1f : -0.1f;
            if (_scale < 0.2f) _scale = 0.2f;
            if (_scale > 3f) _scale = 3f;
            ApplyScale();
        }

        private void ApplyScale()
        {
            Width = Math.Max(180, (int)(_bitmap.Width * _scale) + 20);
            Height = Math.Max(140, (int)(_bitmap.Height * _scale) + 60);
            UpdateWindowRegion();
        }

        private void StyleActionButton(Button button, Color backColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Margin = new Padding(6, 0, 0, 0);
            button.Padding = new Padding(8, 4, 8, 4);
            button.BackColor = backColor;
            button.ForeColor = Color.White;
        }

        private void SaveImage()
        {
            Directory.CreateDirectory(_saveDirectory);
            using (var dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = _saveDirectory;
                dialog.FileName = "winscreen-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
                dialog.Filter = "PNG Image (*.png)|*.png";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _bitmap.Save(dialog.FileName);
                }
            }
        }

        public string SaveToDefaultDirectory()
        {
            Directory.CreateDirectory(_saveDirectory);
            string filePath = Path.Combine(_saveDirectory, "slsnap-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            _bitmap.Save(filePath);
            return filePath;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateWindowRegion();
        }

        private void UpdateWindowRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 18))
            {
                Region = new Region(path);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            int right = bounds.Right - 1;
            int bottom = bounds.Bottom - 1;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(right - diameter, bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
