using System;
using System.IO;
using LazyChat.Services.Interfaces;

namespace LazyChat.Services.Infrastructure
{
    /// <summary>
    /// File-based logger implementation
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private readonly bool _enableConsole;

        public FileLogger(string logFilePath = null, bool enableConsole = true)
        {
            _logFilePath = logFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyChat",
                "logs",
                $"lazychat_{DateTime.Now:yyyyMMdd}.log");

            _enableConsole = enableConsole;

            string logDir = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        public void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public void LogWarning(string message)
        {
            Log("WARN", message);
        }

        public void LogError(string message, Exception exception = null)
        {
            string fullMessage = message;
            if (exception != null)
            {
                fullMessage += $"\nException: {exception.GetType().Name}\nMessage: {exception.Message}\nStackTrace: {exception.StackTrace}";
            }
            Log("ERROR", fullMessage);
        }

        public void LogDebug(string message)
        {
#if DEBUG
            Log("DEBUG", message);
#endif
        }

        private void Log(string level, string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

            lock (_lockObject)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
                catch
                {
                    // Fail silently if logging fails
                }
            }

            if (_enableConsole)
            {
                Console.WriteLine(logMessage);
            }
        }
    }

    /// <summary>
    /// In-memory logger for testing
    /// </summary>
    public class MemoryLogger : ILogger
    {
        public System.Collections.Generic.List<string> Logs { get; } = new System.Collections.Generic.List<string>();

        public void LogInfo(string message)
        {
            Logs.Add($"INFO: {message}");
        }

        public void LogWarning(string message)
        {
            Logs.Add($"WARN: {message}");
        }

        public void LogError(string message, Exception exception = null)
        {
            Logs.Add($"ERROR: {message}" + (exception != null ? $" - {exception.Message}" : ""));
        }

        public void LogDebug(string message)
        {
            Logs.Add($"DEBUG: {message}");
        }

        public void Clear()
        {
            Logs.Clear();
        }
    }

    /// <summary>
    /// Real network adapter implementation
    /// </summary>
    public class NetworkAdapter : INetworkAdapter
    {
        public System.Net.IPAddress GetLocalIPAddress()
        {
            try
            {
                string hostName = System.Net.Dns.GetHostName();
                System.Net.IPHostEntry hostEntry = System.Net.Dns.GetHostEntry(hostName);

                foreach (System.Net.IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && 
                        !System.Net.IPAddress.IsLoopback(ip))
                    {
                        return ip;
                    }
                }
            }
            catch
            {
            }

            return System.Net.IPAddress.Loopback;
        }

        public void CreateUdpClient(int port, out System.Net.Sockets.UdpClient client)
        {
            client = new System.Net.Sockets.UdpClient(port);
        }

        public void CreateTcpListener(int port, out System.Net.Sockets.TcpListener listener)
        {
            listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
        }

        public void CreateTcpClient(out System.Net.Sockets.TcpClient client)
        {
            client = new System.Net.Sockets.TcpClient();
        }
    }

    /// <summary>
    /// Real file system implementation
    /// </summary>
    public class FileSystemAdapter : IFileSystem
    {
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            File.WriteAllBytes(path, data);
        }

        public FileStream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public FileStream OpenWrite(string path)
        {
            return new FileStream(path, FileMode.Create, FileAccess.Write);
        }

        public FileInfo GetFileInfo(string path)
        {
            return new FileInfo(path);
        }
    }
}
