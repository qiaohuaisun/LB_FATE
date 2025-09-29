using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ETBBS;
using Xunit;

public class LBFateE2ETests
{
    private sealed class TestClient : IDisposable
    {
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _lock = new();
        public List<string> Lines { get; } = new();
        private int _promptCount = 0;
        public int PromptCount => System.Threading.Volatile.Read(ref _promptCount);
        public string Id { get; }

        public TestClient(string id, string host, int port)
        {
            Id = id;
            _client = new TcpClient();
            _client.Connect(host, port);
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            Task.Run(ReadLoop);
        }

        private async Task ReadLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var line = await _reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    lock (_lock) Lines.Add(line);
                    if (line == "PROMPT") Interlocked.Increment(ref _promptCount);
                }
            }
            catch { }
        }

        public void Send(string text)
        {
            lock (_lock) _writer.WriteLine(text);
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _writer.Dispose(); } catch { }
            try { _reader.Dispose(); } catch { }
            try { _client.Close(); } catch { }
        }
    }

    private static string CreateAllClassRolesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lbfate-tests-roles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string[] classes = new[] { "saber", "rider", "archer", "lancer", "caster", "berserker", "assassin" };
        foreach (var cls in classes)
        {
            var roleName = char.ToUpper(cls[0]) + cls.Substring(1) + " X";
            var text =
                "role \"" + roleName + "\" id \"" + cls + "_x\" {\n" +
                "  vars { \"hp\" = 25; \"max_hp\" = 25; \"mp\" = 10.0; \"atk\" = 5; \"def\" = 0; \"matk\" = 5; \"range\" = 2; \"speed\" = 3 }\n" +
                "  skills {\n" +
                "    skill \"Boost\" { range 5; targeting any; cost mp 0; cooldown 0; add global tag \"force_act_now\" }\n" +
                "  }\n" +
                "}\n";
            File.WriteAllText(Path.Combine(dir, cls + ".lbr"), text);
        }
        return dir;
    }

    private static object Invoke(object target, string method, params object?[] args)
    {
        var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return mi.Invoke(target, args)!;
    }

    private static T GetField<T>(object target, string name)
    {
        var fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return (T)fi.GetValue(target)!;
    }

    private static void SetField(object target, string name, object value)
    {
        var fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        fi.SetValue(target, value);
    }

    [Fact]
    public void EndToEnd_ForceActNow_Triggers_TargetReady_For_ExtraTurn()
    {
        // Ensure server logs are enabled to help debugging (optional)
        Environment.SetEnvironmentVariable("LB_FATE_SERVER_LOGS", "1");

        var rolesDir = CreateAllClassRolesDirectory();
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetType("LB_FATE.Game") != null);
        if (asm == null)
        {
            try { asm = Assembly.Load("LB_FATE"); } catch { }
        }
        if (asm == null)
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var dllPath = Path.Combine(baseDir, "LB_FATE.dll");
            if (File.Exists(dllPath)) asm = Assembly.LoadFrom(dllPath);
        }
        Assert.NotNull(asm);
        var netServerType = asm.GetTypes().First(t => t.Name == "NetServer");
        var gameType = asm.GetType("LB_FATE.Game")!;

        int port = 35600 + new Random().Next(1000);
        var server = Activator.CreateInstance(netServerType, new object[] { port })!;
        Invoke(server, "Start");

        // Start waiting on server for two players
        var waitTask = Task.Run(() => Invoke(server, "WaitForPlayers", 2));

        using var c1 = new TestClient("P1", "127.0.0.1", port);
        using var c2 = new TestClient("P2", "127.0.0.1", port);

        // Get endpoints map from server
        var endpointMap = waitTask.GetAwaiter().GetResult();

        // Create Game with endpoints and roles dir, small map to keep units closer
        var game = Activator.CreateInstance(gameType, new object?[] { rolesDir, 2, endpointMap, 7, 5 })!;
        // Initialize world
        Invoke(game, "InitWorld");

        // Place P1 and P2 within range to ensure Boost can target
        var state = GetField<WorldState>(game, "state");
        state = WorldStateOps.WithUnit(state, "P1", u => u with { Vars = u.Vars.SetItem(Keys.Pos, new Coord(1, 1)) });
        state = WorldStateOps.WithUnit(state, "P2", u => u with { Vars = u.Vars.SetItem(Keys.Pos, new Coord(3, 1)) });
        SetField(game, "state", state);

        // P1 client logic: on first PROMPT send skills, on second PROMPT use Boost on P2, then pass
        var p1Worker = Task.Run(async () =>
        {
            int seenPrompts = 0;
            var sw = new System.Diagnostics.Stopwatch(); sw.Start();
            var lastSkills = new List<string>();
            while (sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                // Snapshot lines
                string[] lines;
                lock (c1.Lines) lines = c1.Lines.ToArray();
                if (c1.PromptCount > seenPrompts)
                {
                    seenPrompts = c1.PromptCount;
                    if (seenPrompts == 1)
                    {
                        c1.Send("skills");
                    }
                    else if (seenPrompts == 2)
                    {
                        // Parse last skills dump
                        var idx = -1;
                        foreach (var ln in lines)
                        {
                            var m = Regex.Match(ln, @"^\s*\[(\d+)\]\s+([^\(]+)\(");
                            if (m.Success)
                            {
                                var num = int.Parse(m.Groups[1].Value);
                                var name = m.Groups[2].Value.Trim();
                                lastSkills.Add($"{num}:{name}");
                                if (string.Equals(name, "Boost", StringComparison.OrdinalIgnoreCase)) idx = num;
                            }
                        }
                        if (idx >= 0) c1.Send($"use {idx} P2"); else c1.Send("pass");
                    }
                    else if (seenPrompts >= 3)
                    {
                        c1.Send("pass");
                        break;
                    }
                }
                await Task.Delay(10);
            }
        });

        // P2 client: always pass when prompted (including extra turn)
        var p2Worker = Task.Run(async () =>
        {
            int seen = 0;
            var sw = new System.Diagnostics.Stopwatch(); sw.Start();
            while (sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (c2.PromptCount > seen)
                {
                    seen = c2.PromptCount;
                    c2.Send("pass");
                    if (seen >= 1) break; // only respond once
                }
                await Task.Delay(10);
            }
        });

        // Call one turn for P1 (phase 1 day 1)
        Invoke(game, "Turn", "P1", 1, 1);
        Task.WaitAll(new Task[] { p1Worker }, 2000);

        // Verify force_act_now set and target is P2
        state = GetField<WorldState>(game, "state");
        bool tagPresent = state.Global.Tags.Contains("force_act_now") || (state.Global.Vars.TryGetValue("force_act_now", out var fv) && fv is bool fb && fb);
        Assert.True(tagPresent);
        var tgtId = state.Global.Vars.TryGetValue(DslRuntime.TargetKey, out var to) ? to as string : null;
        Assert.Equal("P2", tgtId);

        // Simulate forced action: call Turn for P2
        Invoke(game, "Turn", "P2", 1, 1);
        Task.WaitAll(new Task[] { p2Worker }, 2000);
        Assert.True(c2.PromptCount >= 1);
    }
}
