using System;

namespace MazSvc.Models
{
    /// <summary>
    /// Represents a protected executable that the service monitors
    /// </summary>
    [Serializable]
    public class ProtectedExecutable
    {
        /// <summary>
        /// The executable file name (e.g., "notepad.exe")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The full path to the executable (e.g., "C:\Windows\notepad.exe")
        /// </summary>
        public string FullPath { get; set; }

        public ProtectedExecutable()
        {
        }

        public ProtectedExecutable(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
}
