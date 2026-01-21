using System;
using System.Runtime.InteropServices;

namespace MazSvc
{
    /// <summary>
    /// Handles notifications from Windows Service to user sessions
    /// Uses WTSSendMessage API since services run in Session 0
    /// </summary>
    public class NotificationHelper : IDisposable
    {
        #region Win32 API

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSSendMessage(
            IntPtr hServer,
            int SessionId,
            string pTitle,
            int TitleLength,
            string pMessage,
            int MessageLength,
            int Style,
            int Timeout,
            out int pResponse,
            bool bWait);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            int Reserved,
            int Version,
            out IntPtr ppSessionInfo,
            out int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("kernel32.dll")]
        private static extern int WTSGetActiveConsoleSessionId();

        private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

        private const int WTS_ACTIVE_STATE = 0;
        private const int MB_OK = 0x00000000;
        private const int MB_ICONINFORMATION = 0x00000040;
        private const int MB_ICONWARNING = 0x00000030;
        private const int MB_ICONERROR = 0x00000010;
        private const int MB_SYSTEMMODAL = 0x00001000;

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionID;
            public IntPtr pWinStationName;
            public int State;
        }

        #endregion

        private bool _disposed;

        public NotificationHelper()
        {
            _disposed = false;
        }

        /// <summary>
        /// Shows a notification message to all active user sessions
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="message">Message content</param>
        public void ShowNotification(string title, string message)
        {
            ShowNotification(title, message, NotificationType.Information);
        }

        /// <summary>
        /// Shows a notification message to all active user sessions
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="message">Message content</param>
        /// <param name="type">Type of notification (affects icon)</param>
        public void ShowNotification(string title, string message, NotificationType type)
        {
            try
            {
                // Get all active sessions and send message to each
                int[] activeSessions = GetActiveSessions();

                if (activeSessions.Length == 0)
                {
                    // Try console session as fallback
                    int consoleSession = WTSGetActiveConsoleSessionId();
                    if (consoleSession != -1)
                    {
                        SendMessageToSession(consoleSession, title, message, type);
                    }
                    return;
                }

                foreach (int sessionId in activeSessions)
                {
                    SendMessageToSession(sessionId, title, message, type);
                }
            }
            catch
            {
                // Silently fail if notification cannot be shown
            }
        }

        /// <summary>
        /// Gets all active user sessions
        /// </summary>
        private int[] GetActiveSessions()
        {
            System.Collections.Generic.List<int> sessions = new System.Collections.Generic.List<int>();

            IntPtr pSessionInfo = IntPtr.Zero;
            int sessionCount = 0;

            try
            {
                if (WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, out pSessionInfo, out sessionCount))
                {
                    int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                    IntPtr current = pSessionInfo;

                    for (int i = 0; i < sessionCount; i++)
                    {
                        WTS_SESSION_INFO sessionInfo = (WTS_SESSION_INFO)Marshal.PtrToStructure(
                            current, typeof(WTS_SESSION_INFO));

                        // Only include active sessions (State == WTSActive)
                        if (sessionInfo.State == WTS_ACTIVE_STATE && sessionInfo.SessionID != 0)
                        {
                            sessions.Add(sessionInfo.SessionID);
                        }

                        current = new IntPtr(current.ToInt64() + dataSize);
                    }
                }
            }
            finally
            {
                if (pSessionInfo != IntPtr.Zero)
                {
                    WTSFreeMemory(pSessionInfo);
                }
            }

            return sessions.ToArray();
        }

        /// <summary>
        /// Sends a message to a specific session
        /// </summary>
        private void SendMessageToSession(int sessionId, string title, string message, NotificationType type)
        {
            try
            {
                int style = MB_OK | MB_SYSTEMMODAL;

                switch (type)
                {
                    case NotificationType.Information:
                        style |= MB_ICONINFORMATION;
                        break;
                    case NotificationType.Warning:
                        style |= MB_ICONWARNING;
                        break;
                    case NotificationType.Error:
                        style |= MB_ICONERROR;
                        break;
                }

                int response;
                WTSSendMessage(
                    WTS_CURRENT_SERVER_HANDLE,
                    sessionId,
                    title,
                    title.Length * 2, // Unicode string length in bytes
                    message,
                    message.Length * 2, // Unicode string length in bytes
                    style,
                    10, // Timeout in seconds (0 for no timeout, but we use 10 to not block)
                    out response,
                    false); // Don't wait for response
            }
            catch
            {
                // Silently fail
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Type of notification (affects the icon displayed)
    /// </summary>
    public enum NotificationType
    {
        Information,
        Warning,
        Error
    }
}
