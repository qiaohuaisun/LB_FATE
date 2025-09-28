using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LB_FATE.MauiClient;

public partial class DiscoverPage : ContentPage
{
    public record HostItem(string Host, int Port);

    private readonly Action<string, int> _apply;
    private readonly List<HostItem> _items = new();

    public DiscoverPage(Action<string, int> apply)
    {
        InitializeComponent();
        _apply = apply;
        Title = AppResources.DiscoverTitle; // localized title
        RefreshBtn.Text = AppResources.Refresh;
        UseBtn.Text = AppResources.UseSelected;
    }

    public DiscoverPage() : this((host, port) => MainPage.Instance?.ApplyHostPort(host, port)) { }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await DiscoverAsync();
    }

    private async Task DiscoverAsync()
    {
        _items.Clear();
        HostsList.ItemsSource = null;
        const int discoveryPort = 35501;
        const string query = "ETBBS_LB_FATE_DISCOVER";
        var deadline = DateTime.UtcNow.AddMilliseconds(1500);
        try
        {
            using var udp = new UdpClient() { EnableBroadcast = true };
            var data = Encoding.UTF8.GetBytes(query);
            await udp.SendAsync(data, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
            while (DateTime.UtcNow < deadline)
            {
                var receiveTask = udp.ReceiveAsync();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(200));
                if (completed != receiveTask) continue;
                var res = receiveTask.Result;
                var msg = Encoding.UTF8.GetString(res.Buffer);
                if (msg.StartsWith("ETBBS_LB_FATE_HOST", StringComparison.Ordinal))
                {
                    var parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var port))
                    {
                        var host = res.RemoteEndPoint.Address.ToString();
                        if (!_items.Any(t => t.Host == host && t.Port == port))
                            _items.Add(new HostItem(host, port));
                    }
                }
            }
            HostsList.ItemsSource = _items;
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.DiscoveryFailed, ex.Message, "OK");
        }
    }

    private async void OnUseClicked(object? sender, EventArgs e)
    {
        if (HostsList.SelectedItem is HostItem item)
        {
            _apply(item.Host, item.Port);
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert(AppResources.ErrorTitle, AppResources.NoSelection, "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await DiscoverAsync();
    }
}
