using System.Runtime.InteropServices;

namespace DeskBox.Platform;

/// <summary>
/// Finds the bounds of the taskbar button under a screen point through UI
/// Automation (raw vtable calls, AOT-safe, see <see cref="ComVtable"/>).
/// Slots from UIAutomationClient.h: IUIAutomation ElementFromPoint 7;
/// IUIAutomationElement get_CurrentBoundingRectangle 43.
/// </summary>
internal static class TaskbarButtonLocator
{
    private static readonly Guid s_clsidCuiAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    private static readonly Guid s_iidIuiAutomation = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
    private static IntPtr s_automation;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ElementFromPointInvoker(IntPtr self, Win32Helper.POINT point, out IntPtr element);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetBoundingRectangleInvoker(IntPtr self, out Win32Helper.RECT rect);

    /// <summary>
    /// Returns the button bounds when the point is on a taskbar band (outside
    /// the work area) and the element there is button-sized; the taskbar
    /// background or a missing UIA server yields false.
    /// </summary>
    internal static bool TryGetButtonBounds(Win32Helper.POINT point, Win32Helper.RECT workArea, out Win32Helper.RECT bounds)
    {
        bounds = default;
        bool onTaskbar = point.X < workArea.Left || point.X >= workArea.Right ||
            point.Y < workArea.Top || point.Y >= workArea.Bottom;
        if (!onTaskbar)
        {
            return false;
        }

        if (s_automation == IntPtr.Zero)
        {
            Guid clsid = s_clsidCuiAutomation;
            Guid iid = s_iidIuiAutomation;
            if (!ComVtable.TryCreateInProcessInstance(ref clsid, ref iid, out s_automation))
            {
                return false;
            }
        }

        IntPtr element = IntPtr.Zero;
        try
        {
            if (ComVtable.Slot<ElementFromPointInvoker>(s_automation, 7)(s_automation, point, out element) < 0 ||
                element == IntPtr.Zero ||
                ComVtable.Slot<GetBoundingRectangleInvoker>(element, 43)(element, out Win32Helper.RECT rect) < 0)
            {
                return false;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            // A button is roughly square; the whole taskbar or its start
            // region is far wider than tall.
            if (width <= 0 || height <= 0 || width > height * 3 || height > width * 3)
            {
                return false;
            }

            bounds = rect;
            return true;
        }
        finally
        {
            if (element != IntPtr.Zero)
            {
                Marshal.Release(element);
            }
        }
    }
}
