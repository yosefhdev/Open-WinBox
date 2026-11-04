using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenWinBox;

// Acciones del menú contextual de un archivo, las mismas que ofrece el Explorador
internal static class FileActions
{
    // En un hilo STA aparte: abrir algunos tipos (p. ej. imágenes con Fotos) desde el hilo de UI tumbaba la app
    // con una excepción COM. Sin programa asociado se ofrece "Abrir con…" como en el Explorador
    public static void Open(string path) => ShellIcon.RunOnStaThread(() =>
    {
        AllowSetForegroundWindow(-1); // que la app abierta pase al frente aunque el widget no esté activo
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return 0; }
        catch (Win32Exception) { }
        // Windows bloquea el acceso directo cuando apunta a un archivo descargado de internet
        // ("extensión de archivo peligrosa de web"): abrir el archivo original con su programa
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && LinkTarget(path) is { } target)
        {
            path = target;
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return 0; }
            catch (Win32Exception) { }
        }
        try { Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}"); }
        catch (Win32Exception) { }
        return 0;
    });

    public static void RunAsAdmin(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" }); }
        catch (Win32Exception) { } // el usuario canceló el aviso de UAC
    }

    public static void ShowInExplorer(string path) => Process.Start("explorer.exe", $"/select,\"{path}\"");

    public static void ShowProperties(string path)
    {
        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            fMask = 0xC, // SEE_MASK_INVOKEIDLIST: el verbo "properties" lo resuelve el shell
            lpVerb = "properties",
            lpFile = path,
            nShow = 1,
        };
        ShellExecuteEx(ref info);
    }

    // Papelera (no borrado definitivo): se puede recuperar desde la Papelera de reciclaje
    public static void MoveToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = 3, // FO_DELETE
            pFrom = path + "\0\0",
            fFlags = 0x40 | 0x10, // FOF_ALLOWUNDO | FOF_NOCONFIRMATION (la papelera ya es reversible)
        };
        var result = SHFileOperation(ref op);
        if (result != 0) throw new IOException(Lang.T("file.recycleError", result));
    }

    // Mueve a la carpeta indicada sin pisar nada: "x (2).lnk" si ya existe
    public static void MoveTo(string path, string folder)
    {
        var target = UniquePath(folder, Path.GetFileName(path));
        if (Directory.Exists(path)) Directory.Move(path, target);
        else File.Move(path, target);
    }

    public static void CopyTo(string path, string folder) =>
        File.Copy(path, UniquePath(folder, Path.GetFileName(path)));

    // Acceso directo "<nombre>.lnk" en la carpeta, apuntando a path (el original no se toca)
    private static string? LinkTarget(string lnk)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        string target = shell.CreateShortcut(lnk).TargetPath;
        return File.Exists(target) || Directory.Exists(target) ? target : null;
    }

    public static void CreateShortcutTo(string path, string folder)
    {
        var name = Directory.Exists(path) ? Path.GetFileName(path.TrimEnd('\\')) : Path.GetFileNameWithoutExtension(path);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        var link = shell.CreateShortcut(UniquePath(folder, name + ".lnk"));
        link.TargetPath = path;
        link.WorkingDirectory = Path.GetDirectoryName(path) ?? "";
        link.Save();
    }

    public static string UniquePath(string folder, string name)
    {
        var candidate = Path.Combine(folder, name);
        for (var i = 2; File.Exists(candidate) || Directory.Exists(candidate); i++)
            candidate = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
        return candidate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb, lpFile, lpParameters, lpDirectory;
        public int nShow;
        public IntPtr hInstApp, lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon, hProcess;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
}
