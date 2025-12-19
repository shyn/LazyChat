using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Moq;
using LazyChat.Services.Interfaces;

namespace LazyChat.Tests.Services
{
    /// <summary>
    /// Mock helpers for testing
    /// </summary>
    public static class MockHelpers
    {
        /// <summary>
        /// Creates a mock logger that tracks all log calls
        /// </summary>
        public static Mock<ILogger> CreateMockLogger()
        {
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(l => l.LogInfo(It.IsAny<string>()));
            mockLogger.Setup(l => l.LogWarning(It.IsAny<string>()));
            mockLogger.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()));
            mockLogger.Setup(l => l.LogDebug(It.IsAny<string>()));
            return mockLogger;
        }

        /// <summary>
        /// Creates a mock network adapter for testing
        /// </summary>
        public static Mock<INetworkAdapter> CreateMockNetworkAdapter()
        {
            var mockAdapter = new Mock<INetworkAdapter>();
            mockAdapter.Setup(a => a.GetLocalIPAddress())
                .Returns(IPAddress.Parse("192.168.1.100"));
            
            return mockAdapter;
        }

        /// <summary>
        /// Creates a mock file system for testing
        /// </summary>
        public static Mock<IFileSystem> CreateMockFileSystem()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            return mockFileSystem;
        }

        /// <summary>
        /// Creates a mock communication service
        /// </summary>
        public static Mock<IP2PCommunicationService> CreateMockCommunicationService()
        {
            var mockCommService = new Mock<IP2PCommunicationService>();
            mockCommService.Setup(c => c.IsRunning).Returns(true);
            return mockCommService;
        }
    }

    /// <summary>
    /// In-memory logger for testing that captures all log messages
    /// </summary>
    public class TestLogger : ILogger
    {
        public List<string> InfoLogs { get; } = new List<string>();
        public List<string> WarningLogs { get; } = new List<string>();
        public List<string> ErrorLogs { get; } = new List<string>();
        public List<string> DebugLogs { get; } = new List<string>();

        public void LogInfo(string message)
        {
            InfoLogs.Add(message);
        }

        public void LogWarning(string message)
        {
            WarningLogs.Add(message);
        }

        public void LogError(string message, Exception exception = null)
        {
            ErrorLogs.Add(message + (exception != null ? $": {exception.Message}" : ""));
        }

        public void LogDebug(string message)
        {
            DebugLogs.Add(message);
        }

        public void Clear()
        {
            InfoLogs.Clear();
            WarningLogs.Clear();
            ErrorLogs.Clear();
            DebugLogs.Clear();
        }
    }

    /// <summary>
    /// Mock network adapter for testing
    /// </summary>
    public class TestNetworkAdapter : INetworkAdapter
    {
        public IPAddress LocalIPAddress { get; set; } = IPAddress.Parse("192.168.1.100");

        public IPAddress GetLocalIPAddress()
        {
            return LocalIPAddress;
        }

        public void CreateUdpClient(int port, out UdpClient client)
        {
            client = new UdpClient();
        }

        public void CreateTcpListener(int port, out TcpListener listener)
        {
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void CreateTcpClient(out TcpClient client)
        {
            client = new TcpClient();
        }
    }
}
