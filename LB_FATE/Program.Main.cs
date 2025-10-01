using LB_FATE;
using LB_FATE.Logging;
using Serilog;
using Serilog.Events;

class Program
{
    static void Main(string[] args)
    {
        // Parse command-line flags
        bool enablePerfTracking = args.Contains("--perf") || args.Contains("--performance");
        bool verboseMode = args.Contains("--verbose") || args.Contains("-v");
        bool debugMode = args.Contains("--debug") || args.Contains("-d");

        // Determine log level from environment or flags
        var minLevel = DetermineLogLevel(verboseMode, debugMode);

        // Initialize logging system with performance tracking
        LoggerSetup.InitializeGlobalLogger(enableFileLogging: true, enablePerformanceMetrics: enablePerfTracking, minLevel: minLevel);

        try
        {
            RunApplication(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            LoggerSetup.CloseLogger();
        }
    }

    static void RunApplication(string[] args)
    {
        // Ensure UTF-8 console I/O for correct Chinese display on Windows terminals
        System.Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        System.Console.InputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        Log.Information("LB_FATE - Console Turn-based 2D Grid (ETBBS)");
        Console.WriteLine("LB_FATE - Console Turn-based 2D Grid (ETBBS)");
        string? rolesDir = null;
        bool host = false, client = false;
        string hostName = "127.0.0.1";
        int port = 35500;
        int players = 7;
        int mapW = 25, mapH = 15;
        string mode = Environment.GetEnvironmentVariable("LB_FATE_MODE") ?? "";
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--roles": if (i + 1 < args.Length) { rolesDir = args[++i]; } break;
                case "--host": host = true; break;
                case "--client": client = true; if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) { var addr = args[++i]; var parts = addr.Split(':'); hostName = parts[0]; if (parts.Length > 1 && int.TryParse(parts[1], out var pp)) port = pp; } break;
                case "--hostaddr": if (i + 1 < args.Length) hostName = args[++i]; break;
                case "--port": if (i + 1 < args.Length && int.TryParse(args[i + 1], out var p)) { port = p; i++; } break;
                case "--players": if (i + 1 < args.Length && int.TryParse(args[i + 1], out var n)) { players = Math.Max(1, Math.Min(7, n)); i++; } break;
                case "--mode":
                    if (i + 1 < args.Length)
                    {
                        var m = args[++i].ToLowerInvariant();
                        if (m is "boss" or "ffa") mode = m;
                    }
                    break;
                case "--size":
                    if (i + 1 < args.Length)
                    {
                        var s = args[++i];
                        var xParts = s.ToLowerInvariant().Split('x');
                        if (xParts.Length == 2 && int.TryParse(xParts[0], out var w) && int.TryParse(xParts[1], out var h))
                        { mapW = Math.Max(5, w); mapH = Math.Max(5, h); }
                    }
                    break;
                case "--width": if (i + 1 < args.Length && int.TryParse(args[i + 1], out var w0)) { mapW = Math.Max(5, w0); i++; } break;
                case "--height": if (i + 1 < args.Length && int.TryParse(args[i + 1], out var h0)) { mapH = Math.Max(5, h0); i++; } break;
            }
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            try
            {
                Environment.SetEnvironmentVariable("LB_FATE_MODE", mode, EnvironmentVariableTarget.Process);
                Log.Information("Game mode set to: {Mode}", mode);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set environment variable LB_FATE_MODE");
            }
        }

        if (client)
        {
            try
            {
                Log.Information("Starting client mode, connecting to {Host}:{Port}", hostName, port);
                NetClient.Run(hostName, port);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Client fatal error");
                Console.WriteLine($"客户端致命错误：{ex.Message}");
            }
            return;
        }
        if (host)
        {
            using var server = new NetServer(port);
            server.Start();
            var playerSeats = Math.Max(1, Math.Min(7, players));
            var endpoints = server.WaitForPlayers(playerSeats);
            Console.WriteLine("正在托管：游戏将自动重启。按 Ctrl+C 停止。");
            Game? currentGame = null;
            var seats = Enumerable.Range(1, playerSeats).Select(i => $"P{i}");
            server.StartReconnections(
                seatIds: seats,
                isOccupied: pid => currentGame?.HasEndpoint(pid) ?? false,
                attach: (pid, ep) => { currentGame?.AttachEndpoint(pid, ep); }
            );
            while (true)
            {
                currentGame = new Game(rolesDir, endpoints.Count, endpoints, mapW, mapH);
                currentGame.Run();
                // Inform clients with a prominent banner + countdown and continue to next game
                ShowNextGameBanner(endpoints, seconds: 3);
            }
        }
        Console.WriteLine("本地模式：7 名玩家在同一控制台。");
        while (true)
        {
            new Game(rolesDir, 7, null, mapW, mapH).Run();
            Console.WriteLine();
            Console.Write("开始新一局游戏？(Y/n): ");
            var resp = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(resp) && (resp.StartsWith("n", StringComparison.OrdinalIgnoreCase) || resp.StartsWith("q", StringComparison.OrdinalIgnoreCase)))
                break;
            ShowNextGameBanner(null, seconds: 3);
        }
    }

    private static void ShowNextGameBanner(Dictionary<string, IPlayerEndpoint>? endpoints, int seconds)
    {
        try
        {
            // Local console banner
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("+==================================+");
            Console.WriteLine("|          新游戏即将开始          |");
            Console.WriteLine("+==================================+");
            Console.ResetColor();
            for (int s = seconds; s >= 1; s--)
            {
                Console.WriteLine($"{s} 秒后开始...");
                if (endpoints != null)
                {
                    foreach (var ep in endpoints.Values)
                    {
                        try
                        {
                            ep.SendLine("==================================");
                            ep.SendLine("新游戏即将开始");
                            ep.SendLine($"{s} 秒后开始...");
                            ep.SendLine("==================================");
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to send countdown to client endpoint");
                        }
                    }
                }
                Thread.Sleep(800);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Countdown display interrupted");
        }
    }

    /// <summary>
    /// Determines the appropriate log level based on environment variables and command-line flags.
    /// Priority: command-line flags > environment variable > default (Information).
    /// </summary>
    static LogEventLevel DetermineLogLevel(bool verboseMode, bool debugMode)
    {
        // Command-line flags take highest priority
        if (verboseMode) return LogEventLevel.Verbose;
        if (debugMode) return LogEventLevel.Debug;

        // Check environment variable
        var envLevel = Environment.GetEnvironmentVariable("LB_FATE_LOG_LEVEL");
        if (!string.IsNullOrWhiteSpace(envLevel))
        {
            if (Enum.TryParse<LogEventLevel>(envLevel, ignoreCase: true, out var level))
            {
                return level;
            }
        }

        // Default: Information for production
        return LogEventLevel.Information;
    }
}
