using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Clearwell.Core;

namespace Clearwell;

public partial class MainWindow
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessages);
    }

    private IntPtr WindowMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0024 && TryMonitor(hwnd, out var monitor)) // WM_GETMINMAXINFO
        {
            var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            var scale = Math.Max(96, GetDpiForWindow(hwnd)) / 96.0;
            limits.MaxPosition = new NativePoint(monitor.Work.Left - monitor.Monitor.Left, monitor.Work.Top - monitor.Monitor.Top);
            limits.MaxSize = new NativePoint(monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top);
            limits.MaxTrackSize = limits.MaxSize;
            limits.MinTrackSize = new NativePoint((int)Math.Min(900 * scale, limits.MaxSize.X), (int)Math.Min(560 * scale, limits.MaxSize.Y));
            Marshal.StructureToPtr(limits, lParam, false);
            handled = true;
        }
        else if (message is 0x02E0 or 0x007E or 0x001A) // DPI/display/work-area change
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => FitToMonitor(false)));
        return IntPtr.Zero;
    }

    private void FitToMonitor(bool center)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!TryMonitor(hwnd, out var monitor) || !GetWindowRect(hwnd, out var current)) return;
        var scale = Math.Max(96, GetDpiForWindow(hwnd)) / 96.0;
        MinWidth = Math.Min(900, (monitor.Work.Right - monitor.Work.Left) / scale);
        MinHeight = Math.Min(560, (monitor.Work.Bottom - monitor.Work.Top) / scale);
        if (WindowState != WindowState.Normal) return;
        var fitted = WindowBounds.Fit(monitor.Work.ToRect(), current.ToRect(), center);
        if (fitted != current.ToRect()) SetWindowPos(hwnd, IntPtr.Zero, (int)fitted.Left, (int)fitted.Top, (int)fitted.Width, (int)fitted.Height, 0x0014); // no activation or Z-order change
    }

    private static bool TryMonitor(IntPtr hwnd, out MonitorInfo monitor)
    {
        monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor);
    }

    private void VerifyCaption()
    {
        UpdateLayout();
        var hwnd = new WindowInteropHelper(this).Handle;
        foreach (var (element, expected) in new (FrameworkElement, int)[] { (BrandSubtitle, 2), (GlobalSearch, 1), (SearchButton, 1), (MinimizeButton, 1), (MaximizeButton, 1), (CloseButton, 1) })
        {
            var point = element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
            var packed = new IntPtr(unchecked((int)(((uint)(ushort)(int)point.Y << 16) | (ushort)(int)point.X)));
            if (SendMessage(hwnd, 0x0084, IntPtr.Zero, packed).ToInt32() != expected)
                throw new InvalidOperationException("Native caption hit test failed: " + element.Name);
        }
        foreach (var button in new[] { MinimizeButton, MaximizeButton, CloseButton })
        {
            var icon = (FrameworkElement)button.Content;
            var center = icon.TranslatePoint(new Point(icon.ActualWidth / 2, icon.ActualHeight / 2), button);
            if (Math.Abs(center.X - button.ActualWidth / 2) > 1 || Math.Abs(center.Y - button.ActualHeight / 2) > 1)
                throw new InvalidOperationException("Caption icon is not centered: " + button.Name);
        }
    }

    private void VerifyWindowPlacement(string evidence)
    {
        FitToMonitor(false);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!TryMonitor(hwnd, out var monitor) || !GetWindowRect(hwnd, out var bounds) || !monitor.Work.ToRect().Contains(bounds.ToRect()))
            throw new InvalidOperationException("Startup window escaped the monitor work area.");
        File.WriteAllText(evidence, System.Text.Json.JsonSerializer.Serialize(new { Dpi = GetDpiForWindow(hwnd), State = WindowState.ToString(), WorkArea = monitor.Work.ToRect(), Window = bounds.ToRect() }));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y) { public int X = x, Y = y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rect ToRect() => new(Left, Top, Right - Left, Bottom - Top);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
