# LazyChat - Agent Development Guide

## Project Overview

LazyChat is a **peer-to-peer LAN chat application** built with **.NET 10.0** and **Avalonia UI** (cross-platform XAML framework). It supports automatic peer discovery via UDP broadcast, real-time messaging via TCP, and file/image transfers using MessagePack serialization.

**Key characteristics:**
- **Language:** C# 12.0
- **Framework:** .NET 10.0 (with Avalonia 11.2.0 for cross-platform UI)
- **Architecture:** Interface-driven, dependency injection, event-based
- **Testing:** NUnit 4.2.2 + Moq 4.20.72
- **Serialization:** MessagePack 3.1.4 (6-7x faster than BinaryFormatter)
- **Primary language:** Simplified Chinese (UI and documentation)

---

## Essential Commands

### Build & Run
```bash
# Build the solution
dotnet build LazyChat.slnx

# Run the main application
dotnet run --project LazyChat/LazyChat.csproj

# Build in Release mode
dotnet build LazyChat.slnx -c Release
```

### Testing
```bash
# Run all tests
dotnet test LazyChat.Tests/LazyChat.Tests.csproj

# Run tests with detailed output
dotnet test LazyChat.Tests/LazyChat.Tests.csproj --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~PeerDiscoveryServiceTests"
```

### Clean & Restore
```bash
# Clean build artifacts
dotnet clean LazyChat.slnx

# Restore NuGet packages
dotnet restore LazyChat.slnx

# Full clean rebuild
dotnet clean && dotnet restore && dotnet build
```

---

## Project Structure

```
LazyChat/
├── LazyChat/                      # Main application
│   ├── Models/                    # Data models
│   │   ├── ChatModels.cs          # UI-facing chat models
│   │   ├── NetworkModels.cs       # Network protocol models (MessagePack)
│   │   └── HistoryModels.cs       # Chat history persistence models
│   ├── Services/                  # Business logic
│   │   ├── Interfaces/            # Service contracts
│   │   │   └── IServices.cs       # All service interfaces
│   │   ├── Infrastructure/        # Cross-cutting concerns
│   │   │   └── ServiceInfrastructure.cs  # Logger, NetworkAdapter
│   │   ├── PeerDiscoveryService.cs       # UDP broadcast peer discovery
│   │   ├── P2PCommunicationService.cs    # TCP messaging
│   │   ├── FileTransferService.cs        # File transfer with chunking
│   │   └── ChatHistoryStore.cs           # SQLite-based history
│   ├── ViewModels/                # MVVM ViewModels
│   │   ├── MainWindowViewModel.cs        # Main chat window logic
│   │   ├── FileTransferViewModel.cs      # File transfer dialog
│   │   └── InputDialogViewModel.cs       # Username input
│   ├── Views/                     # Avalonia XAML views
│   │   ├── MainWindow.axaml       # Main chat UI
│   │   ├── FileTransferWindow.axaml
│   │   └── InputDialog.axaml
│   ├── Exceptions/                # Custom exception hierarchy
│   │   └── CustomExceptions.cs    # LazyChatException, NetworkException, etc.
│   ├── App.axaml                  # Application entry XAML
│   ├── App.axaml.cs               # Application lifecycle
│   ├── Program.cs                 # Main entry point
│   └── LazyChat.csproj            # Project file
├── LazyChat.Tests/                # Test project
│   ├── Services/                  # Service unit tests
│   │   ├── PeerDiscoveryServiceTests.cs
│   │   └── MockHelpers.cs         # Test utilities
│   ├── Models/                    # Model tests
│   │   └── NetworkModelsTests.cs
│   └── LazyChat.Tests.csproj      # Test project file
├── LazyChat.slnx                  # Solution file (new .slnx format)
└── README.md                      # Chinese documentation
```

---

## Architecture & Design Patterns

### 1. Interface-Driven Design
**Every service has an interface in `IServices.cs`:**
- `IPeerDiscoveryService` - Peer discovery
- `IP2PCommunicationService` - Messaging
- `IFileTransferService` - File transfers
- `ILogger` - Logging abstraction
- `INetworkAdapter` - Network operations (for testing)

**Purpose:** Enables dependency injection and mocking for tests.

### 2. Dependency Injection via Constructor
```csharp
public class PeerDiscoveryService : IPeerDiscoveryService
{
    private readonly ILogger _logger;
    private readonly INetworkAdapter _networkAdapter;
    
    public PeerDiscoveryService(
        string userName, 
        int listeningPort, 
        ILogger logger = null,           // Optional - defaults to FileLogger
        INetworkAdapter networkAdapter = null)  // Optional - for testing
    {
        _logger = logger ?? new FileLogger();
        _networkAdapter = networkAdapter ?? new NetworkAdapter();
    }
}
```

### 3. Event-Based Communication
Services expose events for asynchronous notifications:
```csharp
public event EventHandler<PeerInfo> PeerDiscovered;
public event EventHandler<NetworkMessage> MessageReceived;
public event EventHandler<string> ErrorOccurred;
```

ViewModels subscribe to these events to update UI.

### 4. MVVM Pattern (Avalonia)
- **Views** (XAML) - Declarative UI
- **ViewModels** - UI logic, data binding, commands
- **Models** - Data structures

Commands use `RelayCommand` pattern for button bindings.

---

## Code Conventions

### Naming Conventions
- **Private fields:** `_camelCase` (e.g., `_logger`, `_discoveredPeers`)
- **Public properties:** `PascalCase` (e.g., `IsRunning`, `OnlinePeers`)
- **Local variables:** `camelCase`
- **Constants:** `UPPER_SNAKE_CASE` (e.g., `DISCOVERY_PORT`)
- **Interfaces:** `IInterfaceName` (e.g., `IPeerDiscoveryService`)
- **Event handlers:** `On[EventName]` (e.g., `OnPeerDiscovered`)

### File Organization
- **One class per file** (exception: small helper classes)
- **Namespace matches directory structure**
- **Interfaces in separate `Interfaces/` folder**

### Language Features
- **C# 12.0 features used:**
  - Primary constructors (minimal usage)
  - Pattern matching
  - Null-coalescing operators
- **Disabled features:**
  - `Nullable` disabled (compatibility reasons)
  - `ImplicitUsings` disabled (explicit imports required)

### Import Style
Always use explicit `using` statements at the top of files:
```csharp
using System;
using System.Collections.Generic;
using System.Net;
using LazyChat.Models;
using LazyChat.Services.Interfaces;
```

### Logging
Log at appropriate levels:
```csharp
_logger.LogInfo("Service started");
_logger.LogWarning("Port might be in use");
_logger.LogError("Failed to connect", exception);
_logger.LogDebug("Broadcasted to 192.168.1.255");  // Only in DEBUG builds
```

**Log file location:** `%LocalAppData%/LazyChat/logs/lazychat_YYYYMMDD.log`

---

## Testing Approach

### Test Framework
- **NUnit 4.2.2** - Test framework
- **Moq 4.20.72** - Mocking framework
- **Convention:** Arrange-Act-Assert (AAA) pattern

### Test Structure
```csharp
[TestFixture]
public class MyServiceTests
{
    private TestLogger _logger;
    private MyService _service;
    
    [SetUp]
    public void SetUp()
    {
        _logger = new TestLogger();
        _service = new MyService(_logger);
    }
    
    [Test]
    public void MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var input = "test";
        
        // Act
        var result = _service.Process(input);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo("success"));
    }
    
    [TearDown]
    public void TearDown()
    {
        _service?.Dispose();
    }
}
```

### Test Naming
Format: `MethodName_Scenario_ExpectedResult`
- `Start_ValidConfiguration_LogsStartupMessage`
- `SendMessage_NullMessage_ThrowsException`
- `GetOnlinePeers_Initially_ReturnsEmptyList`

### Mock Helpers
Use `TestLogger` and `TestNetworkAdapter` from `MockHelpers.cs` for testing.

### Testing Best Practices
- **Test one thing per test**
- **Always dispose resources in `[TearDown]`**
- **Use `TestLogger` to verify logging behavior**
- **Mock network dependencies** to avoid flaky tests
- **Test exception paths** as well as happy paths

---

## Network Protocol

### Discovery Protocol (UDP Broadcast)
- **Port:** 8888
- **Broadcast interval:** 5 seconds
- **Peer timeout:** 15 seconds
- **Message types:** `Discovery`, `DiscoveryResponse`, `UserJoined`, `UserLeft`

### Communication Protocol (TCP)
- **Port:** 9999 (configurable)
- **Serialization:** MessagePack
- **Message types:** `TextMessage`, `ImageMessage`, `FileTransferRequest`, etc.

### Multi-Interface Support (Important!)
`PeerDiscoveryService` now supports:
- **Multi-NIC broadcasting** - Broadcasts on all physical interfaces
- **VPN interface filtering** - Skips virtual/VPN adapters
- **Peer caching** - Persists peer addresses to `%LocalAppData%/LazyChat/peer_cache.dat`
- **Unicast probing** - Falls back to direct peer probing every 10 seconds

**See `PEER_DISCOVERY_IMPROVEMENTS.md` for detailed explanation.**

### File Transfer
- **Chunk size:** 64KB (65536 bytes)
- **Progress updates:** Real-time via events
- **Error handling:** Automatic retry on failure

---

## Custom Exception Hierarchy

```
LazyChatException                  (Base)
├── NetworkException               (Network operations)
├── FileTransferException          (File transfers)
├── PeerDiscoveryException         (Peer discovery)
└── MessageSerializationException  (MessagePack errors)
```

**Usage:**
```csharp
try
{
    // Network operation
}
catch (SocketException ex)
{
    throw new NetworkException("Failed to connect", ex, "Connect");
}
```

**Always:**
- Wrap low-level exceptions in domain exceptions
- Log before throwing
- Include context (operation name, file ID, etc.)

---

## Important Gotchas

### 1. PeerId Consistency Issue
**Problem:** PeerInfo generates a new GUID on construction, causing peer identity inconsistency.

**Solution (recent fix):** Ensure PeerId is generated once and preserved. When deserializing peer info from cache or network, reuse existing PeerId.

**Reference:** Commit `d24d0c0` - "fix: maybe fix the PeerId not consistant issue"

### 2. MessagePack Serialization
All network models MUST be decorated with MessagePack attributes:
```csharp
[MessagePackObject]
public class NetworkMessage
{
    [Key(0)]
    public MessageType Type { get; set; }
    
    [Key(1)]
    public string SenderId { get; set; }
    
    [IgnoreMember]  // For non-serialized properties
    public IPAddress IpAddress { get; set; }
}
```

**Key numbers must be sequential and stable** - never reorder or skip.

### 3. UI Thread Marshalling (Avalonia)
ViewModels update ObservableCollections from background threads. Use Dispatcher:
```csharp
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
    OnlinePeers.Add(newPeer);
});
```

### 4. Resource Disposal
All services implement `IDisposable`. Always:
- Stop background threads in `Dispose()`
- Close sockets/streams
- Unsubscribe from events to prevent memory leaks

### 5. Test-Only Interfaces
`INetworkAdapter` exists solely for testing - it allows mocking network operations without actual network calls.

### 6. SQLite Persistence (Recent Addition)
`ChatHistoryStore` uses Microsoft.Data.Sqlite for chat history. Connection string pattern:
```csharp
string dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "LazyChat", "chat_history.db");
```

### 7. Chinese Language UI
All user-facing strings are in Simplified Chinese. When adding UI elements:
- Use Chinese for labels, button text, tooltips
- Use Chinese for log messages visible to users
- Use English for internal logging (DEBUG level)

---

## Common Development Tasks

### Adding a New Message Type
1. Add enum value to `MessageType` in `NetworkModels.cs`
2. Add validation logic to `NetworkMessage.Validate()`
3. Handle in `P2PCommunicationService.ProcessMessage()`
4. Add test cases in `LazyChat.Tests`

### Adding a New Service
1. Define interface in `Services/Interfaces/IServices.cs`
2. Implement service in `Services/MyService.cs`
3. Add constructor dependency injection support
4. Create test file `LazyChat.Tests/Services/MyServiceTests.cs`
5. Wire into `MainWindowViewModel` if needed

### Adding a New View
1. Create XAML file in `Views/` (e.g., `MyWindow.axaml`)
2. Create ViewModel in `ViewModels/` (e.g., `MyWindowViewModel.cs`)
3. Implement `INotifyPropertyChanged` for data binding
4. Use `RelayCommand` for button/menu commands
5. Set `x:DataType` attribute in XAML for compile-time binding checks

### Debugging Network Issues
1. Check logs in `%LocalAppData%/LazyChat/logs/`
2. Look for `[INFO] Active interface: ...` lines to see which NICs are used
3. Check `[DEBUG] Broadcasted on ...` to verify broadcast sending
4. Inspect peer cache at `%LocalAppData%/LazyChat/peer_cache.dat`
5. Use Wireshark to capture UDP port 8888 and TCP port 9999

---

## Dependencies & NuGet Packages

### Main Application (LazyChat.csproj)
- **Avalonia 11.2.0** - Cross-platform XAML UI framework
  - `Avalonia.Desktop` - Desktop platform support
  - `Avalonia.Themes.Fluent` - Fluent design theme
  - `Avalonia.Fonts.Inter` - Inter font family
  - `Avalonia.Diagnostics` - DEBUG-only UI inspector
- **MessagePack 3.1.4** - High-performance serialization
- **System.Drawing.Common 9.0.0** - Cross-platform image handling
- **Microsoft.Data.Sqlite 9.0.0** - SQLite database (for chat history)

### Test Project (LazyChat.Tests.csproj)
- **NUnit 4.2.2** - Test framework
- **NUnit3TestAdapter 4.6.0** - Visual Studio test adapter
- **Microsoft.NET.Test.Sdk 17.11.1** - Test SDK
- **Moq 4.20.72** - Mocking framework

**Note:** Uses `net10.0-windows` target for test project (requires Windows Forms for some test scenarios).

---

## Performance Characteristics

### MessagePack vs BinaryFormatter
- **Serialization speed:** ~6-7x faster
- **Payload size:** ~70% smaller
- **CPU usage:** ~85% reduction
- **Safety:** No arbitrary type deserialization vulnerabilities

### Target Performance Metrics
- **Startup time:** < 5 seconds
- **Message latency:** < 100ms (same LAN)
- **File throughput:** ~10 MB/s (network-dependent)
- **Memory footprint:** < 50MB (idle state)
- **Concurrent peers:** 50+ (typical LAN)

### Network Optimization
- UDP broadcast limited to 5-second intervals (avoid flooding)
- Unicast peer probing at 10-second intervals (cache only)
- TCP keep-alive for established connections
- Chunk-based file transfer (64KB chunks) for progress feedback

---

## Security Considerations

### Current State (LAN-Only)
- **No encryption** - All traffic is plaintext
- **No authentication** - Trust-on-first-use model
- **No access control** - Any peer can send any message
- **Broadcast-based discovery** - Vulnerable to spoofing

### Mitigation Strategies
- **Firewall rules:** Only allow UDP 8888 and TCP 9999 on LAN interface
- **File validation:** Check file size and MIME type before accepting
- **No port forwarding** - Never expose to public internet

### Future Security Roadmap (from README.md)
- AES encryption for messages (v1.1)
- Authentication tokens (v1.2)
- Digital signatures for file integrity (v2.0)

---

## Platform-Specific Notes

### Windows
- Default NuGet source configured in `NuGet.Config`
- Log location: `%LocalAppData%\LazyChat\logs\`
- Peer cache: `%LocalAppData%\LazyChat\peer_cache.dat`

### macOS
- Uses CoreCLR runtime
- Log location: `~/Library/Application Support/LazyChat/logs/`
- Peer cache: `~/Library/Application Support/LazyChat/peer_cache.dat`
- Font fallback: "PingFang SC" for Chinese characters

### Linux
- Requires Avalonia Linux dependencies: `libx11-dev`, `libice-dev`, `libsm-dev`
- Log location: `~/.local/share/LazyChat/logs/`
- Font fallback: "Noto Sans CJK SC" or "WenQuanYi Micro Hei"

---

## Known Issues & Limitations

### Current Known Issues
1. **PeerId consistency** - Recently addressed (commit d24d0c0), may need further validation
2. **UI update lag** - Peer connection status may not reflect immediately (commit 38f34bf mentions fix)
3. **No IPv6 support** - Only IPv4 addresses are handled
4. **No mDNS/Bonjour** - Could improve discovery on Apple devices

### Current Limitations
- **LAN-only** - No NAT traversal or relay servers
- **No group chat** - Only 1-on-1 conversations
- **No encryption** - Plaintext communication
- **No message history sync** - Each device stores history locally
- **No offline messages** - Messages only delivered if recipient is online

### Planned Improvements (from PEER_DISCOVERY_IMPROVEMENTS.md)
- Manual peer addition via IP address
- mDNS/Bonjour support for better Apple device discovery
- IPv6 multicast support
- NAT traversal for cross-subnet scenarios

---

## Development Workflow (TDD Recommended)

### Step 1: Write Test First
```csharp
[Test]
public void NewFeature_Scenario_ExpectedResult()
{
    // Arrange
    var service = new MyService(new TestLogger());
    
    // Act
    var result = service.NewFeature();
    
    // Assert
    Assert.That(result, Is.EqualTo(expected));
}
```

### Step 2: Implement Minimal Code
Make the test pass with simplest implementation.

### Step 3: Refactor
Improve code quality while keeping tests green.

### Step 4: Integration Test (if needed)
Test interaction between services.

### Step 5: Manual Test
Run the app and verify UI behavior.

---

## Git Workflow

### Branching
- `master` - Main development branch
- Feature branches: `feature/feature-name`
- Bug fixes: `fix/issue-description`

### Commit Message Style (from recent commits)
- `fix: description` - Bug fixes
- `refactor: description` - Code improvements
- `feat: description` - New features
- `test: description` - Test additions
- `docs: description` - Documentation updates

### Example Recent Commits
```
d24d0c0 fix: maybe fix the PeerId not consistant issue
ab2d77c refactor: more robust peer discovery
38f34bf fix: ui do not update when peer connected.
```

---

## Troubleshooting

### Build Errors

**Error: "The type or namespace name 'Avalonia' could not be found"**
- Run `dotnet restore LazyChat.slnx`
- Check NuGet.Config is present

**Error: "The target framework 'net10.0' is not supported"**
- Verify .NET 10.0 SDK is installed: `dotnet --version`
- Update SDK if < 10.0

### Test Errors

**Error: "Port 8888 is already in use"**
- Stop any running LazyChat instances
- Check for zombie processes: `netstat -an | grep 8888`
- Use mock network adapter in tests

**Tests fail randomly**
- Network-dependent tests should use mocks
- Check test isolation (ensure proper cleanup in TearDown)

### Runtime Errors

**Error: "Unable to bind to port 9999"**
- Check if port is in use: `netstat -an | grep 9999`
- Run as administrator (Windows) or with proper permissions
- Try different port in configuration

**Peers not discovered**
- Check firewall allows UDP 8888
- Verify devices are on same subnet
- Look for "Active interface" log entries
- Check if VPN is interfering (see PEER_DISCOVERY_IMPROVEMENTS.md)

**File transfer stuck at 0%**
- Check disk space on receiving end
- Verify TCP port 9999 is not blocked
- Look for errors in log file

---

## Quick Reference

### Key Ports
- **UDP 8888** - Peer discovery (broadcast)
- **TCP 9999** - P2P communication (configurable)

### Key Directories
- **Logs:** `%LocalAppData%/LazyChat/logs/`
- **Peer cache:** `%LocalAppData%/LazyChat/peer_cache.dat`
- **Database:** `%LocalAppData%/LazyChat/chat_history.db`

### Key Classes
- `PeerDiscoveryService` - Peer discovery logic
- `P2PCommunicationService` - Message exchange
- `FileTransferService` - File transfer chunking
- `ChatHistoryStore` - SQLite persistence
- `MainWindowViewModel` - Main UI controller

### Key Interfaces
- `IPeerDiscoveryService` - Discovery contract
- `IP2PCommunicationService` - Messaging contract
- `IFileTransferService` - File transfer contract
- `ILogger` - Logging abstraction
- `INetworkAdapter` - Network operations (test mock point)

### Key Models
- `NetworkMessage` - Wire format (MessagePack)
- `PeerInfo` - Peer metadata
- `ChatMessage` - UI-facing message
- `FileTransferInfo` - Transfer state

---

## Additional Documentation

- **README.md** - User-facing documentation (Chinese)
- **TESTING.md** - Testing guidelines (may exist, check file)
- **PEER_DISCOVERY_IMPROVEMENTS.md** - Detailed peer discovery architecture
- **Code comments** - XML doc comments on all public APIs

---

## Contact & Contribution

### Reporting Issues
File issues with:
- **Expected behavior**
- **Actual behavior**
- **Steps to reproduce**
- **Log file excerpt** (from `%LocalAppData%/LazyChat/logs/`)
- **Environment:** OS, .NET version, network setup

### Contributing
Follow TDD workflow:
1. Write failing test
2. Implement feature
3. Ensure all tests pass
4. Update documentation
5. Submit PR with clear description

---

**Last updated:** 2025-12-22  
**Codebase version:** Based on git commit `d24d0c0`  
**Agent note:** This file is automatically generated. Update when major architectural changes occur.
