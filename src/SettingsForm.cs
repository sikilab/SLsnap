using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class SettingsForm : Form
    {
        private readonly HotkeyRecorderTextBox _hotkeyTextBox;
        private readonly TextBox _saveDirectoryTextBox;
        private readonly CheckBox _startupCheckBox;
        private readonly AppLanguage _language;

        public AppSettings ResultSettings { get; private set; }

        public SettingsForm(AppSettings settings, AppLanguage language)
        {
            _language = language;
            Text = Localization.Get(language, "SettingsTitle") + " - " + AppInfo.ProductName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 190);

            var hotkeyLabel = new Label { Text = Localization.Get(language, "SettingsHotkey"), AutoSize = true, Location = new Point(20, 24) };
            _hotkeyTextBox = new HotkeyRecorderTextBox { Location = new Point(160, 20), Width = 240, Text = settings.CaptureHotkey };

            var dirLabel = new Label { Text = Localization.Get(language, "SettingsSaveDir"), AutoSize = true, Location = new Point(20, 66) };
            _saveDirectoryTextBox = new TextBox { Location = new Point(160, 62), Width = 190, Text = settings.SaveDirectory };
            var browseButton = new Button { Text = Localization.Get(language, "SettingsBrowse"), Location = new Point(358, 60), Size = new Size(70, 26) };
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

            _startupCheckBox = new CheckBox
            {
                Text = Localization.Get(language, "SettingsStartup"),
                AutoSize = true,
                Location = new Point(160, 102),
                Checked = settings.LaunchAtStartup
            };

            var saveButton = new Button { Text = Localization.Get(language, "ButtonSave"), Location = new Point(260, 138), Size = new Size(80, 30) };
            var cancelButton = new Button { Text = Localization.Get(language, "ButtonCancel"), Location = new Point(348, 138), Size = new Size(80, 30) };

            saveButton.Click += (s, e) =>
            {
                var hotkey = _hotkeyTextBox.Text.Trim();
                var dir = _saveDirectoryTextBox.Text.Trim();
                uint modifiers;
                uint keyCode;
                if (!HotkeyParser.TryParse(hotkey, out modifiers, out keyCode))
                {
                    MessageBox.Show(this, Localization.Get(_language, "SettingsInvalidHotkey"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(dir))
                {
                    MessageBox.Show(this, Localization.Get(_language, "SettingsDirRequired"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Directory.CreateDirectory(dir);
                ResultSettings = new AppSettings
                {
                    CaptureHotkey = hotkey,
                    SaveDirectory = dir,
                    LaunchAtStartup = _startupCheckBox.Checked,
                    Language = language == AppLanguage.Chinese ? "zh" : "en"
                };
                DialogResult = DialogResult.OK;
                Close();
            };

            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(hotkeyLabel);
            Controls.Add(_hotkeyTextBox);
            Controls.Add(dirLabel);
            Controls.Add(_saveDirectoryTextBox);
            Controls.Add(browseButton);
            Controls.Add(_startupCheckBox);
            Controls.Add(saveButton);
            Controls.Add(cancelButton);
        }
    }
}
