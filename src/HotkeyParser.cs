using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WinScreen
{
    internal static class HotkeyParser
    {
        public static string Format(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.ControlKey || keyCode == Keys.Menu || keyCode == Keys.ShiftKey)
            {
                return null;
            }

            List<string> parts = new List<string>();
            if ((keyData & Keys.Control) == Keys.Control)
            {
                parts.Add("Ctrl");
            }
            if ((keyData & Keys.Alt) == Keys.Alt)
            {
                parts.Add("Alt");
            }
            if ((keyData & Keys.Shift) == Keys.Shift)
            {
                parts.Add("Shift");
            }

            string keyText = null;
            if (keyCode >= Keys.A && keyCode <= Keys.Z)
            {
                keyText = keyCode.ToString().ToUpperInvariant();
            }
            else if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
            {
                keyText = ((int)(keyCode - Keys.D0)).ToString();
            }
            else if (keyCode >= Keys.F1 && keyCode <= Keys.F12)
            {
                keyText = keyCode.ToString().ToUpperInvariant();
            }

            if (parts.Count == 0 || string.IsNullOrEmpty(keyText))
            {
                return null;
            }

            parts.Add(keyText);
            return string.Join("+", parts.ToArray());
        }

        public static bool TryParse(string text, out uint modifiers, out uint keyCode)
        {
            modifiers = 0;
            keyCode = 0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var tokens = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            string keyToken = null;
            foreach (var token in tokens)
            {
                var value = token.Trim().ToUpperInvariant();
                switch (value)
                {
                    case "CTRL":
                        modifiers |= NativeMethods.MOD_CONTROL;
                        break;
                    case "ALT":
                        modifiers |= NativeMethods.MOD_ALT;
                        break;
                    case "SHIFT":
                        modifiers |= NativeMethods.MOD_SHIFT;
                        break;
                    case "WIN":
                    case "META":
                        modifiers |= NativeMethods.MOD_WIN;
                        break;
                    default:
                        keyToken = value;
                        break;
                }
            }

            if (modifiers == 0 || string.IsNullOrEmpty(keyToken))
            {
                return false;
            }

            if (keyToken.Length == 1)
            {
                keyCode = (uint)char.ToUpperInvariant(keyToken[0]);
                return true;
            }

            if (keyToken.StartsWith("F", StringComparison.OrdinalIgnoreCase))
            {
                int fn;
                if (int.TryParse(keyToken.Substring(1), out fn) && fn >= 1 && fn <= 12)
                {
                    keyCode = (uint)Keys.F1 + (uint)(fn - 1);
                    return true;
                }
            }

            return false;
        }
    }
}
