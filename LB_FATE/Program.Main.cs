using LB_FATE;

class Program
{
    static void Main(string[] args)
    {
        // Ensure UTF-8 console I/O for correct Chinese display on Windows terminals
        System.Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        System.Console.InputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.WriteLine("LB_FATE - Console Turn-based 2D Grid (ETBBS)");
        string? rolesDir = null;
        bool host = false, client = false;
        string hostName = "127.0.0.1";
        int port = 35500;
        int players = 7;
        int mapW = 15, mapH = 9;
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

        if (client)
        {
            try { NetClient.Run(hostName, port); }
            catch (Exception ex)
            {
                Console.WriteLine($"Client fatal error: {ex.Message}");
            }
            return;
        }
        if (host)
        {
            using var server = new NetServer(port);
            server.Start();
            var playerSeats = Math.Max(1, Math.Min(7, players));
            var endpoints = server.WaitForPlayers(playerSeats);
            Console.WriteLine("Hosting: games will auto-restart. Press Ctrl+C to stop.");
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
        Console.WriteLine("Local mode: 7 players on one console.");
        while (true)
        {
            new Game(rolesDir, 7, null, mapW, mapH).Run();
            Console.WriteLine();
            Console.Write("Start another local game? (Y/n): ");
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
            Console.WriteLine("|         NEW GAME STARTING        |");
            Console.WriteLine("+==================================+");
            Console.ResetColor();
            for (int s = seconds; s >= 1; s--)
            {
                Console.WriteLine($"Starting in {s}...");
                if (endpoints != null)
                {
                    foreach (var ep in endpoints.Values)
                    {
                        try
                        {
                            ep.SendLine("==================================");
                            ep.SendLine("NEW GAME STARTING");
                            ep.SendLine($"Starting in {s}...");
                            ep.SendLine("==================================");
                        }
                        catch { }
                    }
                }
                Thread.Sleep(800);
            }
        }
        catch { }
    }
}
