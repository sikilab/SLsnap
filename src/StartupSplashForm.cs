using System.Drawing;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class StartupSplashForm : Form
    {
        private readonly Timer _timer;

        public StartupSplashForm(Icon icon, AppLanguage language)
        {
            Text = AppInfo.ProductName;
            Icon = icon;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.White;
            ClientSize = new Size(430, 220);

            Panel accent = new Panel
            {
                BackColor = Color.FromArgb(37, 99, 235),
                Dock = DockStyle.Top,
                Height = 6
            };

            PictureBox iconBox = new PictureBox
            {
                Size = new Size(56, 56),
                Location = new Point(24, 28),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = icon.ToBitmap()
            };

            Label title = new Label
            {
                Text = AppInfo.ProductName,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(96, 26),
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            Label subtitle = new Label
            {
                Text = Localization.Get(language, "SplashBody"),
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(98, 62),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            Panel card = new Panel
            {
                BackColor = Color.FromArgb(248, 250, 252),
                Location = new Point(24, 104),
                Size = new Size(382, 88)
            };

            Label tip1 = new Label
            {
                Text = "\u2022 " + Localization.Get(language, "SplashTip1"),
                AutoSize = false,
                Size = new Size(342, 24),
                Location = new Point(18, 16),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            Label tip2 = new Label
            {
                Text = "\u2022 " + Localization.Get(language, "SplashTip2"),
                AutoSize = false,
                Size = new Size(342, 40),
                Location = new Point(18, 42),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            card.Controls.Add(tip1);
            card.Controls.Add(tip2);

            Controls.Add(accent);
            Controls.Add(iconBox);
            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(card);

            _timer = new Timer();
            _timer.Interval = 3200;
            _timer.Tick += delegate
            {
                _timer.Stop();
                if (!IsDisposed)
                {
                    Close();
                }
            };
            Shown += delegate { _timer.Start(); };
            FormClosed += delegate { _timer.Dispose(); };
        }
    }
}
