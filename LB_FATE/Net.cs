using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

interface IPlayerEndpoint : IDisposable
{
    string Id { get; }
    void SendLine(string text);
    string? ReadLine();
    bool IsAlive { get; }
}

class ConsoleEndpoint : IPlayerEndpoint
{
    public string Id { get; }
    public ConsoleEndpoint(string id) { Id = id; }
    public void SendLine(string text) { Console.WriteLine(text); }
    public string? ReadLine() { return Console.ReadLine(); }
    public bool IsAlive => true;
    public void Dispose() { }
}

class TcpPlayerEndpoint : IPlayerEndpoint
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private volatile bool _closed = false;
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
        if (_closed) return;
        lock (_lock)
        {
            try { _writer.WriteLine(text); }
            catch { try { _client.Close(); } catch { } _closed = true; }
        }
    }

    public string? ReadLine()
    {
        try
        {
            var s = _reader.ReadLine();
            if (s == null) { _closed = true; return "pass"; }
            return s;
        }
        catch { _closed = true; return "pass"; }
    }

    public void Dispose()
    {
        try { _reader.Dispose(); } catch { }
        try { _writer.Dispose(); } catch { }
        try { _client.Close(); } catch { }
    }

    public bool IsAlive => !_closed && _client.Connected;
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
            try { Console.WriteLine($"[NET] Player connected and assigned: {pid} from {(client.Client.RemoteEndPoint?.ToString() ?? "?")}"); } catch { }
        }
        return map;
    }

    // Background reconnection loop: attach new Tcp clients to offline seats among provided ids.
    public void StartReconnections(IEnumerable<string> seatIds, Func<string, bool> isOccupied, Action<string, IPlayerEndpoint> attach)
    {
        _ = Task.Run(async () =>
        {
            var seats = seatIds.ToArray();
            while (_started)
            {
                TcpClient? client = null;
                try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch { continue; }
                try
                {
                    // pick first offline seat
                    string? pid = seats.FirstOrDefault(id => !isOccupied(id));
                    if (pid == null)
                    {
                        // no offline seats; reject
                        using var tmp = client;
                        try
                        {
                            var stream = tmp.GetStream();
                            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                            writer.WriteLine("SERVER: no offline seats available");
                        }
                        catch { }
                        try { Console.WriteLine($"[NET] Reconnection rejected (no seat): {(client.Client.RemoteEndPoint?.ToString() ?? "?")}"); } catch { }
                        continue;
                    }
                    var ep = new TcpPlayerEndpoint(pid, client);
                    try { Console.WriteLine($"[NET] Player reconnected to {pid} from {(client.Client.RemoteEndPoint?.ToString() ?? "?")}"); } catch { }
                    attach(pid, ep);
                }
                catch { try { client?.Close(); } catch { } }
            }
        });
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
        bool auto = false; int maxAttempts = 0; int delaySec = 3;
        try
        {
            var v = Environment.GetEnvironmentVariable("LB_FATE_CLIENT_AUTO_RECONNECT");
            auto = v != null && (v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase));
            if (int.TryParse(Environment.GetEnvironmentVariable("LB_FATE_CLIENT_RECONNECT_MAX"), out var m)) maxAttempts = Math.Max(0, m);
            if (int.TryParse(Environment.GetEnvironmentVariable("LB_FATE_CLIENT_RECONNECT_DELAY"), out var d)) delaySec = Math.Max(1, d);
        }
        catch { }

        int attempt = 0;
        for (;;)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(host, port);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

                Console.WriteLine($"Connected to {host}:{port}");
                while (true)
                {
                    string? line;
                    try { line = reader.ReadLine(); }
                    catch { Console.WriteLine("连接已断开。"); break; }
                    if (line == null) { Console.WriteLine("连接已断开。"); break; }
                    if (line == "PROMPT")
                    {
                        Console.Write("> ");
                        var cmd = Console.ReadLine() ?? string.Empty;
                        try { writer.WriteLine(cmd); } catch { Console.WriteLine("发送失败，连接已断开。"); break; }
                        continue;
                    }
                    Console.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }

            if (!auto && maxAttempts <= 0) break;
            attempt++;
            if (maxAttempts > 0 && attempt > maxAttempts) { Console.WriteLine("Reached max reconnect attempts."); break; }
            Console.WriteLine($"Reconnecting in {delaySec}s... (attempt {attempt})");
            try { Thread.Sleep(TimeSpan.FromSeconds(delaySec)); } catch { }
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
