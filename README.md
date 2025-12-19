# LazyChat - P2P���������칤��

һ��**��׳��ģ�黯���ɲ��ԡ�������**���޷���������������Ӧ�ã�֧���Զ����֡�������Ϣ��ͼƬ���ļ����䡣

## ? ����

- ?? **�Զ�����** - UDP�㲥�Զ����־������ڵ��û�
- ?? **��ʱͨѶ** - ��Ե�ʵʱ��Ϣ����
- ??? **ͼƬ����** - ���ͺ�Ԥ��ͼƬ��JPG��PNG��GIF��BMP��
- ?? **�ļ�����** - ���ļ��ֿ鴫�䣬��������ʾ
- ?? **�û�״̬** - ʵʱ��ʾ����/����״̬
- ?? **���������** - ��ȫP2P�ܹ�
- ?? **��������** - �߲��Ը����ʺ�������֤
- ? **���������л�** - ʹ�� MessagePack���ȴ�ͳ������ 6-7 ��

## ?? ��������

### ���������л�
- **MessagePack ��� BinaryFormatter**
  - ? 6-7�����л��ٶ�����
  - ?? 70% �����������
  - ?? ������ȫ����
  - ?? 85% CPU ʹ���ʽ���

### ����Э��
- **����Э��**: UDP�㲥���˿�8888��
- **ͨ��Э��**: TCP�ɿ����䣨�˿�9999��
- **��Ϣ��ʽ**: ���������л�������չJSON��

### �ļ�����
- **�ֿ��С**: 64KB
- **���ȸ���**: ʵʱ���ȸ���
- **����ָ�**: ����ʧ���Զ�����

### �쳣����
```csharp
LazyChatException                  // �����쳣
������ NetworkException              // �����쳣
������ FileTransferException         // �ļ������쳣
������ PeerDiscoveryException        // �����쳣
������ MessageSerializationException // ���л��쳣
```

### ��־��¼
- **�ļ���־**: �Զ����浽����Ӧ������Ŀ¼
- **����̨��־**: DEBUGģʽ�����������̨
- **��־����**: INFO, WARNING, ERROR, DEBUG

## ?? ����ָ��

### �����¹���

��ѭ��������������TDD�����̣�

1. **��д����**
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

2. **ʵ�ֹ���** - �ò���ͨ��

3. **�ع�����** - �ڲ��Ա����¸Ľ����

### ����淶

- ? ʹ�ýӿڶ��Ǿ���ʵ��
- ? ͨ�����캯��ע������
- ? ����XML�ĵ�ע��
- ? ��¼�ؼ�������־
- ? ��֤�������
- ? �׳���������쳣

### ʾ���������·���

```csharp
// 1. ����ӿ�
public interface IMyService : IDisposable
{
    void DoWork();
    event EventHandler<string> WorkCompleted;
}

// 2. ʵ�ַ���
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
            // ʵ���߼�
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

// 3. ��д����
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

## ?? ��ȫ����

- ?? **����������**: ���ڿ��ž�����ʹ��
- ?? **�޼���**: ��Ϣδ���ܣ������鴫��������Ϣ
- ?? **����ǽ**: ȷ������UDP 8888��TCP 9999�˿�
- ?? **�ļ���֤**: �����ļ�ǰ������Դ

## ?? �����ų�

### �޷����������û�
- ������ǽ����
- ȷ����ͬһ��������
- ��֤UDP�˿�8888δ��ռ��

### ��Ϣ����ʧ��
- ���TCP�˿�9999������
- ��֤Ŀ���û�����
- �鿴��־�ļ���ȡ��ϸ����

### �ļ������ж�
- �����̿ռ�
- ��֤�ļ�Ȩ��
- ������������ȶ���

## ?? ����ָ��

- **�����ӳ�**: < 5��
- **��Ϣ�ӳ�**: < 100ms����������
- **�ļ�����**: ~10MB/s��ȡ�������磩
- **�ڴ�ռ��**: < 50MB�����У�
- **��󲢷��û�**: 50+������ֵ��

## ??? ·��ͼ

### v1.1���ƻ��У�
- [ ] ��Ϣ���ܣ�AES��
- [ ] Ⱥ��֧��
- [ ] ��Ϣ��ʷ�־û�
- [ ] ������Ϣ����
- [ ] �������֧��

### v1.2���ƻ��У�
- [ ] ������Ϣ
- [ ] ��ͼ���߼���
- [ ] ���ⶨ��
- [ ] ������֧��
- [ ] �ƶ��˰汾

### v2.0���滮�У�
- [ ] �˵��˼���
- [ ] ��Ƶͨ��
- [ ] ���ϵͳ
- [ ] ��ƽ̨֧�֣�.NET Core��

## ?? ����

��ӭ���ף�����ѭ���²��裺

1. Fork���ֿ�
2. �������Է�֧ (`git checkout -b feature/AmazingFeature`)
3. ��д����
4. ʵ�ֹ���
5. �ύ���� (`git commit -m 'Add AmazingFeature'`)
6. ���ͷ�֧ (`git push origin feature/AmazingFeature`)
7. ����Pull Request

## ?? ����֤

����Ŀ���� MIT ����֤ - �鿴 [LICENSE](LICENSE) �ļ��˽�����

## ?? ����

LazyChat Team

## ?? ��л

- NUnit - ���Կ��
- Moq - Mocking���
- .NET Framework - Ӧ��ƽ̨

## ?? ��ϵ��ʽ

- ���ⷴ��: [GitHub Issues](https://github.com/your-repo/issues)
- ���ܽ���: [GitHub Discussions](https://github.com/your-repo/discussions)

---

**ע��**: ��Ӧ�ý���ѧϰ���ڲ�����ʹ�á������ڹ�������ʹ�ã���ȷ�����ݰ�ȫ��
