using System.Collections.ObjectModel;

namespace LB_FATE.MauiClient;

public partial class SmartInputPage : ContentPage
{
    public enum InputMode
    {
        Move,
        Attack,
        Skill,
        GoTo
    }

    private readonly InputMode _mode;
    private readonly Action<string> _commandCallback;
    private readonly ObservableCollection<MainPage.PlayerCard> _players;

    private string _selectedDirection = "";
    private MainPage.PlayerCard? _selectedPlayer;
    private int _skillNumber = 0;

    public SmartInputPage(InputMode mode, Action<string> commandCallback, IEnumerable<MainPage.PlayerCard>? players = null)
    {
        InitializeComponent();
        _mode = mode;
        _commandCallback = commandCallback;
        _players = new ObservableCollection<MainPage.PlayerCard>(players ?? []);

        SetupUI();
        UpdateCommandPreview();
    }

    private void SetupUI()
    {
        switch (_mode)
        {
            case InputMode.Move:
                ActionTitle.Text = "🚶 Move Command";
                ActionDescription.Text = "Select destination coordinates";
                CoordinateSection.IsVisible = true;
                break;

            case InputMode.Attack:
                ActionTitle.Text = "⚔️ Attack Command";
                ActionDescription.Text = "Select target player or coordinates";
                PlayerSection.IsVisible = true;
                CoordinateSection.IsVisible = true;
                DirectionSection.IsVisible = true;
                PlayerSelector.ItemsSource = _players.Where(p => !p.Offline);
                break;

            case InputMode.Skill:
                ActionTitle.Text = "✨ Skill Command";
                ActionDescription.Text = "Select skill and target";
                SkillSection.IsVisible = true;
                PlayerSection.IsVisible = true;
                CoordinateSection.IsVisible = true;
                DirectionSection.IsVisible = true;
                PlayerSelector.ItemsSource = _players;
                break;

            case InputMode.GoTo:
                ActionTitle.Text = "📍 Go To";
                ActionDescription.Text = "Quick movement to coordinates";
                CoordinateSection.IsVisible = true;
                break;
        }
    }

    private void OnCoordinateChanged(object? sender, ValueChangedEventArgs e)
    {
        XValueLabel.Text = ((int)XStepper.Value).ToString();
        YValueLabel.Text = ((int)YStepper.Value).ToString();
        CoordinatePreview.Text = $"Target: ({(int)XStepper.Value}, {(int)YStepper.Value})";
        UpdateCommandPreview();
    }

    private void OnPlayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MainPage.PlayerCard player)
        {
            _selectedPlayer = player;
            UpdateCommandPreview();
        }
    }

    private void OnDirectionClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string direction)
        {
            _selectedDirection = direction;
            DirectionPreview.Text = $"Direction: {direction.ToUpper()}";
            UpdateCommandPreview();
        }
    }

    private void OnSkillNumberChanged(object? sender, TextChangedEventArgs e)
    {
        if (int.TryParse(e.NewTextValue, out var number) && number > 0)
        {
            _skillNumber = number;
            SkillPreview.Text = $"Skill: #{number}";
        }
        else
        {
            _skillNumber = 0;
            SkillPreview.Text = "Skill: Invalid number";
        }
        UpdateCommandPreview();
    }

    private void UpdateCommandPreview()
    {
        string command = "";

        switch (_mode)
        {
            case InputMode.Move:
            case InputMode.GoTo:
                command = $"move {(int)XStepper.Value} {(int)YStepper.Value}";
                break;

            case InputMode.Attack:
                if (_selectedPlayer != null)
                {
                    command = $"attack {_selectedPlayer.Id}";
                }
                else if (!string.IsNullOrEmpty(_selectedDirection))
                {
                    command = $"attack {_selectedDirection}";
                }
                else
                {
                    command = $"attack {(int)XStepper.Value} {(int)YStepper.Value}";
                }
                break;

            case InputMode.Skill:
                if (_skillNumber > 0)
                {
                    if (_selectedPlayer != null)
                    {
                        command = $"use {_skillNumber} {_selectedPlayer.Id}";
                    }
                    else if (!string.IsNullOrEmpty(_selectedDirection))
                    {
                        command = $"use {_skillNumber} {_selectedDirection}";
                    }
                    else
                    {
                        command = $"use {_skillNumber} {(int)XStepper.Value} {(int)YStepper.Value}";
                    }
                }
                else
                {
                    command = "use [skill number required]";
                }
                break;
        }

        CommandPreview.Text = command;
    }

    private async void OnExecuteClicked(object? sender, EventArgs e)
    {
        var command = CommandPreview.Text;
        if (!string.IsNullOrEmpty(command) && !command.Contains("[") && !command.Contains("Invalid"))
        {
            _commandCallback(command);
            await Navigation.PopModalAsync();
        }
        else
        {
            await DisplayAlert("Invalid Command", "Please complete all required fields", "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}