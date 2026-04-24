using System;
using System.Threading;
using System.Windows.Forms;

namespace WinScreen
{
    internal static class Program
    {
        private static Mutex _singleInstanceMutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "SikiLab.SLSnap.Singleton", out createdNew);
            if (!createdNew)
            {
                return;
            }

            NativeMethods.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new TrayApplicationContext());
            }
            finally
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
            }
        }
    }
}
