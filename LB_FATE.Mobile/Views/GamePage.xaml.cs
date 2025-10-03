using LB_FATE.Mobile.ViewModels;

namespace LB_FATE.Mobile.Views;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _viewModel;

    // 横屏时的右侧信息区容器
    private bool _isLandscape = false;
    private Grid? _rightPanel = null;             // 右侧信息区容器
    private ScrollView? _rightScrollView = null;   // 右侧滚动容器

    // 台词显示需要的状态（供 BossQuote 部分类使用）
    private readonly Random _random = new();
    private readonly List<(double x, double y)> _activeQuotePositions = new(); // 活跃台词位置列表
    private string? _lastQuote = null;
    private DateTime _lastQuoteTime = DateTime.MinValue;
    private const int MaxConcurrentQuotes = 4; // 最多同时显示4个台词

    public GamePage(GameViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // 让 ViewModel 能调用本页面的台词动画
        _viewModel.SetGamePage(this);

#if WINDOWS
        // Hook native key handling for Windows to support Ctrl+Enter to send command
        this.Loaded += OnLoadedForWindows;
#endif
    }

    public void FocusCommandEntry()
    {
        try { CommandEntry?.Focus(); } catch { }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        bool isLandscape = width > height;
        if (isLandscape != _isLandscape)
        {
            _isLandscape = isLandscape;
            AdjustLayoutForOrientation(isLandscape);
            AdjustCellSize(width, height, isLandscape);
        }
    }

#if WINDOWS
    private void OnLoadedForWindows(object? sender, EventArgs e)
    {
        try
        {
            if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
            {
                fe.KeyDown -= OnWinKeyDown;
                fe.KeyDown += OnWinKeyDown;
            }
        }
        catch { }
    }

    private void OnWinKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        try
        {
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (ctrl && e.Key == Windows.System.VirtualKey.Enter)
            {
                if (_viewModel?.SendCommandCommand?.CanExecute(null) == true)
                {
                    _viewModel.SendCommandCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        catch { }
    }
#endif

    // 根据屏幕尺寸动态调整网格 CellSize，确保可读性
    private void AdjustCellSize(double width, double height, bool isLandscape)
    {
        float cellSize;
        int gridW = (_viewModel?.GridData?.Width > 0) ? _viewModel.GridData.Width : 25;
        int gridH = (_viewModel?.GridData?.Height > 0) ? _viewModel.GridData.Height : 15;

        if (isLandscape)
        {
            // 横屏：左侧 60% 区域放棋盘
            float availableWidth = (float)(width * 0.6 - 60);
            float availableHeight = (float)(height - 60);
            float widthCellSize = availableWidth / Math.Max(1, gridW);
            float heightCellSize = availableHeight / Math.Max(1, gridH);
            cellSize = Math.Min(widthCellSize, heightCellSize);
        }
        else
        {
            // 竖屏：棋盘占 40% 高度
            float availableWidth = (float)(width - 60);
            float availableHeight = (float)(height * 0.4 - 60);
            float widthCellSize = availableWidth / Math.Max(1, gridW);
            float heightCellSize = availableHeight / Math.Max(1, gridH);
            cellSize = Math.Min(widthCellSize, heightCellSize);
        }

        // 限制范围，防止过小或过大
        cellSize = Math.Max(20f, Math.Min(50f, cellSize));

        if (GridBoard != null)
        {
            GridBoard.CellSize = cellSize;
        }
    }

    // 横/竖屏切换时调整右侧信息区布局（尽量保持原逻辑与结构）
    private void AdjustLayoutForOrientation(bool isLandscape)
    {
        var dayPhaseBorder = FindElementByName("DayPhaseBorder");
        var statusBorder = FindElementByName("StatusBorder");
        var gridView = FindElementByName("GridViewBorder");
        var skillPanel = FindElementByName("SkillPanel");
        var logPanel = FindElementByName("LogPanel");
        var inputPanel = FindElementByName("InputPanel");
        var bossQuoteLayer = FindElementByName("BossQuoteLayer");

        if (isLandscape)
        {
            // 横屏布局：左侧棋盘 (6)，右侧信息面板 (4)
            MainGrid.RowDefinitions.Clear();
            MainGrid.ColumnDefinitions.Clear();

            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) });
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

            if (gridView != null)
            {
                Grid.SetRow(gridView, 0);
                Grid.SetColumn(gridView, 0);
                Grid.SetRowSpan(gridView, 1);
                Grid.SetColumnSpan(gridView, 1);
            }

            if (_rightScrollView == null)
            {
                _rightPanel = new Grid { RowSpacing = 6, Padding = new Thickness(0) };
                _rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Day/Phase
                _rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 状态栏
                _rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 技能面板
                _rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 日志面板
                _rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 输入框

                _rightScrollView = new ScrollView
                {
                    Content = _rightPanel,
                    Orientation = ScrollOrientation.Vertical,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Always
                };
            }
            else
            {
                if (_rightPanel != null && _rightPanel.RowDefinitions.Count > 3)
                {
                    _rightPanel.RowDefinitions[3].Height = GridLength.Auto;
                }
            }

            if (dayPhaseBorder != null && _rightPanel != null)
            {
                if (dayPhaseBorder.Parent is Grid parentGrid) parentGrid.Children.Remove(dayPhaseBorder);
                _rightPanel.Children.Add(dayPhaseBorder);
                Grid.SetRow(dayPhaseBorder, 0);
                Grid.SetColumn(dayPhaseBorder, 0);
            }

            if (statusBorder != null && _rightPanel != null)
            {
                if (statusBorder.Parent is Grid parentGrid) parentGrid.Children.Remove(statusBorder);
                _rightPanel.Children.Add(statusBorder);
                Grid.SetRow(statusBorder, 1);
                Grid.SetColumn(statusBorder, 0);
            }

            if (skillPanel != null && _rightPanel != null)
            {
                if (skillPanel.Parent is Grid parentGrid) parentGrid.Children.Remove(skillPanel);
                _rightPanel.Children.Add(skillPanel);
                Grid.SetRow(skillPanel, 2);
                Grid.SetColumn(skillPanel, 0);
            }

            if (logPanel != null && _rightPanel != null)
            {
                if (logPanel.Parent is Grid parentGrid) parentGrid.Children.Remove(logPanel);
                logPanel.HeightRequest = 250;
                logPanel.VerticalOptions = LayoutOptions.Start;
                logPanel.MinimumHeightRequest = 250;
                _rightPanel.Children.Add(logPanel);
                Grid.SetRow(logPanel, 3);
                Grid.SetColumn(logPanel, 0);
            }

            if (inputPanel != null && _rightPanel != null)
            {
                if (inputPanel.Parent is Grid parentGrid) parentGrid.Children.Remove(inputPanel);
                _rightPanel.Children.Add(inputPanel);
                Grid.SetRow(inputPanel, 4);
                Grid.SetColumn(inputPanel, 0);
            }

            if (!MainGrid.Children.Contains(_rightScrollView))
            {
                MainGrid.Children.Add(_rightScrollView);
            }
            Grid.SetRow(_rightScrollView, 0);
            Grid.SetColumn(_rightScrollView, 1);

            // Make Boss quote overlay span across both columns in landscape
            if (bossQuoteLayer is not null)
            {
                Grid.SetRow(bossQuoteLayer, 0);
                Grid.SetRowSpan(bossQuoteLayer, 1 + 5); // rows 0..5
                Grid.SetColumn(bossQuoteLayer, 0);
                Grid.SetColumnSpan(bossQuoteLayer, 2);
            }
        }
        else
        {
            // 竖屏：恢复原始纵向布局
            MainGrid.RowDefinitions.Clear();
            MainGrid.ColumnDefinitions.Clear();

            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            MainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            if (_rightScrollView != null && MainGrid.Children.Contains(_rightScrollView))
            {
                MainGrid.Children.Remove(_rightScrollView);
            }

            if (dayPhaseBorder != null)
            {
                if (dayPhaseBorder.Parent is Grid parentGrid) parentGrid.Children.Remove(dayPhaseBorder);
                if (!MainGrid.Children.Contains(dayPhaseBorder)) MainGrid.Children.Add(dayPhaseBorder);
                Grid.SetRow(dayPhaseBorder, 0);
                Grid.SetColumn(dayPhaseBorder, 0);
                Grid.SetRowSpan(dayPhaseBorder, 1);
                Grid.SetColumnSpan(dayPhaseBorder, 1);
            }

            if (statusBorder != null)
            {
                if (statusBorder.Parent is Grid parentGrid) parentGrid.Children.Remove(statusBorder);
                if (!MainGrid.Children.Contains(statusBorder)) MainGrid.Children.Add(statusBorder);
                Grid.SetRow(statusBorder, 1);
                Grid.SetColumn(statusBorder, 0);
                Grid.SetRowSpan(statusBorder, 1);
                Grid.SetColumnSpan(statusBorder, 1);
            }

            if (gridView != null)
            {
                if (!MainGrid.Children.Contains(gridView)) MainGrid.Children.Add(gridView);
                Grid.SetRow(gridView, 2);
                Grid.SetColumn(gridView, 0);
                Grid.SetRowSpan(gridView, 1);
                Grid.SetColumnSpan(gridView, 1);
            }

            if (skillPanel != null)
            {
                if (skillPanel.Parent is Grid parentGrid) parentGrid.Children.Remove(skillPanel);
                if (!MainGrid.Children.Contains(skillPanel)) MainGrid.Children.Add(skillPanel);
                Grid.SetRow(skillPanel, 3);
                Grid.SetColumn(skillPanel, 0);
                Grid.SetRowSpan(skillPanel, 1);
                Grid.SetColumnSpan(skillPanel, 1);
            }

            if (logPanel != null)
            {
                if (logPanel.Parent is Grid parentGrid) parentGrid.Children.Remove(logPanel);
                logPanel.ClearValue(Border.HeightRequestProperty);
                logPanel.ClearValue(Border.MinimumHeightRequestProperty);
                logPanel.VerticalOptions = LayoutOptions.Fill;
                if (!MainGrid.Children.Contains(logPanel)) MainGrid.Children.Add(logPanel);
                Grid.SetRow(logPanel, 4);
                Grid.SetColumn(logPanel, 0);
                Grid.SetRowSpan(logPanel, 1);
                Grid.SetColumnSpan(logPanel, 1);
            }

            if (inputPanel != null)
            {
                if (inputPanel.Parent is Grid parentGrid) parentGrid.Children.Remove(inputPanel);
                if (!MainGrid.Children.Contains(inputPanel)) MainGrid.Children.Add(inputPanel);
                Grid.SetRow(inputPanel, 5);
                Grid.SetColumn(inputPanel, 0);
                Grid.SetRowSpan(inputPanel, 1);
                Grid.SetColumnSpan(inputPanel, 1);
            }

            // Restore overlay span to single column in portrait
            if (bossQuoteLayer is not null)
            {
                Grid.SetRow(bossQuoteLayer, 0);
                Grid.SetRowSpan(bossQuoteLayer, 1 + 5);
                Grid.SetColumn(bossQuoteLayer, 0);
                Grid.SetColumnSpan(bossQuoteLayer, 1);
            }
        }
    }

    private View? FindElementByName(string name)
    {
        var field = GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(this) as View;
    }

    private View? FindElementByRow(int row)
    {
        foreach (var child in MainGrid.Children)
        {
            if (child is not View view) continue;
            // 跳过 Boss 预警覆盖层（它可能跨行）
            if (view is Border border && Grid.GetRowSpan(border) > 1 && Grid.GetRow(border) == 0) continue;
            if (Grid.GetRow(view) == row) return view;
        }
        return null;
    }
}
