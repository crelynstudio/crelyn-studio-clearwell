using System.Windows;

namespace Clearwell.Core;

public static class WindowBounds
{
    public static Rect Fit(Rect workArea, Rect window, bool center)
    {
        var width = Math.Min(window.Width, workArea.Width);
        var height = Math.Min(window.Height, workArea.Height);
        var left = center ? workArea.Left + (workArea.Width - width) / 2 : Math.Clamp(window.Left, workArea.Left, workArea.Right - width);
        var top = center ? workArea.Top + (workArea.Height - height) / 2 : Math.Clamp(window.Top, workArea.Top, workArea.Bottom - height);
        return new Rect(left, top, width, height);
    }
}
