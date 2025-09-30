using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LB_FATE.AvaloniaClient.Services;

/// <summary>
/// TCP client for connecting to LB_FATE game server.
/// </summary>
public class GameClient : IDisposable
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public event EventHandler<string>? MessageReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>
    /// Connect to the game server.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);

            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            _cts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);

            ConnectionStatusChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the server.
    /// </summary>
    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _writer?.Close();
            _reader?.Close();
            _client?.Close();
            ConnectionStatusChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Disconnect error: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a command to the server.
    /// </summary>
    public async Task SendCommandAsync(string command)
    {
        if (_writer == null || !IsConnected)
        {
            ErrorOccurred?.Invoke(this, "Not connected to server");
            return;
        }

        try
        {
            await _writer.WriteLineAsync(command);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Send error: {ex.Message}");
            Disconnect();
        }
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        if (_reader == null) return;

        try
        {
            while (!token.IsCancellationRequested && IsConnected)
            {
                var line = await _reader.ReadLineAsync(token);
                if (line == null)
                {
                    // Server closed connection
                    ConnectionStatusChanged?.Invoke(this, false);
                    break;
                }

                MessageReceived?.Invoke(this, line);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, false);
        }
    }

    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
        _client?.Dispose();
    }
}