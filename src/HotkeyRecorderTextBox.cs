using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace WinScreen
{
    internal sealed class HotkeyRecorderTextBox : TextBox
    {
        public HotkeyRecorderTextBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            string formatted = HotkeyParser.Format(keyData);
            if (!string.IsNullOrEmpty(formatted))
            {
                Text = formatted;
            }
            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
            base.OnKeyPress(e);
        }
    }
}
