using System.Threading;
using System.Windows.Forms;

namespace ThemeTray;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        _mutex = new Mutex(initiallyOwned: true, name: "ThemeTray.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            using var app = new TrayApplicationContext();
            Application.Run(app);
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
