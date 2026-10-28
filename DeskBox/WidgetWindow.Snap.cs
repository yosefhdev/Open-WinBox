using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace OpenWinBox;

// Imán como el del original: al mover o redimensionar, los bordes se alinean con los del escritorio
// y con los de otros widgets cuando están cerca, y se iluminan. Solo alinea: cada widget se sigue moviendo solo.
public sealed partial class WidgetWindow
{
    [Flags]
    public enum Sides { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

    private static readonly List<WidgetWindow> All = new();
    // DIP: qué tan cerca debe estar un borde para que el imán actúe. Corto a propósito: con más rango
    // el widget se sentía "pegado" y había que mover mucho el mouse para despegarlo
    private const int SnapDistance = 8;
    private const int SnapGap = 5;       // DIP: separación entre widgets, igual que widgetSnapSpacing del original

    private SubclassProc? _subclassProc; // referencia viva: Windows llama a este delegado
    private RECT _frameMargins;          // borde invisible de redimensionar alrededor de lo que se ve
    private RECT _startRect;             // ventana y mouse al empezar a mover/redimensionar
    private POINT _startCursor;
    private bool _needAnchor;
    private RECT _lockRect;              // donde estaba la ventana al empezar a moverla (bloqueada)
    private WidgetWindow? _mergeTarget;  // widget de archivos con el cursor sobre su barra mientras se mueve este
    private long _mergeSince;
    private string _statusBeforeDrag = "";
    private static int MergeDelay => App.Current.Config.MergeDelayMs;
    private const nuint MergeTimerId = 0xDB08;

    private void InitSnapping()
    {
        All.Add(this);
        Closed += (_, _) => All.Remove(this);
        _subclassProc = OnWindowMessage;
        SetWindowSubclass(Hwnd, _subclassProc, 1, 0);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        switch (msg)
        {
            case WM_ENTERSIZEMOVE:
                GetWindowRect(hwnd, out var window);
                _lockRect = window;
                var frame = VisualBounds(hwnd);
                _frameMargins = new RECT { L = frame.L - window.L, T = frame.T - window.T, R = window.R - frame.R, B = window.B - frame.B };
                // El ancla se toma en el primer WM_MOVING/WM_SIZING: ahí el rectángulo propuesto y el mouse
                // coinciden. Aquí el mouse ya puede haberse movido sin que la ventana lo haya seguido.
                _needAnchor = true;
                break;
            case WM_MOVING when _config.IsLocked:
                // Bloqueado: el arrastre nativo de DragBar deja la ventana donde estaba, sin imán
                Marshal.StructureToPtr(_lockRect, lParam, false);
                return 1;
            case WM_MOVING:
            case WM_SIZING:
                var rect = Marshal.PtrToStructure<RECT>(lParam);
                var edges = msg == WM_MOVING ? Sides.Left | Sides.Top | Sides.Right | Sides.Bottom : EdgesFor((int)wParam);
                // Partir siempre de donde está el mouse, no del rectángulo ya ajustado: así el imán suelta
                // en cuanto el mouse se aleja unos píxeles, en vez de ir arrastrando la corrección anterior
                GetCursorPos(out var cursor);
                if (_needAnchor)
                {
                    _startRect = rect;
                    _startCursor = cursor;
                    _needAnchor = false;
                }
                int dx = cursor.X - _startCursor.X, dy = cursor.Y - _startCursor.Y;
                if (edges.HasFlag(Sides.Left)) rect.L = _startRect.L + dx;
                if (edges.HasFlag(Sides.Right)) rect.R = _startRect.R + dx;
                if (edges.HasFlag(Sides.Top)) rect.T = _startRect.T + dy;
                if (edges.HasFlag(Sides.Bottom)) rect.B = _startRect.B + dy;
                SnapRect(ref rect, edges, msg == WM_MOVING);
                Marshal.StructureToPtr(rect, lParam, false);
                if (msg == WM_MOVING && IsFileWidget) TrackMerge();
                DefSubclassProc(hwnd, msg, wParam, lParam);
                return 1; // TRUE: usar el rectángulo ajustado
            case WM_TIMER when (nuint)wParam == MergeTimerId:
                TrackMerge(); // con el mouse quieto no llegan WM_MOVING; WM_TIMER sí llega dentro del bucle de mover
                return 0;
            case WM_EXITSIZEMOVE:
                foreach (var w in All) w.SetGlow(Sides.None);
                EndMerge();
                break;
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    // Unir arrastrando: el cursor sobre la barra de otro widget de archivos lo marca como destino
    private void TrackMerge()
    {
        GetCursorPos(out var c);
        WidgetWindow? target = null;
        foreach (var w in All)
        {
            if (w == this || !w.IsFileWidget || !w.AppWindow.IsVisible) continue;
            var b = VisualBounds(w.Hwnd);
            if (c.X >= b.L && c.X < b.R && c.Y >= b.T && c.Y < b.T + 36 * GetDpiForWindow(w.Hwnd) / 96.0) { target = w; break; }
        }
        if (target != _mergeTarget)
        {
            _mergeTarget?.ShowMergeOverlay(-1);
            if (_mergeTarget == null) { _statusBeforeDrag = StatusText.Text; SetTimer(Hwnd, MergeTimerId, 100, IntPtr.Zero); }
            if (target == null) { KillTimer(Hwnd, MergeTimerId); StatusText.Text = _statusBeforeDrag; ShowMergeOverlay(-1); }
            _mergeTarget = target;
            _mergeSince = Environment.TickCount64;
        }
        if (target == null) return;
        var progress = (Environment.TickCount64 - _mergeSince) / (double)MergeDelay;
        target.ShowMergeOverlay(progress);
        // El origen tapa casi todo el destino: mostrar el aviso también aquí, bajo el cursor
        ShowMergeOverlay(progress);
        StatusText.Text = progress >= 1 ? $"Suelta para unir con «{target.TitleFor(target.ActiveFolder)}»" : "Mantén para unir…";
    }

    // Al soltar se decide por el tiempo medido, no por el timer (que puede no haber avanzado)
    private void EndMerge()
    {
        if (_mergeTarget is not { } target) return;
        var ready = Environment.TickCount64 - _mergeSince >= MergeDelay;
        _mergeTarget = null;
        KillTimer(Hwnd, MergeTimerId);
        target.ShowMergeOverlay(-1);
        ShowMergeOverlay(-1);
        StatusText.Text = _statusBeforeDrag;
        // MergeWidgets cierra esta ventana: no hacerlo dentro de su propio subclass proc
        if (ready && !target._closed) DispatcherQueue.TryEnqueue(() => App.Current.MergeWidgets(target._config, _config));
    }

    // progress < 0 oculta; de 0 a 1 "cargando"; >= 1 listo para soltar
    private void ShowMergeOverlay(double progress)
    {
        if (_closed) return;
        MergeOverlay.Opacity = progress < 0 ? 0 : progress >= 1 ? 1 : 0.6;
        MergeProgress.Value = Math.Clamp(progress, 0, 1);
        MergeText.Text = progress >= 1 ? "Suelta para unir" : "Mantén para unir…";
    }

    // Arrastre desde la agarradera de los widgets funcionales: la ventana sigue al mouse y usa el mismo imán.
    // (El arrastre nativo iniciado desde WinUI a veces arranca tarde y pierde los primeros movimientos.)
    private bool IsHandleDragging { get; set; }

    private void BeginHandleDrag()
    {
        GetWindowRect(Hwnd, out _startRect);
        var frame = VisualBounds(Hwnd);
        _frameMargins = new RECT { L = frame.L - _startRect.L, T = frame.T - _startRect.T, R = _startRect.R - frame.R, B = _startRect.B - frame.B };
        GetCursorPos(out _startCursor);
        IsHandleDragging = true;
    }

    private void MoveHandleDrag()
    {
        GetCursorPos(out var cursor);
        var rect = _startRect;
        int dx = cursor.X - _startCursor.X, dy = cursor.Y - _startCursor.Y;
        rect.L += dx; rect.R += dx; rect.T += dy; rect.B += dy;
        SnapRect(ref rect, Sides.Left | Sides.Top | Sides.Right | Sides.Bottom, moving: true);
        AppWindow.Move(new PointInt32(rect.L, rect.T));
    }

    private void EndHandleDrag()
    {
        if (!IsHandleDragging) return;
        IsHandleDragging = false;
        foreach (var w in All) w.SetGlow(Sides.None);
    }

    // rect llega en coordenadas de ventana (con bordes invisibles); el imán trabaja con lo que se ve
    private void SnapRect(ref RECT rect, Sides edges, bool moving)
    {
        var scale = GetDpiForWindow(Hwnd) / 96.0;
        var distance = (int)Math.Round(SnapDistance * scale);
        var gap = (int)Math.Round(SnapGap * scale);
        var v = new RECT { L = rect.L + _frameMargins.L, T = rect.T + _frameMargins.T, R = rect.R - _frameMargins.R, B = rect.B - _frameMargins.B };
        var area = DisplayArea.GetFromRect(new RectInt32(v.L, v.T, v.R - v.L, v.B - v.T), DisplayAreaFallback.Nearest).WorkArea;

        var others = new List<(WidgetWindow Window, RECT Bounds)>();
        foreach (var w in All)
            if (w != this && w.AppWindow.IsVisible && w.AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized })
                others.Add((w, VisualBounds(w.Hwnd)));

        // Eje X: mis bordes izquierdo/derecho contra el área de trabajo y contra widgets a la misma altura
        var x = Best(edges, distance, Sides.Left, v.L, Sides.Right, v.R, area.X, area.X + area.Width,
            others.FindAll(o => v.T < o.Bounds.B + distance && v.B > o.Bounds.T - distance),
            o => (o.L, o.R), gap);
        var y = Best(edges, distance, Sides.Top, v.T, Sides.Bottom, v.B, area.Y, area.Y + area.Height,
            others.FindAll(o => v.L < o.Bounds.R + distance && v.R > o.Bounds.L - distance),
            o => (o.T, o.B), gap);

        Apply(ref rect, x, moving, Sides.Left, Sides.Right, (ref RECT r, int d, Sides s) => { if (s == Sides.Left) r.L += d; else r.R += d; });
        Apply(ref rect, y, moving, Sides.Top, Sides.Bottom, (ref RECT r, int d, Sides s) => { if (s == Sides.Top) r.T += d; else r.B += d; });

        foreach (var w in All) w.SetGlow(Sides.None);
        SetGlow((x?.Mine ?? Sides.None) | (y?.Mine ?? Sides.None));
        x?.Other?.SetGlow(x.Value.Theirs | (y?.Other == x.Value.Other ? y!.Value.Theirs : Sides.None));
        if (y?.Other is { } yo && yo != x?.Other) yo.SetGlow(y.Value.Theirs);
    }

    private record struct Snap(int Delta, Sides Mine, WidgetWindow? Other, Sides Theirs);
    private delegate void EdgeMover(ref RECT r, int delta, Sides side);

    // El candidato más cercano dentro de la distancia del imán, para uno de los dos ejes
    private static Snap? Best(Sides edges, int distance, Sides low, int myLow, Sides high, int myHigh, int areaLow, int areaHigh,
        List<(WidgetWindow Window, RECT Bounds)> others, Func<RECT, (int Low, int High)> axis, int gap)
    {
        var candidates = new List<Snap>();
        void Add(Sides mine, int edge, int target, WidgetWindow? other, Sides theirs)
        {
            if (edges.HasFlag(mine) && Math.Abs(target - edge) <= distance) candidates.Add(new(target - edge, mine, other, theirs));
        }
        // Bordes del escritorio: pegado al borde, sin separación
        Add(low, myLow, areaLow, null, Sides.None);
        Add(high, myHigh, areaHigh, null, Sides.None);
        foreach (var (window, bounds) in others)
        {
            var (oLow, oHigh) = axis(bounds);
            Add(low, myLow, oLow, window, low);          // bordes del mismo lado alineados
            Add(high, myHigh, oHigh, window, high);
            Add(low, myLow, oHigh + gap, window, high);  // uno al lado del otro, con la separación del original
            Add(high, myHigh, oLow - gap, window, low);
        }
        Snap? best = null;
        foreach (var c in candidates)
            if (best == null || Math.Abs(c.Delta) < Math.Abs(best.Value.Delta)) best = c;
        return best;
    }

    // Mover: desplaza toda la ventana. Redimensionar: solo el borde que el usuario arrastra.
    private static void Apply(ref RECT rect, Snap? snap, bool moving, Sides low, Sides high, EdgeMover move)
    {
        if (snap is not { } s) return;
        if (moving)
        {
            move(ref rect, s.Delta, low);
            move(ref rect, s.Delta, high);
        }
        else move(ref rect, s.Delta, s.Mine);
    }

    private static Sides EdgesFor(int sizingEdge) => sizingEdge switch
    {
        1 => Sides.Left, 2 => Sides.Right, 3 => Sides.Top, 6 => Sides.Bottom,
        4 => Sides.Top | Sides.Left, 5 => Sides.Top | Sides.Right,
        7 => Sides.Bottom | Sides.Left, 8 => Sides.Bottom | Sides.Right,
        _ => Sides.None,
    };

    public void SetGlow(Sides sides)
    {
        GlowLeft.Visibility = sides.HasFlag(Sides.Left) ? Visibility.Visible : Visibility.Collapsed;
        GlowTop.Visibility = sides.HasFlag(Sides.Top) ? Visibility.Visible : Visibility.Collapsed;
        GlowRight.Visibility = sides.HasFlag(Sides.Right) ? Visibility.Visible : Visibility.Collapsed;
        GlowBottom.Visibility = sides.HasFlag(Sides.Bottom) ? Visibility.Visible : Visibility.Collapsed;
    }

    // Lo que se ve de la ventana (sin el borde invisible de redimensionar de Windows 11)
    private static RECT VisualBounds(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf<RECT>()) != 0)
            GetWindowRect(hwnd, out r);
        return r;
    }

    private struct RECT { public int L, T, R, B; }
    private struct POINT { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    private const uint WM_TIMER = 0x113, WM_SIZING = 0x214, WM_MOVING = 0x216, WM_ENTERSIZEMOVE = 0x231, WM_EXITSIZEMOVE = 0x232;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr hwnd, nuint id, uint ms, IntPtr proc);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hwnd, nuint id);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
}
