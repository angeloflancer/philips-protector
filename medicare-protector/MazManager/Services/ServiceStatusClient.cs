using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;

namespace MazManager.Services
{
    /// <summary>
    /// Client for communicating with MazSvc service via named pipe
    /// Gets running process status from the service
    /// </summary>
    public class ServiceStatusClient
    {
        private const string PIPE_NAME = "MazSvcStatusPipe";
        private const int CONNECT_TIMEOUT_MS = 1000;

        /// <summary>
        /// Gets the list of running protected processes from the service
        /// </summary>
        /// <returns>List of running process info, or empty list if service unavailable</returns>
        public List<RunningProcessInfo> GetRunningProcesses()
        {
            List<RunningProcessInfo> processes = new List<RunningProcessInfo>();

            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut))
                {
                    pipeClient.Connect(CONNECT_TIMEOUT_MS);
                    pipeClient.ReadMode = PipeTransmissionMode.Message;

                    // Send request
                    byte[] request = Encoding.UTF8.GetBytes("GET_STATUS");
                    pipeClient.Write(request, 0, request.Length);
                    pipeClient.Flush();

                    // Read response
                    byte[] responseBuffer = new byte[4096];
                    int bytesRead = pipeClient.Read(responseBuffer, 0, responseBuffer.Length);
                    string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                    // Parse JSON response
                    processes = ParseJsonResponse(response);
                }
            }
            catch
            {
                // Service not available or pipe error - return empty list
            }

            return processes;
        }

        /// <summary>
        /// Checks if the service is responding
        /// </summary>
        public bool IsServiceResponding()
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut))
                {
                    pipeClient.Connect(CONNECT_TIMEOUT_MS);
                    pipeClient.ReadMode = PipeTransmissionMode.Message;

                    // Send ping request
                    byte[] request = Encoding.UTF8.GetBytes("PING");
                    pipeClient.Write(request, 0, request.Length);
                    pipeClient.Flush();

                    // Read response
                    byte[] responseBuffer = new byte[256];
                    int bytesRead = pipeClient.Read(responseBuffer, 0, responseBuffer.Length);
                    string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead).Trim();

                    return response == "PONG";
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses JSON array of running processes
        /// </summary>
        private List<RunningProcessInfo> ParseJsonResponse(string json)
        {
            List<RunningProcessInfo> processes = new List<RunningProcessInfo>();

            if (string.IsNullOrEmpty(json) || json == "[]")
            {
                return processes;
            }

            try
            {
                // Simple JSON parser for our specific format
                int index = 0;
                while (index < json.Length)
                {
                    // Find next object start
                    int objStart = json.IndexOf('{', index);
                    if (objStart < 0) break;

                    int objEnd = json.IndexOf('}', objStart);
                    if (objEnd < 0) break;

                    string objContent = json.Substring(objStart + 1, objEnd - objStart - 1);

                    // Parse fields
                    int processId = ExtractJsonInt(objContent, "ProcessId");
                    string processName = ExtractJsonString(objContent, "ProcessName");
                    string fullPath = ExtractJsonString(objContent, "FullPath");

                    if (processId > 0 && !string.IsNullOrEmpty(fullPath))
                    {
                        RunningProcessInfo info = new RunningProcessInfo();
                        info.ProcessId = processId;
                        info.ProcessName = processName ?? "";
                        info.FullPath = UnescapeJsonString(fullPath);
                        processes.Add(info);
                    }

                    index = objEnd + 1;
                }
            }
            catch
            {
                // Parse error - return empty list
            }

            return processes;
        }

        private int ExtractJsonInt(string json, string key)
        {
            string searchKey = "\"" + key + "\":";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0) return 0;

            int valueStart = keyIndex + searchKey.Length;
            int valueEnd = valueStart;

            while (valueEnd < json.Length && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '-'))
            {
                valueEnd++;
            }

            if (valueEnd > valueStart)
            {
                string valueStr = json.Substring(valueStart, valueEnd - valueStart);
                int result;
                if (int.TryParse(valueStr, out result))
                {
                    return result;
                }
            }

            return 0;
        }

        private string ExtractJsonString(string json, string key)
        {
            string searchKey = "\"" + key + "\":\"";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0) return null;

            int valueStart = keyIndex + searchKey.Length;
            int valueEnd = valueStart;

            // Find the end of the value (unescaped quote)
            while (valueEnd < json.Length)
            {
                if (json[valueEnd] == '"' && (valueEnd == valueStart || json[valueEnd - 1] != '\\'))
                {
                    break;
                }
                valueEnd++;
            }

            if (valueEnd > valueStart)
            {
                return json.Substring(valueStart, valueEnd - valueStart);
            }

            return null;
        }

        private string UnescapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < value.Length)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    char next = value[i + 1];
                    switch (next)
                    {
                        case '\\': sb.Append('\\'); i += 2; break;
                        case '"': sb.Append('"'); i += 2; break;
                        case 'n': sb.Append('\n'); i += 2; break;
                        case 'r': sb.Append('\r'); i += 2; break;
                        case 't': sb.Append('\t'); i += 2; break;
                        default: sb.Append(value[i]); i++; break;
                    }
                }
                else
                {
                    sb.Append(value[i]);
                    i++;
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Information about a running protected process
    /// </summary>
    public class RunningProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string FullPath { get; set; }
    }
}
