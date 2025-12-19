# LazyChat - P2P 局域网聊天工具

一款 **即开即用、模块化、可测试、跨平台** 的局域网 P2P 聊天应用，支持自动发现、实时消息、图片与文件传输。

## ✨ 核心特性
- 🔍 **自动发现**：UDP 广播自动发现同一网段用户
- 💬 **实时通信**：端到端实时文本消息
- 🖼️ **图片传输**：发送与预览 JPG/PNG/GIF/BMP
- 📁 **文件传输**：分块传输，断点续传/重试
- 👀 **在线状态**：实时显示在线/离线状态
- 🔒 **纯 P2P 架构**：无中心服务器
- 🧪 **高测试性**：接口分层，易于单测/集成测
- 📦 **高效序列化**：MessagePack，相比传统 BinaryFormatter 约 6-7 倍性能

## ⚙️ 技术概要

### 序列化
- **MessagePack vs BinaryFormatter**
  - 🚀 6-7 倍序列化速度
  - 📉 ~70% 体积缩减
  - 🛡️ 更安全
  - 🧠 ~85% CPU 占用降低

### 网络协议
- **发现协议**：UDP 广播，端口 8888
- **通信协议**：可靠 TCP，端口 9999
- **消息格式**：MessagePack，可扩展 JSON

### 文件传输
- **分块大小**：64KB
- **实时进度**：推送更新
- **错误恢复**：失败自动重试

### 异常体系
```csharp
LazyChatException                  // 基类
    NetworkException              // 网络相关
    FileTransferException         // 文件传输
    PeerDiscoveryException        // 节点发现
    MessageSerializationException // 序列化
```

### 日志
- **文件日志**：按应用目录自动落地
- **控制台日志**：DEBUG 模式输出
- **等级**：INFO / WARNING / ERROR / DEBUG

## 🧭 开发与测试

### 开发流程（TDD）
1. **先写测试**
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
2. **实现代码**：让测试通过
3. **重构优化**：在绿灯下改进设计

### 代码规范
- ✅ 接口驱动，依赖注入
- ✅ 构造函数注入依赖
- ✅ XML 文档注释
- ✅ 关键路径记录日志
- ✅ 参数与状态校验
- ✅ 抛出语义化异常

### 示例结构
```csharp
// 1. 定义接口
public interface IMyService : IDisposable
{
    void DoWork();
    event EventHandler<string> WorkCompleted;
}

// 2. 实现
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
            // 业务逻辑
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

## 🔐 安全建议
- 🚫 **无端口暴露到公网**：仅限局域网使用
- 🧱 **防火墙**：允许 UDP 8888 / TCP 9999
- 🗂️ **文件校验**：传输前验证来源与大小

## 🛠️ 故障排查
### 发现不到其他用户
- 检查防火墙是否放行 UDP 8888
- 确认在同一子网
- 确认端口未被占用

### 消息收不到
- 检查 TCP 9999 是否被占用
- 确认对方在线
- 查看日志获取细节

### 文件传输中断
- 检查磁盘空间与权限
- 确认网络稳定
- 查看日志获取错误原因

## 📊 性能指标（目标）
- **启动时延**：< 5s
- **消息时延**：< 100ms（同网段）
- **文件吞吐**：≈ 10MB/s（视网络而定）
- **内存占用**：< 50MB（运行态）
- **并发在线**：50+（典型局域网）

## 🗺️ 路线图
### v1.1 规划
- [ ] 消息加密（AES）
- [ ] 群聊
- [ ] 消息历史持久化
- [ ] 离线消息
- [ ] 表情包支持

### v1.2 规划
- [ ] 语音消息
- [ ] 截图与贴图
- [ ] 主题定制
- [ ] 多语言
- [ ] 移动端

### v2.0 规划
- [ ] 插件体系
- [ ] 视频通话
- [ ] 云同步
- [ ] 跨平台（.NET Core 完整支持）

## 🤝 贡献指南
1. Fork 仓库
2. 创建特性分支：`git checkout -b feature/AmazingFeature`
3. 编写/完善测试
4. 实现功能
5. 提交改动：`git commit -m "Add AmazingFeature"`
6. 推送分支：`git push origin feature/AmazingFeature`
7. 提交 Pull Request

## 📄 许可证
本项目采用 MIT License，详见 [LICENSE](LICENSE)。

## 👥 关于
LazyChat Team

## 🙏 鸣谢
- NUnit - 测试框架
- Moq - Mock 工具
- .NET Framework / Avalonia - 跨平台 UI

## 📫 联系方式
- 问题反馈：[GitHub Issues](https://github.com/your-repo/issues)
- 功能讨论：[GitHub Discussions](https://github.com/your-repo/discussions)

---
> 本应用仅供学习与内网使用，生产使用请确保数据安全。
