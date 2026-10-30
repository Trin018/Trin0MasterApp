using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ResetExplode;

/// <summary>
/// Gere o ícone no System Tray (Área de Notificação / perto do relógio) usando a API nativa do Windows (Shell_NotifyIcon).
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly Window _parentWindow;
    private readonly Action _showMainWindow;
    private readonly Action _quickCleanRam;
    private readonly Action _toggleOverlay;
    private readonly Action _exitApp;

    private readonly uint _taskbarCreatedMsg;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _added;

    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1024;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private DateTime _lastCpuTempAlert = DateTime.MinValue;
    private DateTime _lastGpuTempAlert = DateTime.MinValue;

    public TrayService(Window parent, Action showWindow, Action cleanRam, Action toggleOverlay, Action exitApp)
    {
        _parentWindow = parent;
        _showMainWindow = showWindow;
        _quickCleanRam = cleanRam;
        _toggleOverlay = toggleOverlay;
        _exitApp = exitApp;

        _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

        if (_parentWindow.IsLoaded) InitTray();
        else _parentWindow.Loaded += (_, _) => InitTray();
    }

    private void InitTray()
    {
        if (_hwnd != IntPtr.Zero) return;
        _hwnd = new WindowInteropHelper(_parentWindow).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        LoadTrayIcon();
        AddTrayIcon();
    }

    private void LoadTrayIcon()
    {
        try
        {
            var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (res?.Stream != null)
            {
                using var ms = new MemoryStream();
                res.Stream.CopyTo(ms);
                var bytes = ms.ToArray();
                _hIcon = CreateIconFromResourceEx(bytes, (uint)bytes.Length, true, 0x00030000, 16, 16, 0);
            }
        }
        catch { }

        if (_hIcon == IntPtr.Zero)
            _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // Ícone padrão
    }

    private void AddTrayIcon()
    {
        if (_hwnd == IntPtr.Zero) return;
        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = 0x00000001 | 0x00000002 | 0x00000004, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Trin0 Master APP"
        };
        _added = Shell_NotifyIcon(0, ref nid); // NIM_ADD
    }

    public void Notify(string title, string text)
    {
        if (_hwnd == IntPtr.Zero || !_added) return;
        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = 0x00000010, // NIF_INFO
            szInfoTitle = title.Length > 63 ? title[..63] : title,
            szInfo = text.Length > 255 ? text[..255] : text,
            dwInfoFlags = 0x00000001 // NIIF_INFO
        };
        Shell_NotifyIcon(1, ref nid); // NIM_MODIFY
    }

    public void CheckHealthAlerts(float? cpuTemp, float? gpuTemp)
    {
        var now = DateTime.Now;
        if (cpuTemp >= 90f && (now - _lastCpuTempAlert).TotalMinutes > 10)
        {
            _lastCpuTempAlert = now;
            Notify("Alerta Térmico: CPU Quente", $"A CPU atingiu {cpuTemp:0}°C! Verifique a refrigeração.");
        }
        if (gpuTemp >= 86f && (now - _lastGpuTempAlert).TotalMinutes > 10)
        {
            _lastGpuTempAlert = now;
            Notify("Alerta Térmico: GPU Quente", $"A GPU atingiu {gpuTemp:0}°C! A ventoinha pode estar bloqueada.");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int cmd = lParam.ToInt32();
            if (cmd == WM_LBUTTONDBLCLK)
            {
                _showMainWindow();
                handled = true;
            }
            else if (cmd == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
        }
        else if (msg == _taskbarCreatedMsg && _taskbarCreatedMsg != 0)
        {
            AddTrayIcon(); // Recria o ícone caso o explorer.exe reinicie
        }

        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        var menu = new ContextMenu();
        var itemOpen = new MenuItem { Header = "Abrir Trin0 Master APP", FontWeight = FontWeights.Bold };
        itemOpen.Click += (_, _) => _showMainWindow();

        var itemRam = new MenuItem { Header = "Limpar RAM (Standby List)" };
        itemRam.Click += (_, _) => _quickCleanRam();

        var itemOverlay = new MenuItem { Header = "Ligar / Desligar Overlay (Ctrl+Shift+O)" };
        itemOverlay.Click += (_, _) => _toggleOverlay();

        var itemExit = new MenuItem { Header = "Fechar Aplicação" };
        itemExit.Click += (_, _) => _exitApp();

        menu.Items.Add(itemOpen);
        menu.Items.Add(new Separator());
        menu.Items.Add(itemRam);
        menu.Items.Add(itemOverlay);
        menu.Items.Add(new Separator());
        menu.Items.Add(itemExit);

        SetForegroundWindow(_hwnd);
        menu.IsOpen = true;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero && _added)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(2, ref nid); // NIM_DELETE
            _added = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        _hwndSource?.RemoveHook(WndProc);
    }

    // P/Invoke Win32 puro
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconFromResourceEx(byte[] pbIconBits, uint cbIconBits, bool fIcon, uint dwVersion, int cxDesired, int cyDesired, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}