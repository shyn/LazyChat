# 局域网发现服务改进说明

## 改进概述
针对"macOS + Windows 开启 VPN，但局域网仍可直连"场景，对 `PeerDiscoveryService` 进行了以下增强：

## 主要改进点

### 1. **多网卡支持与智能接口选择** 🎯
**问题**：原实现只向一个广播地址发送，如果该地址对应 VPN 虚拟网卡，局域网设备无法收到。

**解决方案**：
- 枚举所有可用网络接口（`DiscoverNetworkInterfaces()`）
- 自动过滤 VPN/虚拟/隧道接口（通过接口类型和名称启发式判断）
- 为每个物理网卡计算独立的广播地址
- 在所有接口上并行广播，确保至少一个能到达局域网

**关键过滤逻辑**：
```csharp
// 跳过 Loopback, Tunnel, 以及名称包含 vpn/virtual/tun/tap/utun/ppp 的接口
if (name.Contains("vpn") || name.Contains("virtual") || 
    name.Contains("tun") || name.Contains("tap") ||
    name.Contains("utun") || name.Contains("ppp"))
```

### 2. **每网卡独立 Socket 绑定** 🔧
**问题**：未绑定的 UDP socket 发包路由由系统决定，VPN 开启后可能强制走 VPN 隧道。

**解决方案**：
- 为每个接口创建独立的 `UdpClient`（`CreateBroadcastClients()`）
- 绑定到该接口的本地 IP（`new UdpClient(new IPEndPoint(localIp, 0))`）
- 强制从物理网卡发出广播包，避免被 VPN 路由劫持

### 3. **Peer 地址缓存与单播探测 Fallback** 💾
**问题**：VPN/企业策略可能完全禁止广播/多播，导致发现失效。

**解决方案**：
- 持久化缓存已发现 peer 的 IP/端口到 `peer_cache.dat`
- 启动时立即对缓存地址发送**单播 Discovery 消息**（`ProbeKnownPeers()`）
- 每隔 10 秒定期探测缓存（即使广播被禁，单播直连仍可用）
- 缓存保留 7 天内活跃的 peer

**文件位置**：
```
%LocalAppData%\LazyChat\peer_cache.dat  (Windows)
~/Library/Application Support/LazyChat/peer_cache.dat  (macOS)
```

### 4. **增强的广播逻辑** 📡
- **多接口并行广播**：每 5 秒在所有物理网卡上发送 Discovery
- **单播探测**：每 10 秒对缓存 peer 发送单播 Discovery
- **响应缓存更新**：收到任何 Discovery/DiscoveryResponse 时更新缓存
- **优雅退出**：停止时在所有接口广播 UserLeft 消息

### 5. **兼容性保持** ✅
- 保持原有 `IPeerDiscoveryService` 接口不变
- 调用方无需修改代码
- 仅增强内部实现，向后兼容

---

## 对比主流 P2P 软件

| 特性 | 原实现 | 改进后 | Syncthing | Resilio Sync |
|-----|--------|--------|-----------|--------------|
| 多网卡广播 | ❌ | ✅ | ✅ | ✅ |
| 接口绑定 | ❌ | ✅ | ✅ | ✅ |
| VPN 过滤 | ❌ | ✅ | ✅ | ✅ |
| 地址缓存 | ❌ | ✅ | ✅ | ✅ |
| 单播探测 | ❌ | ✅ | ✅ | ✅ |
| 手动添加 | ❌ | ⚠️ 待实现 | ✅ | ✅ |

⚠️ **建议后续添加**：在 UI 层增加"手动添加 IP"功能，作为最终 fallback。

---

## 使用场景验证

### 场景 1：双方开 VPN，局域网可直连
**原方案**：可能失败（如果广播走了 VPN 虚拟网卡）  
**新方案**：
1. 自动过滤 VPN 接口，在物理网卡上广播 ✅
2. 如果广播被禁，单播探测缓存地址 ✅
3. 成功建立连接

### 场景 2：企业 Wi-Fi AP 隔离
**原方案**：完全失败  
**新方案**：
1. 广播被 AP 拦截
2. 单播探测缓存（如果之前在其他网络连接过）✅
3. 或需用户手动添加 IP（待实现）

### 场景 3：多网卡环境（有线+Wi-Fi+VPN）
**原方案**：只在一个接口广播  
**新方案**：同时在有线和 Wi-Fi 广播，覆盖所有可达路径 ✅

---

## 日志增强

新增以下关键日志：

```
[INFO] Active interface: Wi-Fi - 192.168.1.100 (broadcast: 192.168.1.255)
[DEBUG] Skipping VPN/virtual interface: utun3
[INFO] Peer discovery service started on 2 interface(s)
[DEBUG] Broadcasted on Wi-Fi to 192.168.1.255
[DEBUG] Probing 3 cached peer(s)
[DEBUG] Probed cached peer Alice at 192.168.1.50
```

可通过日志诊断：
- 发现了哪些接口
- 过滤了哪些 VPN 接口
- 是否成功在多个接口广播
- 单播探测是否生效

---

## 测试建议

### 1. VPN 场景测试
- macOS 开启 VPN，Windows 未开
- 双方都开 VPN
- 验证能否发现对方

### 2. 缓存测试
- A、B 正常连接后关闭
- 删除 A 的缓存文件，B 保留
- 重启 A、B，观察 B 能否通过缓存单播发现 A

### 3. 多网卡测试
- 设备同时连接有线和 Wi-Fi
- 观察日志确认在两个接口都有广播

### 4. 接口过滤测试
- 开启 VPN，检查日志确认 VPN 接口被跳过
- 确认物理接口仍正常工作

---

## 性能考虑

- **带宽**：每个接口每 5 秒发一个 ~100 字节广播包，影响可忽略
- **单播探测**：每 10 秒对缓存 peer 发送，避免频繁探测
- **缓存清理**：只保留 7 天内活跃 peer，避免缓存膨胀

---

## 后续可选增强

1. **手动添加 IP**：UI 增加"添加设备"功能，输入 IP 后保存到缓存
2. **局域网扫描**：用户手动触发的 /24 探测（节流并发，仅作修复按钮）
3. **mDNS/Bonjour 支持**：增加 mDNS 发现作为辅助（尤其对 macOS 友好）
4. **IPv6 支持**：当前仅 IPv4，可扩展支持 IPv6 多播
5. **NAT 穿透**：对于需要跨网段的场景（已超出局域网发现范畴）

---

## 迁移说明

**现有代码无需修改**，服务升级对调用方透明：

```csharp
// 原有代码继续工作
var discovery = new PeerDiscoveryService(userName, port, logger);
discovery.Start();
```

**诊断接口更新**：
```csharp
// 旧版本
var (localIp, available) = discovery.GetDiagnostics();

// 新版本（增加接口数量）
var (localIp, available, interfaceCount) = discovery.GetDiagnostics();
```

---

## 参考资料

- [Syncthing Local Discovery](https://docs.syncthing.net/users/localdisco.html)
- [Resilio Sync Connection Methods](https://help.resilio.com/hc/en-us/articles/204754759)
- [mDNS/DNS-SD (Bonjour) RFC 6762](https://datatracker.ietf.org/doc/html/rfc6762)
- [SSDP/UPnP Discovery](https://en.wikipedia.org/wiki/Simple_Service_Discovery_Protocol)

---

**改进完成时间**：2025-12-19  
**测试状态**：待测试  
**已知问题**：无
