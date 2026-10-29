using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ResetExplode;

public partial class App : Application
{
    private static Mutex? _single;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);

    protected override void OnStartup(StartupEventArgs e)
    {
        bool first;
        try { _single = new Mutex(true, @"Local\Trin0MasterApp.SingleInstance", out first); }
        catch (UnauthorizedAccessException) { first = false; }

        if (!first)
        {
            IntPtr h = FindWindow(null, "Trin0 Master APP");
            if (h != IntPtr.Zero)
            {
                if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
                SetForegroundWindow(h);
            }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        new MainWindow().Show();
    }
}