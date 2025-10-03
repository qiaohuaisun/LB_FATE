using LB_FATE.Mobile.Models;

namespace LB_FATE.Mobile.Views.Controls;

/// <summary>
/// 游戏网格视图控件 - 封装 GraphicsView 并提供可绑定属性
/// </summary>
public class GridBoardView : GraphicsView
{
    private readonly GridBoardDrawable _drawable;

    // 绑定属性：网格数据
    public static readonly BindableProperty GridDataProperty =
        BindableProperty.Create(
            nameof(GridData),
            typeof(GridData),
            typeof(GridBoardView),
            new GridData(),
            propertyChanged: OnGridDataChanged);

    // 绑定属性：单元格大小
    public static readonly BindableProperty CellSizeProperty =
        BindableProperty.Create(
            nameof(CellSize),
            typeof(float),
            typeof(GridBoardView),
            30f,
            propertyChanged: OnCellSizeChanged);

    // 绑定属性：交互状态
    public static readonly BindableProperty InteractionStateProperty =
        BindableProperty.Create(
            nameof(InteractionState),
            typeof(InteractionState),
            typeof(GridBoardView),
            new InteractionState(),
            propertyChanged: OnInteractionStateChanged);

    public GridData GridData
    {
        get => (GridData)GetValue(GridDataProperty);
        set => SetValue(GridDataProperty, value);
    }

    public float CellSize
    {
        get => (float)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public InteractionState InteractionState
    {
        get => (InteractionState)GetValue(InteractionStateProperty);
        set => SetValue(InteractionStateProperty, value);
    }


    // 渲染节流相关
    private bool _isInvalidating = false;
    private readonly object _invalidateLock = new object();

    // 拖动和缩放相关
    private Point _panOffset = new Point(0, 0);
    private Point _lastPanPoint = new Point(0, 0);
    private bool _isPanning = false;
    private bool _isPinching = false;  // 正在缩放标志
    private float _pinchStartCellSize = 30f;

    public GridBoardView()
    {
        _drawable = new GridBoardDrawable();
        Drawable = _drawable;

        // 添加双指缩放手势 - 需要至少2个触摸点
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        this.GestureRecognizers.Add(pinchGesture);

        // 添加拖动手势 - 设置为单指拖动
        var panGesture = new PanGestureRecognizer
        {
            TouchPoints = 1  // 只响应单指拖动
        };
        panGesture.PanUpdated += OnPanUpdated;
        this.GestureRecognizers.Add(panGesture);
    }

    /// <summary>
    /// 处理双指缩放
    /// </summary>
    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _isPinching = true;
                _isPanning = false;
                _pinchStartCellSize = CellSize;
                break;

            case GestureStatus.Running:
                if (!_isPinching) break;

                var newSize = _pinchStartCellSize * (float)e.Scale;
                var clampedSize = Math.Clamp(newSize, 20f, 80f);


                CellSize = clampedSize;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _pinchStartCellSize = CellSize;
                _isPinching = false;
                break;
        }
    }

    /// <summary>
    /// 处理拖动手势
    /// </summary>
    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_isPinching)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isPanning = true;
                _lastPanPoint = new Point(e.TotalX, e.TotalY);
                break;

            case GestureStatus.Running:
                if (_isPanning)
                {
                    var deltaX = e.TotalX - _lastPanPoint.X;
                    var deltaY = e.TotalY - _lastPanPoint.Y;

                    _panOffset = new Point(
                        _panOffset.X + deltaX,
                        _panOffset.Y + deltaY
                    );
                    _lastPanPoint = new Point(e.TotalX, e.TotalY);

                    _drawable.PanOffset = new Microsoft.Maui.Graphics.PointF((float)_panOffset.X, (float)_panOffset.Y);
                    Invalidate();
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isPanning = false;
                break;
        }
    }


    private static void OnGridDataChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GridBoardView view && newValue is GridData gridData)
        {
            view._drawable.GridData = gridData;
            view.ThrottledInvalidate();
        }
    }

    private static void OnCellSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GridBoardView view && newValue is float cellSize)
        {
            view._drawable.CellSize = cellSize;
            view.ThrottledInvalidate();
        }
    }

    private static void OnInteractionStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GridBoardView view && newValue is InteractionState state)
        {
            view._drawable.SelectedUnit = state.SelectedUnit;
            view._drawable.HighlightedCells = state.HighlightedCells;
            view._drawable.InteractionMode = state.Mode;
            view.ThrottledInvalidate();
        }
    }

    /// <summary>
    /// 节流渲染 - 防止频繁刷新
    /// </summary>
    private void ThrottledInvalidate()
    {
        lock (_invalidateLock)
        {
            if (_isInvalidating)
                return;

            _isInvalidating = true;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                Invalidate();
                // Android优化: 33ms延迟 ≈ 30fps，减少渲染压力
                await Task.Delay(DeviceInfo.Platform == DevicePlatform.Android ? 33 : 16);
            }
            finally
            {
                lock (_invalidateLock)
                {
                    _isInvalidating = false;
                }
            }
        });
    }

    /// <summary>
    /// 更新网格数据并刷新显示
    /// </summary>
    public void UpdateGrid(GridData gridData)
    {
        GridData = gridData;
    }

    /// <summary>
    /// 添加或更新单位
    /// </summary>
    public void AddOrUpdateUnit(GridUnit unit)
    {
        GridData.AddOrUpdateUnit(unit);
        ThrottledInvalidate();
    }

    /// <summary>
    /// 移除单位
    /// </summary>
    public void RemoveUnit(string unitId)
    {
        GridData.RemoveUnit(unitId);
        ThrottledInvalidate();
    }

    /// <summary>
    /// 清空网格
    /// </summary>
    public void ClearGrid()
    {
        GridData.Clear();
        ThrottledInvalidate();
    }

    /// <summary>
    /// 缩放视图
    /// </summary>
    public void ZoomIn()
    {
        CellSize = Math.Min(100f, CellSize + 5f);
    }

    public void ZoomOut()
    {
        CellSize = Math.Max(10f, CellSize - 5f);
    }
}

/// <summary>
/// 网格单元格点击事件参数
/// </summary>
public class GridCellTappedEventArgs : EventArgs
{
    public int GridX { get; }
    public int GridY { get; }
    public GridUnit? Unit { get; }

    public GridCellTappedEventArgs(int gridX, int gridY, GridUnit? unit)
    {
        GridX = gridX;
        GridY = gridY;
        Unit = unit;
    }
}
