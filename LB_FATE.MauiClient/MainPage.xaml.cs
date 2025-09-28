using System.Net.Sockets;
using System.Text;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace LB_FATE.MauiClient;

public partial class MainPage : ContentPage
{
    public static MainPage? Instance { get; private set; }
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _reconnectCts;
    private readonly object _logLock = new();
    private readonly ObservableCollection<string> _logItems = new();
    private const int MaxLogItems = 500;
    private readonly ObservableCollection<PlayerCard> _players = new();
    private bool _capturingBoard = false;
    private readonly List<string> _boardRows = new();
    private List<string> _lastBoardRows = new();
    private double _lastComputedCell = 22;
    private int _boardDay = 0, _boardPhase = 0;
    private bool _promptReady = false;
    private bool _manualDisconnect = false;
    private string? _lastHost;
    private int _lastPort;

    public MainPage()
    {
        InitializeComponent();
        Instance = this;
        UpdateLocalizedTexts();
        UpdateStatus(false);
        SetPromptState(false);
        LogList.ItemsSource = _logItems;
        PlayersList.ItemsSource = _players;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLocalizedTexts();
    }

    private void UpdateLocalizedTexts()
    {
        Title = AppResources.AppTitle;
        HostEntry.Placeholder = AppResources.HostPlaceholder;
        PortEntry.Placeholder = AppResources.PortPlaceholder;
        StatusLabel.Text = AppResources.StatusNotConnected;
        SendBtn.Text = AppResources.Send;
        // using bottom TabBar via Shell now; no toolbar needed here
    }

    private void AppendLog(string line)
    {
        lock (_logLock)
        {
            // First, consume visual-only lines (board/legend). If consumed, do not add to log list.
            if (ConsumeBoard(line) || ConsumeLegend(line)) return;

            var indexBefore = _logItems.Count;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _logItems.Add(line);
                // Trim old logs to keep UI responsive
                const int trimTo = MaxLogItems;
                if (_logItems.Count > trimTo)
                {
                    var removeCount = _logItems.Count - trimTo;
                    for (int i = 0; i < removeCount; i++) _logItems.RemoveAt(0);
                }
                try
                {
                    // Ensure it snaps to the latest item reliably
                    var lastIndex = _logItems.Count - 1;
                    if (lastIndex >= 0)
                        LogList.ScrollTo(lastIndex, position: ScrollToPosition.End, animate: false);
                }
                catch { }
            });
        }
    }

    private bool ConsumeBoard(string line)
    {
        // Parse header like: === LB_FATE | Day X | Phase Y ===
        if (line.StartsWith("=== LB_FATE"))
        {
            try
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    var dayStr = parts[1].Trim(); // Day X
                    var phaseStr = parts[2].Replace("===", string.Empty).Trim(); // Phase Y ===
                    if (dayStr.StartsWith("Day") && int.TryParse(dayStr.Substring(3).Trim(), out var d)) _boardDay = d;
                    if (phaseStr.StartsWith("Phase") && int.TryParse(phaseStr.Substring(5).Trim(), out var p)) _boardPhase = p;
                }
            }
            catch { }
        }

        static bool IsBorder(string s)
            => s.Length >= 2 && s.StartsWith("+") && s.EndsWith("+") && s.AsSpan(1, s.Length - 2).ToArray().All(c => c == '-');

        static bool IsRow(string s) => s.Length >= 2 && s.StartsWith("|") && s.EndsWith("|");

        if (!_capturingBoard)
        {
            if (IsBorder(line)) { _capturingBoard = true; _boardRows.Clear(); return true; }
        }
        else
        {
            if (IsRow(line))
            {
                var inner = line.Substring(1, line.Length - 2);
                _boardRows.Add(inner);
                return true;
            }
            if (IsBorder(line))
            {
                // Finish and render
                var rows = _boardRows.ToList();
                _capturingBoard = false; _boardRows.Clear();
                MainThread.BeginInvokeOnMainThread(() => RenderBoard(rows));
                return true;
            }
        }
        return false;
    }

    private void RenderBoard(List<string> rows)
    {
        if (rows.Count == 0) return;
        int h = rows.Count; int w = rows[0].Length;
        BoardTitleLabel.Text = $"Board (Day {_boardDay}, Phase {_boardPhase})";

        // Keep last rows for adaptive resizing on container SizeChanged
        _lastBoardRows = rows.ToList();

        // Compute adaptive square cell size based on available area
        double cellSize = GetAdaptiveCellSize(w, h);
        BoardGrid.Children.Clear();
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();
        for (int r = 0; r < h; r++) BoardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int c = 0; c < w; c++) BoardGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        Color ColorFor(char ch)
        {
            return ch switch
            {
                '.' => Color.FromArgb("#F3F4F6"),
                'o' => Color.FromArgb("#FEF3C7"),
                'x' => Color.FromArgb("#FECACA"),
                '1' => Color.FromArgb("#EF4444"),
                '2' => Color.FromArgb("#F59E0B"),
                '3' => Color.FromArgb("#FBBF24"),
                '4' => Color.FromArgb("#10B981"),
                '5' => Color.FromArgb("#3B82F6"),
                '6' => Color.FromArgb("#6366F1"),
                '7' => Color.FromArgb("#8B5CF6"),
                _ => Color.FromArgb("#E5E7EB")
            };
        }

        for (int r = 0; r < h; r++)
        {
            var line = rows[r];
            for (int c = 0; c < w; c++)
            {
                var ch = line[c];
                var cell = new Grid();
                var bg = new Border
                {
                    Background = new SolidColorBrush(ColorFor(ch)),
                    Stroke = new SolidColorBrush(Color.FromArgb("#D1D5DB")),
                    StrokeThickness = 1,
                    WidthRequest = cellSize,
                    HeightRequest = cellSize
                };
                cell.Add(bg);
                if (char.IsDigit(ch))
                {
                    var lbl = new Label
                    {
                        Text = ch.ToString(),
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        TextColor = Colors.White,
                        FontAttributes = FontAttributes.Bold
                    };
                    cell.Add(lbl);
                }
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                BoardGrid.Add(cell);
            }
        }
    }

    private double GetAdaptiveCellSize(int cols, int rows)
    {
        // Prefer fitting width; also consider height if available
        double availW = (BoardScroll?.Width > 0 ? BoardScroll.Width : this.Width);
        double availH = (BoardScroll?.Height > 0 ? BoardScroll.Height : -1);
        // account for small paddings/margins inside card
        double pad = 8;
        double sizeW = (availW > 0 && cols > 0) ? Math.Floor((availW - pad) / cols) : double.NaN;
        double cell = (!double.IsNaN(sizeW) && sizeW > 0) ? sizeW : 22;
        if (availH > 0 && rows > 0)
        {
            double sizeH = Math.Floor((availH - pad) / rows);
            if (sizeH > 0) cell = Math.Min(cell, sizeH);
        }
        cell = Math.Max(10, Math.Min(40, cell));
        _lastComputedCell = cell;
        return cell;
    }

    private void OnBoardAreaSizeChanged(object? sender, EventArgs e)
    {
        if (_lastBoardRows.Count > 0)
        {
            // Re-render with new size; avoid locking since we're on UI thread
            RenderBoard(_lastBoardRows.ToList());
        }
    }

    // ---------- Legend/Status parsing to player cards ----------
    private bool _capturingLegend = false;
    private bool ConsumeLegend(string line)
    {
        if (line.StartsWith("Legend / Status:"))
        {
            _capturingLegend = true;
            _players.Clear();
            return true;
        }
        if (!_capturingLegend) return false;
        // Terminate conditions
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Commands:") || line.StartsWith("Costs") || line.StartsWith("Phases") || line.StartsWith("Recent:"))
        {
            _capturingLegend = false;
            return true;
        }
        // Example: " 1: P1 Saber       HP[#####.....](30/40) MP=5 Pos=(1,2)"
        // Regex groups: symbol, id, class, hp, maxhp, mp, pos
        var rx = new Regex(@"\s*(?<sym>\d):\s*(?<id>P\d+)\s+(?<class>\S+)\s+HP\[[#\.]+\]\((?<hp>\d+)\/(?<max>\d+)\)\s+MP=(?<mp>[^\s]+)\s+Pos=\((?<x>-?\d+),(?<y>-?\d+)\)");
        var m = rx.Match(line);
        if (!m.Success) return false;

        var mpRaw = m.Groups["mp"].Value;
        int mpVal = 0;
        int maxMpVal = 10; // will be overridden; no-max => MaxMp = Mp (full bar)
        if (mpRaw.Contains('/'))
        {
            var parts = mpRaw.Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                _ = int.TryParse(parts[0], out mpVal);
                if (!int.TryParse(parts[1], out maxMpVal)) maxMpVal = Math.Max(1, mpVal);
            }
        }
        else
        {
            _ = int.TryParse(mpRaw, out mpVal);
            maxMpVal = Math.Max(1, mpVal);
            // Make bar full when there is no explicit max
            mpVal = Math.Max(1, maxMpVal);
        }

        var card = new PlayerCard
        {
            Symbol = m.Groups["sym"].Value,
            Id = m.Groups["id"].Value,
            Class = m.Groups["class"].Value,
            Hp = int.TryParse(m.Groups["hp"].Value, out var hpv) ? hpv : 0,
            MaxHp = int.TryParse(m.Groups["max"].Value, out var mhv) ? mhv : 1,
            MpText = mpRaw,
            Mp = mpVal,
            MaxMp = Math.Max(1, maxMpVal),
            Pos = $"({m.Groups["x"].Value},{m.Groups["y"].Value})",
        };
        card.Color = ColorForSymbol(card.Symbol);
        _players.Add(card);
        return true;
    }

    private static Color ColorForSymbol(string sym)
        => (sym) switch
        {
            "1" => Color.FromArgb("#EF4444"),
            "2" => Color.FromArgb("#F59E0B"),
            "3" => Color.FromArgb("#FBBF24"),
            "4" => Color.FromArgb("#10B981"),
            "5" => Color.FromArgb("#3B82F6"),
            "6" => Color.FromArgb("#6366F1"),
            "7" => Color.FromArgb("#8B5CF6"),
            _ => Color.FromArgb("#9CA3AF")
        };

    public sealed class PlayerCard
    {
        public string Symbol { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public int Hp { get; set; }
        public int MaxHp { get; set; } = 1;
        public string MpText { get; set; } = "0";
        public int Mp { get; set; }
        public int MaxMp { get; set; } = 10;
        public string Pos { get; set; } = string.Empty;
        public string HpText => $"{Hp}/{MaxHp}";
        public double HpRatio => Math.Max(0.0, Math.Min(1.0, MaxHp > 0 ? (double)Hp / MaxHp : 0.0));
        public double MpRatio => Math.Max(0.0, Math.Min(1.0, MaxMp > 0 ? (double)Mp / MaxMp : 0.0));
        public Color Color { get; set; } = Color.FromArgb("#9CA3AF");

        // Visual helpers for nicer status presentation
        public Color HpBarColor
        {
            get
            {
                var r = HpRatio;
                if (r >= 0.6) return Color.FromArgb("#10B981"); // green
                if (r >= 0.3) return Color.FromArgb("#F59E0B"); // amber
                return Color.FromArgb("#EF4444"); // red
            }
        }
        public Color HpTextColor => HpBarColor;
        public Color MpBarColor => Color.FromArgb("#3B82F6");
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        // stop any auto-reconnect loop
        try { _reconnectCts?.Cancel(); } catch { }

        if (_client is not null)
        {
            _manualDisconnect = true;
            await DisconnectAsync();
            return;
        }

        var host = HostEntry.Text?.Trim();
        if (string.IsNullOrEmpty(host)) { await DisplayAlert(AppResources.ErrorTitle, AppResources.ErrorHostRequired, "OK"); return; }
        if (!int.TryParse(PortEntry.Text?.Trim(), out var port)) { await DisplayAlert(AppResources.ErrorTitle, AppResources.ErrorInvalidPort, "OK"); return; }
        _manualDisconnect = false;
        _lastHost = host; _lastPort = port;
        var ok = await ConnectToAsync(host, port);
        if (!ok)
        {
            if (AppSettings.AutoReconnect)
            {
                // notifications suppressed
                StartAutoReconnect(host, port);
            }
            else
            {
                // notifications suppressed
            }
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _reader is not null)
            {
                var line = await _reader.ReadLineAsync();
                if (line is null) break;
                if (line.Equals("PROMPT", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("> ");
                    MainThread.BeginInvokeOnMainThread(() => { SetPromptState(true); CmdEntry.Focus(); });
                }
                else
                {
                    AppendLog(line);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] {ex.Message}");
        }
        finally
        {
            await DisconnectAsync();
            if (!_manualDisconnect && AppSettings.AutoReconnect && !string.IsNullOrEmpty(_lastHost) && _lastPort > 0)
            {
                // notifications suppressed
                StartAutoReconnect(_lastHost!, _lastPort);
            }
        }
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var cmd = CmdEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(cmd)) return;
        if (_writer is null)
        {
            await DisplayAlert(AppResources.NotConnectedTitle, AppResources.NotConnectedMessage, "OK");
            return;
        }
        if (!_promptReady)
        {
            // notifications suppressed
            return;
        }
        try
        {
            await _writer.WriteLineAsync(cmd);
            CmdEntry.Text = string.Empty;
            SetPromptState(false);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.SendFailed, ex.Message, "OK");
        }
    }

    private void OnSendCompleted(object? sender, EventArgs e) => OnSendClicked(sender, e);

    private async void OnQuickCmdClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var cmd = btn.CommandParameter?.ToString() ?? btn.Text ?? string.Empty;
        CmdEntry.Text = cmd;
        await Task.Delay(10);
        OnSendClicked(sender, e);
    }

    private void OnClearLogsClicked(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _logItems.Clear());
    }

    // Discover moved to dedicated page

    private async Task DisconnectAsync()
    {
        try { _cts?.Cancel(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        _cts = null;
        _reader = null;
        _writer = null;
        _client = null;
        UpdateStatus(false);
        SetPromptState(false);
        await Task.CompletedTask;
    }

    private void UpdateStatus(bool connected)
    {
        StatusLabel.Text = connected ? AppResources.StatusConnected : AppResources.StatusNotConnected;
        StatusLabel.TextColor = connected ? Color.FromArgb("#065F46") : Color.FromArgb("#374151");
        EndpointLabel.IsVisible = connected && !string.IsNullOrEmpty(_lastHost) && _lastPort > 0;
        EndpointLabel.Text = EndpointLabel.IsVisible ? $"{_lastHost}:{_lastPort}" : string.Empty;

        // Chip background and dot color
        var success = (Color)Application.Current!.Resources["ColorSuccess"];
        var chipBgConnected = Color.FromArgb("#ECFDF5"); // light green
        var chipBgDisconnected = Color.FromArgb("#F3F4F6"); // light gray
        StatusChip.Background = new SolidColorBrush(connected ? chipBgConnected : chipBgDisconnected);
        StatusDot.BackgroundColor = connected ? success : Color.FromArgb("#9CA3AF");

        // Connect/Disconnect button styling
        ConnectBtn.Text = connected ? AppResources.Disconnect : AppResources.Connect;
        if (connected)
        {
            ConnectBtn.BackgroundColor = (Color)Application.Current!.Resources["ColorDanger"]; // red
            ConnectBtn.TextColor = Colors.White;
        }
        else
        {
            ConnectBtn.ClearValue(Button.BackgroundColorProperty);
            ConnectBtn.ClearValue(Button.TextColorProperty);
        }
    }

    private void SetPromptState(bool ready)
    {
        _promptReady = ready;
        // Entry: read-only when no prompt; keep enabled for selection
        try { CmdEntry.IsReadOnly = !ready; } catch { /* property may not be available on very old targets */ }
        CmdEntry.Placeholder = ready ? AppResources.CommandPlaceholderReady : AppResources.CommandPlaceholderWaiting;
        SendBtn.IsEnabled = ready && _writer is not null;
    }

    private async Task<bool> ConnectToAsync(string host, int port)
    {
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(host, port);
            var stream = client.GetStream();
            var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true, NewLine = "\n" };

            // Swap in
            _client = client;
            _reader = reader;
            _writer = writer;
            _cts = new CancellationTokenSource();

            UpdateStatus(true);
            SetPromptState(false);
            AppendLog(string.Format(AppResources.ConnectedToFormat, host, port));
            _ = Task.Run(() => ReadLoopAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"{AppResources.ConnectFailedLogPrefix} {ex.Message}");
            await DisconnectAsync();
            return false;
        }
    }

    private void StartAutoReconnect(string host, int port)
    {
        try { _reconnectCts?.Cancel(); } catch { }
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;
        _ = Task.Run(async () =>
        {
            int attempt = 0;
            int maxAttempts = AppSettings.ReconnectMaxAttempts;
            int delaySec = AppSettings.ReconnectDelaySeconds;
            while (!token.IsCancellationRequested && _client is null)
            {
                attempt++;
                if (maxAttempts > 0 && attempt > maxAttempts) break;
                AppendLog(string.Format(AppResources.ReconnectAttemptLogFormat, attempt, delaySec));
                // notifications suppressed
                try { await Task.Delay(TimeSpan.FromSeconds(delaySec), token); } catch { break; }
                if (token.IsCancellationRequested) break;
                var ok = await MainThread.InvokeOnMainThreadAsync(() => ConnectToAsync(host, port));
                if (ok)
                {
                    // notifications suppressed
                    break;
                }
            }
        }, token);
    }

    // Deprecated toast kept for compatibility if needed

    private async Task OpenDiscoverAsync()
    {
        await Navigation.PushAsync(new DiscoverPage((host, port) => ApplyHostPort(host, port)));
    }

    private async Task OpenSettingsAsync()
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    public void ApplyHostPort(string host, int port)
    {
        HostEntry.Text = host;
        PortEntry.Text = port.ToString();
        AppendLog(string.Format(AppResources.ConnectedToFormat, host, port));
    }
}
