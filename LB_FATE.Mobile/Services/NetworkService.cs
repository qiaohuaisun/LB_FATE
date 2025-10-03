using System.Net.Sockets;
using System.Text;

namespace LB_FATE.Mobile.Services;

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

    public event Action<string>? MessageReceived;
    public event Action? Connected;
    public event Action<string>? Disconnected;
    public event Action<string>? PromptReceived;

    public bool IsConnected
    {
        get { lock (_lock) return _isConnected; }
        private set { lock (_lock) _isConnected = value; }
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

            IsConnected = true;
            _cts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);

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
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                string? line = await _reader.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    IsConnected = false;
                    Disconnected?.Invoke("服务器关闭连接");
                    break;
                }

                // 特殊消息：PROMPT 表示服务器等待玩家输入
                if (line == "PROMPT")
                {
                    PromptReceived?.Invoke(line);
                }
                else
                {
                    MessageReceived?.Invoke(line);
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
