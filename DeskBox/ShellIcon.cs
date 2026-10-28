using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OpenWinBox;

// Ícono/miniatura del shell de Windows (el mismo que muestra el Explorador).
// Las APIs de miniaturas de Windows.Storage fallan en apps sin empaquetar.
// Píxeles BGRA ya listos: se extraen en un hilo de fondo y la imagen se crea en el hilo de UI
internal sealed record IconPixels(byte[] Pixels, int Width, int Height)
{
    public ImageSource ToImage()
    {
        var image = new WriteableBitmap(Width, Height);
        using (var stream = image.PixelBuffer.AsStream()) stream.Write(Pixels);
        image.Invalidate();
        return image;
    }
}

internal static class ShellIcon
{
    // Algunos extractores de íconos del shell (p. ej. los .url de Steam) solo funcionan en STA;
    // en el pool de hilos (MTA) devuelven el ícono genérico
    public static Task<T> RunOnStaThread<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    // Seguro de llamar fuera del hilo de UI (no toca XAML)
    public static IconPixels? GetPixels(string path, int size)
    {
        if (SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factory) != 0)
            return null;
        var hbmp = IntPtr.Zero;
        var hdc = GetDC(IntPtr.Zero);
        try
        {
            if (factory.GetImage(new SIZE { cx = size, cy = size }, 0, out hbmp) != 0) return null;
            GetObject(hbmp, Marshal.SizeOf<BITMAP>(), out var bm);

            // Pedir filas de arriba abajo (altura negativa) en BGRA de 32 bits, el formato de WriteableBitmap
            var header = new BITMAPINFOHEADER { biSize = 40, biWidth = bm.bmWidth, biHeight = -bm.bmHeight, biPlanes = 1, biBitCount = 32 };
            var pixels = new byte[bm.bmWidth * bm.bmHeight * 4];
            if (GetDIBits(hdc, hbmp, 0, (uint)bm.bmHeight, pixels, ref header, 0) == 0) return null;

            // El shell entrega alpha sin premultiplicar y WriteableBitmap lo espera premultiplicado;
            // sin esto los bordes semitransparentes salen con halos y manchas
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var a = pixels[i + 3];
                if (a == 255) continue;
                pixels[i] = (byte)(pixels[i] * a / 255);
                pixels[i + 1] = (byte)(pixels[i + 1] * a / 255);
                pixels[i + 2] = (byte)(pixels[i + 2] * a / 255);
            }
            return new IconPixels(pixels, bm.bmWidth, bm.bmHeight);
        }
        finally
        {
            if (hbmp != IntPtr.Zero) DeleteObject(hbmp);
            ReleaseDC(IntPtr.Zero, hdc);
            Marshal.ReleaseComObject(factory);
        }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    private struct SIZE { public int cx, cy; }

    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr h, int size, out BITMAP bm);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bmi, uint usage);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
}
