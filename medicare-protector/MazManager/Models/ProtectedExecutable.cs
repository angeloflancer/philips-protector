using System;
using System.ComponentModel;

namespace MazManager.Models
{
    /// <summary>
    /// Status of a protected executable
    /// </summary>
    public enum ExecutableStatus
    {
        /// <summary>
        /// Default state - not running
        /// </summary>
        Normal,

        /// <summary>
        /// Process is running with valid license
        /// </summary>
        Running,

        /// <summary>
        /// Directory has been encrypted due to license violation
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents a protected executable that the service monitors
    /// </summary>
    [Serializable]
    public class ProtectedExecutable : INotifyPropertyChanged
    {
        private string _name;
        private string _fullPath;
        private ExecutableStatus _status;

        /// <summary>
        /// The executable file name (e.g., "notepad.exe")
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        /// <summary>
        /// The full path to the executable (e.g., "C:\Windows\notepad.exe")
        /// </summary>
        public string FullPath
        {
            get { return _fullPath; }
            set
            {
                if (_fullPath != value)
                {
                    _fullPath = value;
                    OnPropertyChanged("FullPath");
                }
            }
        }

        /// <summary>
        /// Current status of the executable
        /// </summary>
        public ExecutableStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged("Status");
                    OnPropertyChanged("StatusText");
                    OnPropertyChanged("StatusColor");
                }
            }
        }

        /// <summary>
        /// Status text for display
        /// </summary>
        public string StatusText
        {
            get
            {
                switch (_status)
                {
                    case ExecutableStatus.Running:
                        return "Running";
                    case ExecutableStatus.Critical:
                        return "Encrypted";
                    default:
                        return "Normal";
                }
            }
        }

        /// <summary>
        /// Status color for display (hex color string)
        /// </summary>
        public string StatusColor
        {
            get
            {
                switch (_status)
                {
                    case ExecutableStatus.Running:
                        return "#4CAF50"; // Green
                    case ExecutableStatus.Critical:
                        return "#F44336"; // Red
                    default:
                        return "#757575"; // Gray
                }
            }
        }

        public ProtectedExecutable()
        {
            _status = ExecutableStatus.Normal;
        }

        public ProtectedExecutable(string name, string fullPath)
        {
            _name = name;
            _fullPath = fullPath;
            _status = ExecutableStatus.Normal;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
