using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Moq;
using LazyChat.Models;
using LazyChat.Services;
using LazyChat.Services.Interfaces;
using LazyChat.Exceptions;

namespace LazyChat.Tests.Services
{
    [TestFixture]
    public class PeerDiscoveryServiceTests
    {
        private TestLogger _logger;
        private TestNetworkAdapter _networkAdapter;

        [SetUp]
        public void SetUp()
        {
            _logger = new TestLogger();
            _networkAdapter = new TestNetworkAdapter();
        }

        [Test]
        public void Constructor_ValidParameters_Success()
        {
            // Arrange & Act
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.IsRunning, Is.False);
        }

        [Test]
        public void Constructor_EmptyUserName_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                new PeerDiscoveryService("", 9999, _logger, _networkAdapter));
        }

        [Test]
        public void Constructor_NullUserName_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                new PeerDiscoveryService(null, 9999, _logger, _networkAdapter));
        }

        [Test]
        public void Constructor_InvalidPort_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new PeerDiscoveryService("TestUser", 0, _logger, _networkAdapter));
            
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new PeerDiscoveryService("TestUser", -1, _logger, _networkAdapter));
            
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new PeerDiscoveryService("TestUser", 70000, _logger, _networkAdapter));
        }

        [Test]
        public void GetLocalPeer_ReturnsCorrectInformation()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);

            // Act
            var localPeer = service.GetLocalPeer();

            // Assert
            Assert.That(localPeer, Is.Not.Null);
            Assert.That(localPeer.UserName, Is.EqualTo("TestUser"));
            Assert.That(localPeer.Port, Is.EqualTo(9999));
            Assert.That(localPeer.PeerId, Is.Not.Null);
            Assert.That(localPeer.IpAddress.ToString(), Is.EqualTo("192.168.1.100"));
        }

        [Test]
        public void GetOnlinePeers_Initially_ReturnsEmptyList()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);

            // Act
            var peers = service.GetOnlinePeers();

            // Assert
            Assert.That(peers, Is.Not.Null);
            Assert.That(peers.Count, Is.EqualTo(0));
        }

        [Test]
        public void Start_LogsStartupMessage()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);

            // Act
            try
            {
                service.Start();
                Thread.Sleep(100); // Give it time to initialize
            }
            finally
            {
                service.Stop();
            }

            // Assert
            Assert.That(_logger.InfoLogs.Any(l => l.Contains("initialized")), Is.True);
        }

        [Test]
        public void Stop_WhenNotRunning_DoesNotThrow()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);

            // Act & Assert
            Assert.DoesNotThrow(() => service.Stop());
        }

        [Test]
        public void Dispose_StopsService()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);

            // Act
            service.Dispose();

            // Assert
            Assert.That(service.IsRunning, Is.False);
            Assert.That(_logger.InfoLogs.Any(l => l.Contains("disposed")), Is.True);
        }

        [Test]
        public void PeerDiscovered_Event_CanBeSubscribed()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);
            bool eventFired = false;
            PeerInfo discoveredPeer = null;

            service.PeerDiscovered += (sender, peer) =>
            {
                eventFired = true;
                discoveredPeer = peer;
            };

            // Act - Event will be tested in integration tests
            // For unit test, just verify subscription works
            
            // Assert
            Assert.That(eventFired, Is.False); // No peers discovered yet
        }

        [Test]
        public void PeerLeft_Event_CanBeSubscribed()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);
            bool eventFired = false;

            service.PeerLeft += (sender, peer) =>
            {
                eventFired = true;
            };

            // Assert
            Assert.That(eventFired, Is.False); // No peers left yet
        }

        [Test]
        public void ErrorOccurred_Event_CanBeSubscribed()
        {
            // Arrange
            var service = new PeerDiscoveryService("TestUser", 9999, _logger, _networkAdapter);
            bool eventFired = false;
            string errorMessage = null;

            service.ErrorOccurred += (sender, error) =>
            {
                eventFired = true;
                errorMessage = error;
            };

            // Assert
            Assert.That(eventFired, Is.False); // No errors yet
        }

        [Test]
        public void MultipleInstances_CanBeCreated()
        {
            // Arrange & Act
            var service1 = new PeerDiscoveryService("User1", 9999, new TestLogger(), new TestNetworkAdapter());
            var service2 = new PeerDiscoveryService("User2", 10000, new TestLogger(), new TestNetworkAdapter());

            // Assert
            Assert.That(service1.GetLocalPeer().PeerId, Is.Not.EqualTo(service2.GetLocalPeer().PeerId));
            Assert.That(service1.GetLocalPeer().UserName, Is.EqualTo("User1"));
            Assert.That(service2.GetLocalPeer().UserName, Is.EqualTo("User2"));

            // Cleanup
            service1.Dispose();
            service2.Dispose();
        }

        [Test]
        public void Constructor_WithoutLogger_UsesDefault()
        {
            // Arrange & Act
            var service = new PeerDiscoveryService("TestUser", 9999);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service.GetLocalPeer(), Is.Not.Null);

            // Cleanup
            service.Dispose();
        }

        [Test]
        public void NetworkMessage_Discovery_Validation()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.Discovery,
                SenderId = "test-peer",
                SenderName = "Test Peer",
                TextContent = "9999"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_UserLeft_Validation()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.UserLeft,
                SenderId = "test-peer",
                SenderName = "Test Peer"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => message.Validate());
        }
    }
}
