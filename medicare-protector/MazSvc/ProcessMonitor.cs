using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Monitors running processes and detects protected executables
    /// Uses WMI events for immediate detection
    /// </summary>
    public class ProcessMonitor : IDisposable
    {
        private ManagementEventWatcher _processStartWatcher;
        private List<ProtectedExecutable> _protectedExecutables;
        private HashSet<string> _runningProcessKeys;
        private object _lockObj = new object();
        private bool _disposed;

        /// <summary>
        /// Event raised when a protected process starts
        /// </summary>
        public event EventHandler<ProcessStartedEventArgs> ProtectedProcessStarted;

        /// <summary>
        /// Event raised when a protected process stops
        /// </summary>
        public event EventHandler<ProcessStoppedEventArgs> ProtectedProcessStopped;

        public ProcessMonitor()
        {
            _runningProcessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _disposed = false;
        }

        /// <summary>
        /// Starts WMI event-based monitoring for immediate process detection
        /// </summary>
        public void StartEventMonitoring(List<ProtectedExecutable> protectedExecutables)
        {
            _protectedExecutables = protectedExecutables;

            if (protectedExecutables == null || protectedExecutables.Count == 0)
            {
                return;
            }

            try
            {
                // Build WQL query for process creation events
                // Using Win32_ProcessStartTrace for immediate notification
                string wqlQuery = "SELECT * FROM Win32_ProcessStartTrace";

                _processStartWatcher = new ManagementEventWatcher(new WqlEventQuery(wqlQuery));
                _processStartWatcher.EventArrived += OnProcessStarted;
                _processStartWatcher.Start();
            }
            catch
            {
                // Fallback: WMI event subscription failed, will rely on polling
            }
        }

        /// <summary>
        /// Stops WMI event monitoring
        /// </summary>
        public void StopEventMonitoring()
        {
            try
            {
                if (_processStartWatcher != null)
                {
                    _processStartWatcher.Stop();
                    _processStartWatcher.Dispose();
                    _processStartWatcher = null;
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Updates the protected executable list for event monitoring
        /// </summary>
        public void UpdateProtectedExecutables(List<ProtectedExecutable> protectedExecutables)
        {
            _protectedExecutables = protectedExecutables;
        }

        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                if (_protectedExecutables == null || _protectedExecutables.Count == 0)
                {
                    return;
                }

                // Get process info from the event
                string processName = null;
                if (e.NewEvent.Properties["ProcessName"] != null)
                {
                    object processNameValue = e.NewEvent.Properties["ProcessName"].Value;
                    if (processNameValue != null)
                    {
                        processName = processNameValue.ToString();
                    }
                }
                
                int processId = 0;
                if (e.NewEvent.Properties["ProcessID"] != null)
                {
                    object processIdValue = e.NewEvent.Properties["ProcessID"].Value;
                    if (processIdValue != null)
                    {
                        processId = Convert.ToInt32(processIdValue);
                    }
                }

                if (string.IsNullOrEmpty(processName) || processId == 0)
                {
                    return;
                }

                // Check if this process matches any protected executable
                string processNameWithoutExt = Path.GetFileNameWithoutExtension(processName);

                foreach (ProtectedExecutable exe in _protectedExecutables)
                {
                    string exeNameWithoutExt = Path.GetFileNameWithoutExtension(exe.Name);

                    if (string.Equals(processNameWithoutExt, exeNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                    {
                        // Small delay to allow process to fully initialize
                        Thread.Sleep(100);

                        // Get the full path to verify
                        string processPath = GetProcessPathById(processId);

                        bool pathMatches = false;
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            string normalizedProcessPath = Path.GetFullPath(processPath).TrimEnd('\\');
                            string normalizedExePath = Path.GetFullPath(exe.FullPath).TrimEnd('\\');
                            pathMatches = string.Equals(normalizedProcessPath, normalizedExePath, StringComparison.OrdinalIgnoreCase);
                        }

                        // Match if name matches and (path matches OR couldn't get path)
                        if (pathMatches || string.IsNullOrEmpty(processPath))
                        {
                            ProcessInfo info = new ProcessInfo();
                            info.ProcessId = processId;
                            info.ProcessName = processNameWithoutExt;
                            info.FullPath = processPath ?? exe.FullPath;

                            string processKey = string.Format("{0}|{1}", processId, info.FullPath);

                            lock (_lockObj)
                            {
                                if (!_runningProcessKeys.Contains(processKey))
                                {
                                    _runningProcessKeys.Add(processKey);

                                    // Raise event for immediate handling
                                    if (ProtectedProcessStarted != null)
                                    {
                                        ProtectedProcessStarted(this, new ProcessStartedEventArgs(info));
                                    }
                                }
                            }

                            break;
                        }
                    }
                }
            }
            catch
            {
                // Ignore event processing errors
            }
        }

        /// <summary>
        /// Gets process path by process ID using WMI
        /// </summary>
        private string GetProcessPathById(int processId)
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        string.Format("SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {0}", processId)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object pathObj = obj["ExecutablePath"];
                        if (pathObj != null)
                        {
                            return pathObj.ToString();
                        }
                    }
                }
            }
            catch
            {
                // WMI query failed
            }

            return null;
        }

        /// <summary>
        /// Gets the list of currently running protected process keys
        /// </summary>
        public HashSet<string> GetRunningProcessKeys()
        {
            lock (_lockObj)
            {
                return new HashSet<string>(_runningProcessKeys, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Clears a process key when it stops
        /// </summary>
        public void RemoveProcessKey(string key)
        {
            lock (_lockObj)
            {
                _runningProcessKeys.Remove(key);
            }
        }

        /// <summary>
        /// Gets all running processes that match the protected executable list
        /// Matches by both process name AND full path
        /// </summary>
        /// <param name="protectedExecutables">List of executables to monitor</param>
        /// <returns>List of matching process information</returns>
        public List<ProcessInfo> GetMatchingProcesses(List<ProtectedExecutable> protectedExecutables)
        {
            List<ProcessInfo> matchingProcesses = new List<ProcessInfo>();

            if (protectedExecutables == null || protectedExecutables.Count == 0)
            {
                return matchingProcesses;
            }

            try
            {
                Process[] allProcesses = Process.GetProcesses();

                foreach (Process process in allProcesses)
                {
                    try
                    {
                        foreach (ProtectedExecutable exe in protectedExecutables)
                        {
                            bool nameMatches = false;
                            bool pathMatches = false;
                            string processPath = null;

                            // Check process name (without extension)
                            string exeNameWithoutExtension = Path.GetFileNameWithoutExtension(exe.Name);
                            if (string.Equals(process.ProcessName, exeNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                            {
                                nameMatches = true;
                            }

                            // Try to get the full path of the process
                            try
                            {
                                processPath = GetProcessPath(process);
                                if (!string.IsNullOrEmpty(processPath))
                                {
                                    // Normalize paths for comparison (remove trailing backslashes, etc.)
                                    string normalizedProcessPath = Path.GetFullPath(processPath).TrimEnd('\\');
                                    string normalizedExePath = Path.GetFullPath(exe.FullPath).TrimEnd('\\');
                                    
                                    if (string.Equals(normalizedProcessPath, normalizedExePath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        pathMatches = true;
                                    }
                                }
                            }
                            catch
                            {
                                // Cannot access process path (likely access denied or elevated process)
                                // Will match by name only if name matches
                                processPath = null;
                            }

                            // Match if:
                            // 1. Name matches AND path matches, OR
                            // 2. Name matches AND we couldn't get the path (elevated/admin process)
                            if (nameMatches && (pathMatches || string.IsNullOrEmpty(processPath)))
                            {
                                ProcessInfo info = new ProcessInfo();
                                info.ProcessId = process.Id;
                                info.ProcessName = process.ProcessName;
                                info.FullPath = processPath ?? exe.FullPath;
                                matchingProcesses.Add(info);
                                break; // Found a match, move to next process
                            }
                        }
                    }
                    catch
                    {
                        // Skip processes that cannot be accessed
                        continue;
                    }
                    finally
                    {
                        try
                        {
                            process.Dispose();
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
                // Return empty list if process enumeration fails
            }

            return matchingProcesses;
        }

        /// <summary>
        /// Gets the full path of a process executable
        /// Works for both normal and elevated (administrator) processes
        /// </summary>
        private string GetProcessPath(Process process)
        {
            // Try WMI first - works better for elevated processes
            try
            {
                string wmiPath = GetProcessPathFromWmi(process.Id);
                if (!string.IsNullOrEmpty(wmiPath))
                {
                    return wmiPath;
                }
            }
            catch
            {
                // WMI failed, try MainModule
            }

            // Try MainModule as fallback (works for non-elevated processes)
            try
            {
                if (process.MainModule != null && !string.IsNullOrEmpty(process.MainModule.FileName))
                {
                    return process.MainModule.FileName;
                }
            }
            catch
            {
                // MainModule access denied (likely elevated process)
            }

            return null;
        }

        /// <summary>
        /// Gets process path using WMI (works for elevated processes)
        /// </summary>
        private string GetProcessPathFromWmi(int processId)
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        string.Format("SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {0}", processId)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object pathObj = obj["ExecutablePath"];
                        if (pathObj != null)
                        {
                            return pathObj.ToString();
                        }
                    }
                }
            }
            catch
            {
                // WMI query failed
            }

            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopEventMonitoring();
            }
        }
    }

    /// <summary>
    /// Event args for protected process started event
    /// </summary>
    public class ProcessStartedEventArgs : EventArgs
    {
        public ProcessInfo ProcessInfo { get; private set; }

        public ProcessStartedEventArgs(ProcessInfo processInfo)
        {
            ProcessInfo = processInfo;
        }
    }

    /// <summary>
    /// Event args for protected process stopped event
    /// </summary>
    public class ProcessStoppedEventArgs : EventArgs
    {
        public string ProcessKey { get; private set; }

        public ProcessStoppedEventArgs(string processKey)
        {
            ProcessKey = processKey;
        }
    }
}
