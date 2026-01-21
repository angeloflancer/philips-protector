using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MazSvc
{
    /// <summary>
    /// Handles system tray notifications from Windows Service
    /// Uses Shell_NotifyIcon API for balloon notifications
    /// </summary>
    public class NotificationHelper : IDisposable
    {
        #region Win32 API for Shell Notification

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll")]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;
        private const uint NIF_GUID = 0x00000020;

        private const uint NIIF_NONE = 0x00000000;
        private const uint NIIF_INFO = 0x00000001;
        private const uint NIIF_WARNING = 0x00000002;
        private const uint NIIF_ERROR = 0x00000003;
        private const uint NIIF_USER = 0x00000004;
        private const uint NIIF_NOSOUND = 0x00000010;

        #endregion

        #region Win32 API for Session Management

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSEnumerateSessions(
            IntPtr hServer, int Reserved, int Version,
            out IntPtr ppSessionInfo, out int pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("kernel32.dll")]
        private static extern int WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken, string lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionID;
            public IntPtr pWinStationName;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        private const int WTS_ACTIVE_STATE = 0;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const short SW_HIDE = 0;

        #endregion

        private bool _disposed;
        private IntPtr _notifyIconHwnd;
        private NOTIFYICONDATA _notifyIconData;
        private bool _iconCreated;
        private WndProc _wndProcDelegate;
        private string _className;
        private uint _iconId;
        private static int _instanceCounter = 0;

        public NotificationHelper()
        {
            _disposed = false;
            _iconCreated = false;
            _iconId = (uint)Interlocked.Increment(ref _instanceCounter);
            _className = "MazSvcNotifyClass_" + _iconId;
        }

        /// <summary>
        /// Shows a system tray balloon notification
        /// </summary>
        public void ShowNotification(string title, string message)
        {
            ShowNotification(title, message, NotificationType.Information);
        }

        /// <summary>
        /// Shows a system tray balloon notification
        /// </summary>
        public void ShowNotification(string title, string message, NotificationType type)
        {
            try
            {
                // Use PowerShell to show toast notification in user session
                ShowToastNotificationInUserSession(title, message, type);
            }
            catch
            {
                // Silently fail if notification cannot be shown
            }
        }

        /// <summary>
        /// Shows a toast notification using PowerShell in the active user session
        /// </summary>
        private void ShowToastNotificationInUserSession(string title, string message, NotificationType type)
        {
            try
            {
                int sessionId = WTSGetActiveConsoleSessionId();
                if (sessionId == -1 || sessionId == 0)
                {
                    // Try to find an active user session
                    sessionId = GetFirstActiveUserSession();
                    if (sessionId <= 0) return;
                }

                // Escape special characters for PowerShell
                string escapedTitle = title.Replace("'", "''").Replace("`", "``");
                string escapedMessage = message.Replace("'", "''").Replace("`", "``");

                // PowerShell script to show Windows 10+ toast notification with system tray icon
                string script = string.Format(@"
Add-Type -AssemblyName System.Windows.Forms
$icon = [System.Drawing.SystemIcons]::Information
$notify = New-Object System.Windows.Forms.NotifyIcon
$notify.Icon = $icon
$notify.Visible = $true
$notify.BalloonTipTitle = '{0}'
$notify.BalloonTipText = '{1}'
$notify.BalloonTipIcon = '{2}'
$notify.ShowBalloonTip(5000)
Start-Sleep -Seconds 6
$notify.Dispose()
", escapedTitle, escapedMessage, GetBalloonTipIcon(type));

                // Convert script to Base64 for safe passing
                byte[] scriptBytes = System.Text.Encoding.Unicode.GetBytes(script);
                string base64Script = Convert.ToBase64String(scriptBytes);

                // Create the process in user session
                LaunchProcessInUserSession(sessionId, "powershell.exe", 
                    string.Format("-NoProfile -WindowStyle Hidden -EncodedCommand {0}", base64Script));
            }
            catch
            {
                // Fallback: Try using msg.exe or other method
                TryFallbackNotification(title, message);
            }
        }

        private string GetBalloonTipIcon(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Warning:
                    return "Warning";
                case NotificationType.Error:
                    return "Error";
                default:
                    return "Info";
            }
        }

        private int GetFirstActiveUserSession()
        {
            IntPtr pSessionInfo = IntPtr.Zero;
            int sessionCount = 0;

            try
            {
                if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, out pSessionInfo, out sessionCount))
                {
                    int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                    IntPtr current = pSessionInfo;

                    for (int i = 0; i < sessionCount; i++)
                    {
                        WTS_SESSION_INFO sessionInfo = (WTS_SESSION_INFO)Marshal.PtrToStructure(
                            current, typeof(WTS_SESSION_INFO));

                        if (sessionInfo.State == WTS_ACTIVE_STATE && sessionInfo.SessionID > 0)
                        {
                            return sessionInfo.SessionID;
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

            return -1;
        }

        private void LaunchProcessInUserSession(int sessionId, string application, string arguments)
        {
            IntPtr userToken = IntPtr.Zero;
            IntPtr envBlock = IntPtr.Zero;

            try
            {
                if (!WTSQueryUserToken((uint)sessionId, out userToken))
                {
                    return;
                }

                CreateEnvironmentBlock(out envBlock, userToken, false);

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = @"winsta0\default";
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = SW_HIDE;

                PROCESS_INFORMATION pi;

                string cmdLine = string.Format("\"{0}\" {1}", application, arguments);

                if (CreateProcessAsUser(
                    userToken,
                    null,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW,
                    envBlock,
                    null,
                    ref si,
                    out pi))
                {
                    CloseHandle(pi.hThread);
                    CloseHandle(pi.hProcess);
                }
            }
            finally
            {
                if (envBlock != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(envBlock);
                }
                if (userToken != IntPtr.Zero)
                {
                    CloseHandle(userToken);
                }
            }
        }

        private void TryFallbackNotification(string title, string message)
        {
            // Fallback using msg.exe (works on older Windows)
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "msg.exe";
                psi.Arguments = string.Format("* /TIME:5 \"{0}: {1}\"", title, message);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch
            {
                // All notification methods failed
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                try
                {
                    if (_iconCreated && _notifyIconHwnd != IntPtr.Zero)
                    {
                        NOTIFYICONDATA nid = new NOTIFYICONDATA();
                        nid.cbSize = Marshal.SizeOf(nid);
                        nid.hWnd = _notifyIconHwnd;
                        nid.uID = _iconId;
                        Shell_NotifyIcon(NIM_DELETE, ref nid);
                    }

                    if (_notifyIconHwnd != IntPtr.Zero)
                    {
                        DestroyWindow(_notifyIconHwnd);
                        _notifyIconHwnd = IntPtr.Zero;
                    }

                    if (!string.IsNullOrEmpty(_className))
                    {
                        UnregisterClass(_className, GetModuleHandle(null));
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
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
