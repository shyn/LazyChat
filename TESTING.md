# LazyChat - Testing Documentation

## Test Architecture

LazyChat follows **Test-Driven Development (TDD)** principles with a focus on:
- **Unit Testing**: Testing individual components in isolation
- **Integration Testing**: Testing component interactions
- **Mocking**: Using interfaces to enable testability
- **Code Coverage**: Aiming for high test coverage of business logic

## Testing Framework

- **NUnit 3.14**: Primary testing framework
- **Moq 4.18**: Mocking framework for dependency injection
- **Castle.Core**: Required by Moq

## Project Structure

```
LazyChat/
©À©¤©¤ Models/                    # Data models with validation
©À©¤©¤ Services/
©¦   ©À©¤©¤ Interfaces/           # Service abstractions (IDiscoveryService, etc.)
©¦   ©À©¤©¤ Infrastructure/       # Cross-cutting concerns (logging, adapters)
©¦   ©¸©¤©¤ *.cs                  # Service implementations
©À©¤©¤ Exceptions/               # Custom exception types
©¸©¤©¤ Controls/                 # UI components

LazyChat.Tests/
©À©¤©¤ Models/                   # Model tests (validation, serialization)
©À©¤©¤ Services/                 # Service tests (business logic)
©¦   ©¸©¤©¤ MockHelpers.cs       # Test utilities and mocks
©¸©¤©¤ Integration/              # Integration tests (coming soon)
```

## Design Principles

### 1. Dependency Injection
All services accept their dependencies through constructors:

```csharp
public PeerDiscoveryService(
    string userName, 
    int listeningPort, 
    ILogger logger = null,
    INetworkAdapter networkAdapter = null)
```

### 2. Interface-Based Design
Services implement interfaces enabling:
- Easy mocking in tests
- Loose coupling
- Dependency inversion

Key interfaces:
- `IPeerDiscoveryService` - Peer discovery
- `IP2PCommunicationService` - P2P messaging
- `IFileTransferService` - File transfers
- `ILogger` - Logging
- `INetworkAdapter` - Network operations (testable)
- `IFileSystem` - File I/O (testable)

### 3. Robust Error Handling
Custom exception hierarchy:
- `LazyChatException` - Base exception
- `NetworkException` - Network failures
- `FileTransferException` - Transfer failures
- `PeerDiscoveryException` - Discovery failures
- `MessageSerializationException` - Serialization errors

### 4. Comprehensive Logging
All services log:
- Initialization events
- State changes
- Errors with full context
- Debug information (in DEBUG builds)

## Running Tests

### Visual Studio
1. Open Test Explorer (Test ¡ú Test Explorer)
2. Click "Run All" to execute all tests
3. View results and coverage

### Command Line
```bash
# Restore packages
nuget restore

# Build tests
msbuild LazyChat.Tests/LazyChat.Tests.csproj

# Run tests
nunit3-console LazyChat.Tests/bin/Debug/LazyChat.Tests.dll
```

### Test Coverage
```bash
# Using OpenCover (optional)
OpenCover.Console.exe -target:"nunit3-console.exe" -targetargs:"LazyChat.Tests.dll"
```

## Test Categories

### Unit Tests

#### NetworkModels Tests
? Message serialization/deserialization  
? Validation logic  
? Type-specific validation  
? Error handling  

#### PeerDiscoveryService Tests
? Constructor validation  
? Service lifecycle (start/stop)  
? Event subscription  
? Logging verification  
? Parameter validation  

#### P2PCommunicationService Tests (TODO)
- [ ] Message sending
- [ ] Connection management
- [ ] Error recovery
- [ ] Concurrent connections

#### FileTransferService Tests (TODO)
- [ ] Chunking logic
- [ ] Progress tracking
- [ ] Cancel functionality
- [ ] File validation

### Integration Tests (TODO)
- [ ] Peer discovery between instances
- [ ] Message exchange
- [ ] File transfer end-to-end
- [ ] Error scenarios

## Writing New Tests

### Example: Testing a Service Method

```csharp
[Test]
public void ServiceMethod_ValidInput_ReturnsExpectedResult()
{
    // Arrange - Setup test data and mocks
    var mockLogger = MockHelpers.CreateMockLogger();
    var service = new MyService(mockLogger.Object);
    var input = "test input";

    // Act - Execute the method
    var result = service.MethodUnderTest(input);

    // Assert - Verify expectations
    Assert.That(result, Is.EqualTo("expected"));
    mockLogger.Verify(l => l.LogInfo(It.IsAny<string>()), Times.Once);
}
```

### Example: Testing Exception Handling

```csharp
[Test]
public void ServiceMethod_InvalidInput_ThrowsException()
{
    // Arrange
    var service = new MyService();

    // Act & Assert
    var ex = Assert.Throws<MyCustomException>(() => 
        service.MethodUnderTest(null));
    
    Assert.That(ex.Message, Does.Contain("expected error"));
}
```

### Example: Testing Async Operations

```csharp
[Test]
public void ServiceMethod_WaitsForCompletion()
{
    // Arrange
    var service = new MyService();
    bool completed = false;

    service.OperationCompleted += (s, e) => completed = true;

    // Act
    service.StartOperation();
    Thread.Sleep(500); // Wait for async operation

    // Assert
    Assert.That(completed, Is.True);
}
```

## Mock Helpers

The `MockHelpers` class provides convenient mock creation:

```csharp
// Create mock logger
var mockLogger = MockHelpers.CreateMockLogger();

// Create mock network adapter
var mockNetwork = MockHelpers.CreateMockNetworkAdapter();

// Create test logger that captures all logs
var testLogger = new TestLogger();
service.DoWork();
Assert.That(testLogger.InfoLogs.Count, Is.GreaterThan(0));
```

## Best Practices

### ? DO
- Write tests before implementing features (TDD)
- Test one thing per test method
- Use descriptive test names (MethodName_Scenario_ExpectedResult)
- Arrange, Act, Assert pattern
- Mock external dependencies
- Test edge cases and error conditions
- Clean up resources in teardown

### ? DON'T
- Test implementation details
- Create test interdependencies
- Use hard-coded paths or ports
- Leave tests that randomly fail
- Skip testing error handling
- Test framework code
- Write tests that require manual setup

## Continuous Integration

Tests should run:
- On every commit (pre-commit hook)
- On pull requests
- On main branch merges
- Nightly for extended test suites

## Code Coverage Goals

- **Overall**: 80%+ coverage
- **Business Logic**: 90%+ coverage
- **Models**: 95%+ coverage
- **UI Code**: 60%+ coverage (harder to test)

## Future Improvements

1. **Integration Tests**: Test actual network communication
2. **Performance Tests**: Measure throughput and latency
3. **Load Tests**: Test with many concurrent peers
4. **UI Tests**: Automated UI testing with FlaUI or similar
5. **Mutation Testing**: Verify test quality
6. **Contract Tests**: Verify protocol compatibility

## Resources

- [NUnit Documentation](https://docs.nunit.org/)
- [Moq Quick Start](https://github.com/moq/moq4/wiki/Quickstart)
- [TDD Best Practices](https://martinfowler.com/bliki/TestDrivenDevelopment.html)

## Contributing

When adding new features:
1. Write failing tests first
2. Implement feature
3. Ensure all tests pass
4. Refactor with tests as safety net
5. Document test coverage
