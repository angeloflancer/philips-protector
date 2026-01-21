using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MazManager.Helpers
{
    /// <summary>
    /// Helper class for creating rounded window regions (Windows XP compatible)
    /// </summary>
    public static class WindowHelper
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Sets a rounded rectangle region for the window
        /// </summary>
        /// <param name="window">The window to apply the region to</param>
        /// <param name="cornerRadius">The corner radius in pixels</param>
        public static void SetRoundedRegion(Window window, int cornerRadius)
        {
            try
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.Handle;

                if (hwnd == IntPtr.Zero)
                    return;

                int width = (int)window.ActualWidth;
                int height = (int)window.ActualHeight;

                if (width <= 0 || height <= 0)
                    return;

                IntPtr rgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, cornerRadius, cornerRadius);
                SetWindowRgn(hwnd, rgn, true);
                // Note: Don't delete rgn - Windows takes ownership after SetWindowRgn
            }
            catch
            {
                // Silently ignore errors - window will just be rectangular
            }
        }

        /// <summary>
        /// Attaches event handlers to automatically update the rounded region when window size changes
        /// </summary>
        /// <param name="window">The window to attach handlers to</param>
        /// <param name="cornerRadius">The corner radius in pixels</param>
        public static void EnableRoundedCorners(Window window, int cornerRadius)
        {
            window.SourceInitialized += (s, e) =>
            {
                SetRoundedRegion(window, cornerRadius);
            };

            window.SizeChanged += (s, e) =>
            {
                SetRoundedRegion(window, cornerRadius);
            };
        }
    }
}
