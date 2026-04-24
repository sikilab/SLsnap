using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class AboutForm : Form
    {
        public AboutForm(Icon icon, AppLanguage language)
        {
            Text = Localization.Get(language, "AboutTitle");
            Icon = icon;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(500, 360);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(24, 20, 24, 20)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = Localization.Get(language, "AboutProduct"),
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            var body = new Label
            {
                Text = Localization.Get(language, "AboutBody"),
                AutoSize = true,
                MaximumSize = new Size(440, 0),
                Margin = new Padding(0, 0, 0, 14)
            };

            var link = new LinkLabel
            {
                Text = Localization.Get(language, "AboutWebsite"),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0)
            };
            link.LinkClicked += (s, e) =>
            {
                Process.Start(AppInfo.WebsiteUrl);
            };

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(body, 0, 1);
            root.Controls.Add(link, 0, 2);
            Controls.Add(root);

            int preferredHeight = root.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
            ClientSize = new Size(ClientSize.Width, preferredHeight + root.Padding.Top + root.Padding.Bottom + 8);
        }
    }
}
