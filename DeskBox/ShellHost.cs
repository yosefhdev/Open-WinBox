using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OpenWinBox;

// Ventana Win32 oculta que recibe el atajo global (F7) y los clics del ícono de bandeja.
// Se crea en el hilo de UI, así que los eventos ya llegan en ese hilo.
internal sealed class ShellHost : IDisposable
{
    public event Action? ToggleRequested;
    public event Action? ExitRequested;
    public event Action? PinToggleRequested;
    public event Action? SettingsRequested;

    public bool HotkeyRegistered { get; }
    public bool PinnedToDesktop { get; set; } // solo para mostrar la marca en el menú

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunValue = "OpenWinBox";
    private const int WM_HOTKEY = 0x312, WM_TRAY = 0x8001, WM_LBUTTONUP = 0x202, WM_RBUTTONUP = 0x205;
    private const int HOTKEY_ID = 1, VK_F7 = 0x76, MOD_NOREPEAT = 0x4000;
    private const int CMD_TOGGLE = 1, CMD_AUTOSTART = 2, CMD_EXIT = 3, CMD_PIN = 4, CMD_SETTINGS = 5;

    private readonly WndProc _wndProc; // referencia viva: el GC no debe recolectar el delegado que usa Windows
    private readonly IntPtr _hwnd;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NOTIFYICONDATA _tray;

    public ShellHost()
    {
        _wndProc = OnMessage;
        var hInstance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = "OpenWinBoxShell",
        };
        RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, wc.lpszClassName, "OpenWinBox", 0x80000000 /* WS_POPUP, nunca se muestra */,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // Falla si otra app (p. ej. el DeskBox original) ya tiene F7
        HotkeyRegistered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_NOREPEAT, VK_F7);

        _tray = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uFlags = 0x1 | 0x2 | 0x4, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            uCallbackMessage = WM_TRAY,
            hIcon = LoadTrayIcon(),
            szTip = HotkeyRegistered ? "OpenWinBox — F7 muestra/oculta" : "OpenWinBox — F7 ocupado por otra app",
        };
        Shell_NotifyIcon(0 /* NIM_ADD */, ref _tray);
    }

    public static readonly string IconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "OpenWinBox.ico");

    // El .ico trae una versión simplificada para 16-24 px; pedir el tamaño exacto de la bandeja la usa
    private static IntPtr LoadTrayIcon()
    {
        var size = GetSystemMetrics(49 /* SM_CXSMICON */);
        var icon = LoadImage(IntPtr.Zero, IconPath, 1 /* IMAGE_ICON */, size, size, 0x10 /* LR_LOADFROMFILE */);
        return icon != IntPtr.Zero ? icon : LoadIcon(IntPtr.Zero, new IntPtr(32512)); // sin el archivo: ícono genérico
    }

    // Antes el autoinicio se guardaba como "DeskBox2" (con la ruta del .exe viejo): pasarlo al nombre nuevo
    public static void MigrateAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue("DeskBox2") is not string) return;
        key.DeleteValue("DeskBox2");
        AutoStart = true;
    }

    public static bool AutoStart
    {
        get => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue(RunValue) is string;
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }

    private IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam == HOTKEY_ID) ToggleRequested?.Invoke();
        else if (msg == WM_TRAY && (int)lParam == WM_LBUTTONUP) ToggleRequested?.Invoke();
        else if (msg == WM_TRAY && (int)lParam == WM_RBUTTONUP) ShowMenu();
        else if (msg == _taskbarCreated) Shell_NotifyIcon(0, ref _tray); // explorer.exe se reinició: volver a poner el ícono
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, 0, CMD_TOGGLE, "Mostrar / ocultar (F7)");
        AppendMenu(menu, PinnedToDesktop ? 0x8u /* MF_CHECKED */ : 0, CMD_PIN, "Mantener en el escritorio (detrás de las apps)");
        AppendMenu(menu, AutoStart ? 0x8u : 0, CMD_AUTOSTART, "Iniciar con Windows");
        AppendMenu(menu, 0x800 /* MF_SEPARATOR */, 0, null);
        AppendMenu(menu, 0, CMD_SETTINGS, "Configuración…");
        AppendMenu(menu, 0x800, 0, null);
        AppendMenu(menu, 0, CMD_EXIT, "Salir");
        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd); // sin esto el menú no se cierra al hacer clic fuera
        var cmd = TrackPopupMenu(menu, 0x100 | 0x2 /* TPM_RETURNCMD | TPM_RIGHTBUTTON */, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);

        switch (cmd)
        {
            case CMD_TOGGLE: ToggleRequested?.Invoke(); break;
            case CMD_AUTOSTART: AutoStart = !AutoStart; break;
            case CMD_PIN: PinToggleRequested?.Invoke(); break;
            case CMD_SETTINGS: SettingsRequested?.Invoke(); break;
            case CMD_EXIT: ExitRequested?.Invoke(); break;
        }
    }

    public void Dispose()
    {
        Shell_NotifyIcon(2 /* NIM_DELETE */, ref _tray);
        UnregisterHotKey(_hwnd, HOTKEY_ID);
        DestroyWindow(_hwnd);
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID, uFlags, uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string? szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    private struct POINT { public int X, Y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string title, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr hInstance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, int vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInstance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, int id, string? text);
    [DllImport("user32.dll")] private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
