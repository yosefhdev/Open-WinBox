using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OpenWinBox;

// Copia semitransparente de un elemento que sigue al cursor fuera de la ventana
// (como al arrastrar un archivo): ventana Win32 por capas, sin activarse y transparente al mouse
internal sealed class DragGhost
{
    private const int Radius = 6; // esquinas como las pestañas
    private IntPtr _hwnd;
    private int _x, _y;
    private bool _closed;

    public static DragGhost Show(UIElement element, int x, int y)
    {
        var ghost = new DragGhost { _x = x, _y = y };
        ghost.CreateAsync(element);
        return ghost;
    }

    public void Move(int x, int y)
    {
        _x = x;
        _y = y;
        if (_hwnd != IntPtr.Zero) SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void Close()
    {
        _closed = true;
        if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd);
        _hwnd = IntPtr.Zero;
    }

    private async void CreateAsync(UIElement element)
    {
        var rtb = new RenderTargetBitmap();
        try { await rtb.RenderAsync(element); }
        catch (Exception) { return; } // sin copia: el arrastre sigue funcionando igual
        if (_closed || rtb.PixelWidth == 0) return;
        var content = (await rtb.GetPixelsAsync()).ToArray();
        if (_closed) return;

        // El contenido es transparente sobre el acrílico: ponerlo en una tarjeta con margen y esquinas redondeadas
        var fe = (FrameworkElement)element;
        var scale = rtb.PixelWidth / Math.Max(1, fe.ActualWidth);
        int padX = (int)(10 * scale), padY = (int)(6 * scale), r = (int)(Radius * scale);
        int cw = rtb.PixelWidth, ch = rtb.PixelHeight, w = cw + 2 * padX, h = ch + 2 * padY;
        byte card = fe.ActualTheme == ElementTheme.Dark ? (byte)0x2C : (byte)0xF3;
        var pixels = new byte[w * h * 4];
        for (var py = 0; py < h; py++)
            for (var px = 0; px < w; px++)
            {
                var i = (py * w + px) * 4;
                if (OutsideCorner(px, py, w, h, r)) continue; // queda transparente
                int sx = px - padX, sy = py - padY;
                var j = (sy * cw + sx) * 4;
                var inside = sx >= 0 && sy >= 0 && sx < cw && sy < ch;
                var a = inside ? content[j + 3] : 0;
                for (var c = 0; c < 3; c++) pixels[i + c] = (byte)((inside ? content[j + c] : 0) + card * (255 - a) / 255); // ya premultiplicado
                pixels[i + 3] = 255;
            }

        _hwnd = CreateWindowEx(WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            "STATIC", "", WS_POPUP, _x, _y, w, h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) return;

        var screen = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(screen);
        var info = new BITMAPINFOHEADER { biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        var bmp = CreateDIBSection(mem, ref info, 0, out var bits, IntPtr.Zero, 0);
        Marshal.Copy(pixels, 0, bits, pixels.Length);
        var old = SelectObject(mem, bmp);
        var size = new SIZE { cx = w, cy = h };
        var src = new POINT();
        var dst = new POINT { X = _x, Y = _y };
        var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 170, AlphaFormat = 1 /* AC_SRC_ALPHA */ };
        UpdateLayeredWindow(_hwnd, screen, ref dst, ref size, mem, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
        SelectObject(mem, old);
        DeleteObject(bmp);
        DeleteDC(mem);
        ReleaseDC(IntPtr.Zero, screen);
        ShowWindow(_hwnd, 4 /* SW_SHOWNOACTIVATE */);
        Move(_x, _y);
    }

    private static bool OutsideCorner(int x, int y, int w, int h, int r)
    {
        var cx = x < r ? r : x >= w - r ? w - r - 1 : x;
        var cy = y < r ? r : y >= h - r ? h - r - 1 : y;
        return (x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r;
    }

    private const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
        WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_POPUP = 0x80000000, SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private struct POINT { public int X, Y; }
    private struct SIZE { public int cx, cy; }
    private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string title, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr hInstance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT dst, ref SIZE size,
        IntPtr hdcSrc, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
}
