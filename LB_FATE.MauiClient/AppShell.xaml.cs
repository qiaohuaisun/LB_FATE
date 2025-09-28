namespace LB_FATE.MauiClient;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        UpdateTitles();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateTitles();
    }

    private void UpdateTitles()
    {
        if (Items.FirstOrDefault() is TabBar tab)
        {
            if (tab.Items.Count >= 3)
            {
                if (tab.Items[0] is ShellSection s0) s0.Title = AppResources.AppTitle;
                if (tab.Items[1] is ShellSection s1) s1.Title = AppResources.DiscoverTitle;
                if (tab.Items[2] is ShellSection s2) s2.Title = AppResources.Settings;
            }
        }
    }
}
