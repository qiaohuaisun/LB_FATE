using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LB_FATE.Mobile.Services;

/// <summary>
/// 客户端协议类型
/// </summary>
public enum ClientProtocol
{
    Text,
    Json
}

/// <summary>
/// 网络服务，负责TCP连接管理和消息收发
/// </summary>
public class NetworkService : IDisposable
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private bool _isConnected;
    private readonly object _lock = new();
    private ClientProtocol _protocol = ClientProtocol.Json; // 默认使用JSON协议

    // 性能统计
    private int _textMessagesReceived = 0;
    private int _jsonMessagesReceived = 0;
    private long _textBytesReceived = 0;
    private long _jsonBytesReceived = 0;

    public event Action<string>? MessageReceived;
    public event Action<JsonDocument>? JsonMessageReceived;
    public event Action? Connected;
    public event Action<string>? Disconnected;
    public event Action<string>? PromptReceived;

    public bool IsConnected
    {
        get { lock (_lock) return _isConnected; }
        private set { lock (_lock) _isConnected = value; }
    }

    public ClientProtocol Protocol
    {
        get { lock (_lock) return _protocol; }
        private set { lock (_lock) _protocol = value; }
    }

    /// <summary>
    /// 获取性能统计信息
    /// </summary>
    public (int TextMessages, int JsonMessages, long TextBytes, long JsonBytes) GetStatistics()
    {
        return (_textMessagesReceived, _jsonMessagesReceived, _textBytesReceived, _jsonBytesReceived);
    }

    /// <summary>
    /// 重置性能统计
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _textMessagesReceived, 0);
        Interlocked.Exchange(ref _jsonMessagesReceived, 0);
        Interlocked.Exchange(ref _textBytesReceived, 0);
        Interlocked.Exchange(ref _jsonBytesReceived, 0);
    }

    /// <summary>
    /// 连接到游戏服务器
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            await DisconnectAsync();

            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);

            var stream = _client.GetStream();
            _reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

            // 发送协议握手
            await _writer.WriteLineAsync("JSON_PROTOCOL v1");
            System.Diagnostics.Debug.WriteLine("[NetworkService] 已发送JSON协议握手请求");

            // 读取服务器响应（带超时）
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                string? ack = await _reader.ReadLineAsync(linkedCts.Token);

                if (ack != null && ack.StartsWith("{"))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(ack);
                        if (doc.RootElement.TryGetProperty("type", out var typeElement) &&
                            typeElement.GetString() == "PROTOCOL_ACK")
                        {
                            Protocol = ClientProtocol.Json;
                            System.Diagnostics.Debug.WriteLine("[NetworkService] ✓ JSON协议已确认");
                        }
                        else
                        {
                            Protocol = ClientProtocol.Text;
                            System.Diagnostics.Debug.WriteLine($"[NetworkService] 收到非PROTOCOL_ACK消息，降级到文本协议: {ack.Substring(0, Math.Min(50, ack.Length))}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        // 如果解析失败，降级到文本协议
                        Protocol = ClientProtocol.Text;
                        System.Diagnostics.Debug.WriteLine($"[NetworkService] JSON解析失败，降级到文本协议: {ex.Message}");
                    }
                }
                else
                {
                    // 如果没有收到JSON响应，降级到文本协议
                    Protocol = ClientProtocol.Text;
                    System.Diagnostics.Debug.WriteLine($"[NetworkService] 收到文本响应，使用文本协议: {ack ?? "null"}");
                }
            }
            catch (OperationCanceledException)
            {
                // 超时，降级到文本协议
                Protocol = ClientProtocol.Text;
                System.Diagnostics.Debug.WriteLine("[NetworkService] 协议握手超时，降级到文本协议");
            }

            IsConnected = true;
            _cts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);

            System.Diagnostics.Debug.WriteLine($"[NetworkService] 连接成功，使用协议: {Protocol}，接收循环已启动");
            Connected?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke($"连接失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        IsConnected = false;

        try { _cts?.Cancel(); } catch { }

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
        }

        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        try { _client?.Dispose(); } catch { }

        _reader = null;
        _writer = null;
        _client = null;
        _cts = null;
        _receiveTask = null;
    }

    /// <summary>
    /// 发送命令到服务器
    /// </summary>
    public async Task<bool> SendCommandAsync(string command)
    {
        if (!IsConnected || _writer == null)
            return false;

        try
        {
            await _writer.WriteLineAsync(command);
            return true;
        }
        catch
        {
            IsConnected = false;
            Disconnected?.Invoke("发送消息时连接中断");
            return false;
        }
    }

    /// <summary>
    /// 接收消息循环
    /// </summary>
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[NetworkService] ReceiveLoop 已启动");
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                string? line = await _reader.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    IsConnected = false;
                    System.Diagnostics.Debug.WriteLine("[NetworkService] 收到null，服务器关闭连接");
                    Disconnected?.Invoke("服务器关闭连接");
                    break;
                }

                System.Diagnostics.Debug.WriteLine($"[NetworkService] 收到消息 ({line.Length} 字节): {(line.Length > 100 ? line.Substring(0, 100) + "..." : line)}");

                // 特殊消息：PROMPT 表示服务器等待玩家输入
                if (line == "PROMPT")
                {
                    PromptReceived?.Invoke(line);
                    continue;
                }

                // 根据协议类型处理消息
                if (Protocol == ClientProtocol.Json && line.StartsWith("{"))
                {
                    try
                    {
                        Interlocked.Increment(ref _jsonMessagesReceived);
                        Interlocked.Add(ref _jsonBytesReceived, line.Length);

                        var doc = JsonDocument.Parse(line);
                        System.Diagnostics.Debug.WriteLine($"[NetworkService] → 触发 JsonMessageReceived 事件");
                        JsonMessageReceived?.Invoke(doc);

                        // 每100条消息记录一次性能统计
                        if (_jsonMessagesReceived % 100 == 0)
                        {
                            var avgBytes = _jsonBytesReceived / _jsonMessagesReceived;
                            System.Diagnostics.Debug.WriteLine($"[NetworkService] JSON性能: {_jsonMessagesReceived} 消息, 平均 {avgBytes} 字节/消息");
                        }
                    }
                    catch (JsonException ex)
                    {
                        // JSON解析失败，作为文本消息处理
                        System.Diagnostics.Debug.WriteLine($"[NetworkService] JSON解析错误: {ex.Message}");
                        MessageReceived?.Invoke(line);
                    }
                }
                else
                {
                    // 文本协议消息
                    Interlocked.Increment(ref _textMessagesReceived);
                    Interlocked.Add(ref _textBytesReceived, line.Length);

                    System.Diagnostics.Debug.WriteLine($"[NetworkService] → 触发 MessageReceived 事件（文本）");
                    MessageReceived?.Invoke(line);

                    // 每100条消息记录一次性能统计
                    if (_textMessagesReceived % 100 == 0)
                    {
                        var avgBytes = _textBytesReceived / _textMessagesReceived;
                        System.Diagnostics.Debug.WriteLine($"[NetworkService] 文本性能: {_textMessagesReceived} 消息, 平均 {avgBytes} 字节/消息");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Disconnected?.Invoke($"连接错误: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
