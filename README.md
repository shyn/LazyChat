# LazyChat - P2P局域网聊天工具

一个**健壮、模块化、可测试、高性能**的无服务器局域网聊天应用，支持自动发现、文字消息、图片和文件传输。

## ? 特性

- ?? **自动发现** - UDP广播自动发现局域网内的用户
- ?? **即时通讯** - 点对点实时消息传输
- ??? **图片分享** - 发送和预览图片（JPG、PNG、GIF、BMP）
- ?? **文件传输** - 大文件分块传输，带进度显示
- ?? **用户状态** - 实时显示在线/离线状态
- ?? **无需服务器** - 完全P2P架构
- ?? **测试驱动** - 高测试覆盖率和质量保证
- ? **高性能序列化** - 使用 MessagePack，比传统方案快 6-7 倍

## ?? 技术亮点

### 高性能序列化
- **MessagePack 替代 BinaryFormatter**
  - ? 6-7倍序列化速度提升
  - ?? 70% 数据体积减少
  - ?? 消除安全风险
  - ?? 85% CPU 使用率降低

详见 [性能对比报告](PERFORMANCE_COMPARISON.md)

### 网络协议
- **发现协议**: UDP广播（端口8888）
- **通信协议**: TCP可靠传输（端口9999）
- **消息格式**: 二进制序列化（可扩展JSON）

### 文件传输
- **分块大小**: 64KB
- **进度跟踪**: 实时进度更新
- **错误恢复**: 传输失败自动清理

### 异常处理
```csharp
LazyChatException                  // 基础异常
├── NetworkException              // 网络异常
├── FileTransferException         // 文件传输异常
├── PeerDiscoveryException        // 发现异常
└── MessageSerializationException // 序列化异常
```

### 日志记录
- **文件日志**: 自动保存到本地应用数据目录
- **控制台日志**: DEBUG模式下输出到控制台
- **日志级别**: INFO, WARNING, ERROR, DEBUG

## ?? 开发指南

### 添加新功能

遵循测试驱动开发（TDD）流程：

1. **编写测试**
```csharp
[Test]
public void NewFeature_Scenario_ExpectedResult()
{
    // Arrange
    var service = new MyService(mockLogger.Object);
    
    // Act
    var result = service.NewFeature();
    
    // Assert
    Assert.That(result, Is.Not.Null);
}
```

2. **实现功能** - 让测试通过

3. **重构代码** - 在测试保护下改进设计

### 代码规范

- ? 使用接口而非具体实现
- ? 通过构造函数注入依赖
- ? 添加XML文档注释
- ? 记录关键操作日志
- ? 验证输入参数
- ? 抛出有意义的异常

### 示例：创建新服务

```csharp
// 1. 定义接口
public interface IMyService : IDisposable
{
    void DoWork();
    event EventHandler<string> WorkCompleted;
}

// 2. 实现服务
public class MyService : IMyService
{
    private readonly ILogger _logger;
    
    public MyService(ILogger logger = null)
    {
        _logger = logger ?? new FileLogger();
    }
    
    public void DoWork()
    {
        try
        {
            _logger.LogInfo("Starting work");
            // 实现逻辑
            OnWorkCompleted("Success");
        }
        catch (Exception ex)
        {
            _logger.LogError("Work failed", ex);
            throw new LazyChatException("Work failed", ex);
        }
    }
    
    public event EventHandler<string> WorkCompleted;
    
    protected virtual void OnWorkCompleted(string result)
    {
        WorkCompleted?.Invoke(this, result);
    }
    
    public void Dispose()
    {
        _logger?.LogInfo("Service disposed");
    }
}

// 3. 编写测试
[TestFixture]
public class MyServiceTests
{
    [Test]
    public void DoWork_Success_RaisesEvent()
    {
        var logger = new TestLogger();
        var service = new MyService(logger);
        bool eventRaised = false;
        
        service.WorkCompleted += (s, r) => eventRaised = true;
        service.DoWork();
        
        Assert.That(eventRaised, Is.True);
        Assert.That(logger.InfoLogs.Count, Is.GreaterThan(0));
    }
}
```

## ?? 安全考虑

- ?? **局域网限制**: 仅在可信局域网使用
- ?? **无加密**: 消息未加密，不建议传输敏感信息
- ?? **防火墙**: 确保允许UDP 8888和TCP 9999端口
- ?? **文件验证**: 接收文件前请检查来源

## ?? 故障排除

### 无法发现其他用户
- 检查防火墙设置
- 确认在同一局域网段
- 验证UDP端口8888未被占用

### 消息发送失败
- 检查TCP端口9999可用性
- 验证目标用户在线
- 查看日志文件获取详细错误

### 文件传输中断
- 检查磁盘空间
- 验证文件权限
- 检查网络连接稳定性

## ?? 性能指标

- **发现延迟**: < 5秒
- **消息延迟**: < 100ms（局域网）
- **文件传输**: ~10MB/s（取决于网络）
- **内存占用**: < 50MB（空闲）
- **最大并发用户**: 50+（理论值）

## ??? 路线图

### v1.1（计划中）
- [ ] 消息加密（AES）
- [ ] 群聊支持
- [ ] 消息历史持久化
- [ ] 离线消息队列
- [ ] 表情符号支持

### v1.2（计划中）
- [ ] 语音消息
- [ ] 截图工具集成
- [ ] 主题定制
- [ ] 多语言支持
- [ ] 移动端版本

### v2.0（规划中）
- [ ] 端到端加密
- [ ] 视频通话
- [ ] 插件系统
- [ ] 跨平台支持（.NET Core）

## ?? 贡献

欢迎贡献！请遵循以下步骤：

1. Fork本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 编写测试
4. 实现功能
5. 提交更改 (`git commit -m 'Add AmazingFeature'`)
6. 推送分支 (`git push origin feature/AmazingFeature`)
7. 开启Pull Request

## ?? 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## ?? 作者

LazyChat Team

## ?? 致谢

- NUnit - 测试框架
- Moq - Mocking框架
- .NET Framework - 应用平台

## ?? 联系方式

- 问题反馈: [GitHub Issues](https://github.com/your-repo/issues)
- 功能建议: [GitHub Discussions](https://github.com/your-repo/discussions)

---

**注意**: 本应用仅供学习和内部网络使用。请勿在公网环境使用，以确保数据安全。
