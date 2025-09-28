namespace LB_FATE.MauiClient;

public partial class SettingsPage : ContentPage
{
    private bool _initialized;
    public SettingsPage()
    {
        InitializeComponent();
        Title = AppResources.Settings;
        AutoReconnectLabel.Text = AppResources.AutoReconnect;
        // Update hint label under switch if present
        if (AutoReconnectLabel.Parent is Layout layout)
        {
            foreach (var c in layout.Children)
            {
                if (c is Label l && l != AutoReconnectLabel) { l.Text = AppResources.AutoReconnectHint; break; }
            }
        }
        DelayLabel.Text = AppResources.ReconnectDelaySeconds;
        MaxAttemptsLabel.Text = AppResources.MaxAttempts;
        LanguageLabel.Text = AppResources.Language;

        AutoReconnectSwitch.IsToggled = AppSettings.AutoReconnect;
        DelaySlider.Value = AppSettings.ReconnectDelaySeconds;
        MaxAttemptsSlider.Value = AppSettings.ReconnectMaxAttempts;
        DelayValue.Text = AppSettings.ReconnectDelaySeconds.ToString();
        MaxAttemptsValue.Text = AppSettings.ReconnectMaxAttempts.ToString();

        // Language list
        LanguagePicker.ItemsSource = new List<string>
        {
            AppResources.SystemDefault,
            AppResources.English,
            AppResources.ChineseSimplified
        };
        LanguagePicker.SelectedIndex = AppSettings.UiLanguage switch
        {
            "en" => 1,
            "zh-Hans" => 2,
            _ => 0
        };

        _initialized = true;
    }

    private void OnAutoReconnectToggled(object? sender, ToggledEventArgs e)
    {
        if (!_initialized) return;
        AppSettings.AutoReconnect = e.Value;
    }

    private void OnDelayChanged(object? sender, ValueChangedEventArgs e)
    {
        if (!_initialized) return;
        var val = (int)Math.Round(e.NewValue);
        AppSettings.ReconnectDelaySeconds = val;
        DelayValue.Text = val.ToString();
    }

    private void OnMaxAttemptsChanged(object? sender, ValueChangedEventArgs e)
    {
        if (!_initialized) return;
        var val = (int)Math.Round(e.NewValue);
        AppSettings.ReconnectMaxAttempts = val;
        MaxAttemptsValue.Text = val.ToString();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!_initialized) return;
        var idx = LanguagePicker.SelectedIndex;
        var code = idx switch { 1 => "en", 2 => "zh-Hans", _ => "system" };
        AppSettings.UiLanguage = code;
        AppResources.OverrideCulture = code == "system" ? null : new System.Globalization.CultureInfo(code);
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        AppSettings.AutoReconnect = true;
        AppSettings.ReconnectDelaySeconds = 2;
        AppSettings.ReconnectMaxAttempts = 10;
        AppSettings.UiLanguage = "system";

        AutoReconnectSwitch.IsToggled = AppSettings.AutoReconnect;
        DelaySlider.Value = AppSettings.ReconnectDelaySeconds;
        MaxAttemptsSlider.Value = AppSettings.ReconnectMaxAttempts;
        DelayValue.Text = AppSettings.ReconnectDelaySeconds.ToString();
        MaxAttemptsValue.Text = AppSettings.ReconnectMaxAttempts.ToString();
        LanguagePicker.SelectedIndex = 0;
        AppResources.OverrideCulture = null;
        // notifications suppressed per requirements
    }
}
