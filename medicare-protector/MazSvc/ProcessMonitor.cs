using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Monitors running processes and detects protected executables
    /// </summary>
    public class ProcessMonitor
    {
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
                using (System.Management.ManagementObjectSearcher searcher =
                    new System.Management.ManagementObjectSearcher(
                        string.Format("SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {0}", processId)))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
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
    }
}
