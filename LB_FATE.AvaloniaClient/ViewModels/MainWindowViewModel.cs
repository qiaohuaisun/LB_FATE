namespace LB_FATE.AvaloniaClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public GameViewModel GameViewModel { get; }

    public MainWindowViewModel()
    {
        GameViewModel = new GameViewModel();
    }
}
