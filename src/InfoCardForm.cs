using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class InfoCardForm : Form
    {
        private readonly Timer _timer;

        public InfoCardForm(Icon icon, AppLanguage language, bool autoClose)
        {
            Text = autoClose ? AppInfo.ProductName : Localization.Get(language, "AboutTitle");
            Icon = icon;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = !autoClose;
            TopMost = autoClose;
            BackColor = Color.White;
            Font = new Font(GetUiFontName(language), 10.5f, FontStyle.Regular);

            BuildLayout(icon, language, autoClose);

            if (autoClose)
            {
                _timer = new Timer();
                _timer.Interval = 3000;
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

        private void BuildLayout(Icon icon, AppLanguage language, bool autoClose)
        {
            const int width = 640;
            const int margin = 34;
            const int accentHeight = 6;
            const int headerTop = 34;
            const int iconSize = 72;
            const int titleX = 128;
            const int textWidth = width - titleX - margin;
            const int cardPaddingX = 24;
            const int cardPaddingY = 18;
            const int cardWidth = width - margin * 2;
            const int cardTextWidth = cardWidth - cardPaddingX * 2;

            Font titleFont = new Font(GetUiFontName(language), 28f, FontStyle.Bold);
            Font subtitleFont = new Font(GetUiFontName(language), 11f, FontStyle.Regular);
            Font tipFont = new Font(GetUiFontName(language), 11f, FontStyle.Regular);
            Font linkFont = new Font("Consolas", 10.5f, FontStyle.Regular);

            string subtitleText = Localization.Get(language, "SplashBody");
            string tip1Text = "\u2022 " + Localization.Get(language, autoClose ? "SplashTip1" : "AboutTip1");
            string tip2Text = "\u2022 " + Localization.Get(language, autoClose ? "SplashTip2" : "AboutTip2");

            Size titleSize = Measure(AppInfo.ProductName, titleFont, textWidth);
            Size subtitleSize = Measure(subtitleText, subtitleFont, textWidth);
            Size tip1Size = Measure(tip1Text, tipFont, cardTextWidth);
            Size tip2Size = Measure(tip2Text, tipFont, cardTextWidth);

            int titleTop = headerTop + 4;
            int subtitleTop = titleTop + titleSize.Height + 8;
            int headerHeight = Math.Max(iconSize, subtitleTop + subtitleSize.Height - headerTop);
            int cardTop = headerTop + headerHeight + 28;
            int tip1Top = cardPaddingY;
            int tip2Top = tip1Top + tip1Size.Height + 12;
            int cardHeight = tip2Top + tip2Size.Height + cardPaddingY;
            int linkTop = cardTop + cardHeight + 24;
            int clientHeight = linkTop + 28 + margin;

            ClientSize = new Size(width, clientHeight);

            Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(37, 99, 235),
                Dock = DockStyle.Top,
                Height = accentHeight
            });

            Controls.Add(new PictureBox
            {
                Size = new Size(iconSize, iconSize),
                Location = new Point(margin, headerTop),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = icon.ToBitmap()
            });

            Controls.Add(new Label
            {
                Text = AppInfo.ProductName,
                Font = titleFont,
                AutoSize = false,
                Size = new Size(textWidth, titleSize.Height),
                Location = new Point(titleX, titleTop),
                ForeColor = Color.FromArgb(15, 23, 42)
            });

            Controls.Add(new Label
            {
                Text = subtitleText,
                Font = subtitleFont,
                AutoSize = false,
                Size = new Size(textWidth, subtitleSize.Height),
                Location = new Point(titleX + 2, subtitleTop),
                ForeColor = Color.FromArgb(71, 85, 105)
            });

            Panel card = new Panel
            {
                BackColor = Color.FromArgb(248, 250, 252),
                Location = new Point(margin, cardTop),
                Size = new Size(cardWidth, cardHeight)
            };

            card.Controls.Add(new Label
            {
                Text = tip1Text,
                Font = tipFont,
                AutoSize = false,
                Size = new Size(cardTextWidth, tip1Size.Height),
                Location = new Point(cardPaddingX, tip1Top),
                ForeColor = Color.FromArgb(30, 41, 59)
            });

            card.Controls.Add(new Label
            {
                Text = tip2Text,
                Font = tipFont,
                AutoSize = false,
                Size = new Size(cardTextWidth, tip2Size.Height),
                Location = new Point(cardPaddingX, tip2Top),
                ForeColor = Color.FromArgb(30, 41, 59)
            });
            Controls.Add(card);

            LinkLabel siteLink = new LinkLabel
            {
                Text = AppInfo.WebsiteLabel,
                Font = linkFont,
                AutoSize = true,
                Location = new Point(margin, linkTop),
                LinkColor = Color.FromArgb(37, 99, 235),
                ActiveLinkColor = Color.FromArgb(29, 78, 216),
                VisitedLinkColor = Color.FromArgb(37, 99, 235)
            };
            siteLink.LinkClicked += delegate
            {
                Process.Start(AppInfo.WebsiteUrl);
            };
            Controls.Add(siteLink);
        }

        private static Size Measure(string text, Font font, int maxWidth)
        {
            return TextRenderer.MeasureText(
                text,
                font,
                new Size(maxWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        private static string GetUiFontName(AppLanguage language)
        {
            return language == AppLanguage.Chinese ? "Microsoft YaHei UI" : "Segoe UI";
        }
    }
}
