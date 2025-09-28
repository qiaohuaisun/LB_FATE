using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

interface IPlayerEndpoint : IDisposable
{
    string Id { get; }
    void SendLine(string text);
    string? ReadLine();
}

class ConsoleEndpoint : IPlayerEndpoint
{
    public string Id { get; }
    public ConsoleEndpoint(string id) { Id = id; }
    public void SendLine(string text) { Console.WriteLine(text); }
    public string? ReadLine() { return Console.ReadLine(); }
    public void Dispose() { }
}

class TcpPlayerEndpoint : IPlayerEndpoint
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    public string Id { get; }

    public TcpPlayerEndpoint(string id, TcpClient client)
    {
        Id = id;
        _client = client;
        var stream = client.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

    public void SendLine(string text)
    {
        lock (_lock)
        {
            _writer.WriteLine(text);
        }
    }

    public string? ReadLine()
    {
        try { return _reader.ReadLine(); }
        catch { return null; }
    }

    public void Dispose()
    {
        try { _reader.Dispose(); } catch { }
        try { _writer.Dispose(); } catch { }
        try { _client.Close(); } catch { }
    }
}

class NetServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly int _port;
    private bool _started;
    private DiscoveryResponder? _discovery;

    public NetServer(int port)
    {
        _port = port;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public void Start()
    {
        if (_started) return;
        _listener.Start();
        _started = true;
        // Start UDP discovery responder (best-effort)
        try { _discovery = new DiscoveryResponder(_port); } catch { }
    }

    public Dictionary<string, IPlayerEndpoint> WaitForPlayers(int count)
    {
        if (!_started) Start();
        var map = new Dictionary<string, IPlayerEndpoint>();
        Console.WriteLine($"Waiting for {count} players to connect...");
        for (int i = 1; i <= count; i++)
        {
            var client = _listener.AcceptTcpClient();
            var pid = $"P{i}";
            var ep = new TcpPlayerEndpoint(pid, client);
            map[pid] = ep;
            ep.SendLine($"WELCOME {pid}");
            ep.SendLine("You are connected. Wait for your turn.");
            Console.WriteLine($"Player connected and assigned: {pid}");
        }
        return map;
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        try { _discovery?.Dispose(); } catch { }
    }
}

static class NetClient
{
    public static void Run(string host, int port)
    {
        using var client = new TcpClient();
        client.Connect(host, port);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

        Console.WriteLine($"Connected to {host}:{port}");
        while (true)
        {
            var line = reader.ReadLine();
            if (line == null) break;
            if (line == "PROMPT")
            {
                Console.Write("> ");
                var cmd = Console.ReadLine() ?? string.Empty;
                writer.WriteLine(cmd);
                continue;
            }
            Console.WriteLine(line);
        }
    }
}

sealed class DiscoveryResponder : IDisposable
{
    private const int DiscoveryPort = 35501; // UDP discovery port
    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _tcpPort;

    public DiscoveryResponder(int tcpPort)
    {
        _tcpPort = tcpPort;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, DiscoveryPort)) { EnableBroadcast = true };
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        try
        {
#if NET6_0_OR_GREATER
            while (!_cts.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync(_cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch { continue; }
                var msg = Encoding.UTF8.GetString(res.Buffer);
                if (msg.StartsWith("ETBBS_LB_FATE_DISCOVER", StringComparison.Ordinal))
                {
                    var reply = Encoding.UTF8.GetBytes($"ETBBS_LB_FATE_HOST {_tcpPort}");
                    try { await _udp.SendAsync(reply, res.RemoteEndPoint).ConfigureAwait(false); } catch { }
                }
            }
#else
            while (!_cts.IsCancellationRequested)
            {
                var res = await _udp.ReceiveAsync();
                var msg = Encoding.UTF8.GetString(res.Buffer);
                if (msg.StartsWith("ETBBS_LB_FATE_DISCOVER", StringComparison.Ordinal))
                {
                    var reply = Encoding.UTF8.GetBytes($"ETBBS_LB_FATE_HOST {_tcpPort}");
                    try { await _udp.SendAsync(reply, res.RemoteEndPoint); } catch { }
                }
            }
#endif
        }
        catch { }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _udp.Close(); } catch { }
        try { _udp.Dispose(); } catch { }
    }
}
