using LB_FATE.Mobile.Models;

namespace LB_FATE.Mobile.Views.Controls;

/// <summary>
/// 游戏网格绘制类 - 使用 Microsoft.Maui.Graphics 绘制 2D 网格地图
/// </summary>
public class GridBoardDrawable : IDrawable
{
    private GridData _gridData = new();
    private float _cellSize = 30f;
    private PointF _panOffset = new PointF(0, 0);

    // GitHub Light主题配色
    private readonly Color _gridLineColor = Color.FromArgb("#D0D7DE");
    private readonly Color _backgroundColor = Color.FromArgb("#FFFFFF");
    private readonly Color _allyColor = Color.FromArgb("#0969DA");        // GitHub蓝
    private readonly Color _enemyColor = Color.FromArgb("#CF222E");       // GitHub红
    private readonly Color _currentPlayerColor = Color.FromArgb("#1A7F37");  // GitHub绿
    private readonly Color _highlightColor = Color.FromArgb("#BF8700");   // GitHub金
    private readonly Color _selectedColor = Color.FromArgb("#1A7F37");    // 深绿
    private readonly Color _moveRangeColor = Color.FromArgb("#0969DA");   // 深蓝
    private readonly Color _attackRangeColor = Color.FromArgb("#D1242F");  // 亮红

    private GridUnit? _selectedUnit;
    private List<(int x, int y)> _highlightedCells = new();
    private InteractionMode _interactionMode = InteractionMode.Normal;

    public GridData GridData
    {
        get => _gridData;
        set => _gridData = value ?? new GridData();
    }

    public float CellSize
    {
        get => _cellSize;
        set => _cellSize = Math.Max(10f, Math.Min(100f, value));
    }

    public GridUnit? SelectedUnit
    {
        get => _selectedUnit;
        set => _selectedUnit = value;
    }

    public List<(int x, int y)> HighlightedCells
    {
        get => _highlightedCells;
        set => _highlightedCells = value ?? new();
    }

    public InteractionMode InteractionMode
    {
        get => _interactionMode;
        set => _interactionMode = value;
    }

    public PointF PanOffset
    {
        get => _panOffset;
        set => _panOffset = value;
    }

    /// <summary>
    /// 绘制主方法
    /// </summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 清空背景
        canvas.FillColor = _backgroundColor;
        canvas.FillRectangle(dirtyRect);

        // 计算可视区域，应用拖动偏移
        float offsetX = Math.Max(0, (dirtyRect.Width - _gridData.Width * _cellSize) / 2) + _panOffset.X;
        float offsetY = Math.Max(0, (dirtyRect.Height - _gridData.Height * _cellSize) / 2) + _panOffset.Y;

        // 绘制网格线
        DrawGridLines(canvas, offsetX, offsetY);

        // 绘制高亮格子（在单位之前）
        DrawHighlightedCells(canvas, offsetX, offsetY);

        // 绘制单位
        DrawUnits(canvas, offsetX, offsetY);

        // 绘制选中单位的指示器
        DrawSelectedIndicator(canvas, offsetX, offsetY);

        // 绘制坐标标签（可选）
        DrawCoordinateLabels(canvas, offsetX, offsetY);
    }

    /// <summary>
    /// 绘制网格线
    /// </summary>
    private void DrawGridLines(ICanvas canvas, float offsetX, float offsetY)
    {
        canvas.StrokeColor = _gridLineColor;
        canvas.StrokeSize = 1f;

        // 绘制垂直线
        for (int x = 0; x <= _gridData.Width; x++)
        {
            float xPos = offsetX + x * _cellSize;
            canvas.DrawLine(xPos, offsetY, xPos, offsetY + _gridData.Height * _cellSize);
        }

        // 绘制水平线
        for (int y = 0; y <= _gridData.Height; y++)
        {
            float yPos = offsetY + y * _cellSize;
            canvas.DrawLine(offsetX, yPos, offsetX + _gridData.Width * _cellSize, yPos);
        }
    }

    /// <summary>
    /// 绘制单位
    /// </summary>
    private void DrawUnits(ICanvas canvas, float offsetX, float offsetY)
    {
        foreach (var unit in _gridData.Units)
        {
            DrawUnit(canvas, unit, offsetX, offsetY);
        }
    }

    /// <summary>
    /// 绘制单个单位
    /// </summary>
    private void DrawUnit(ICanvas canvas, GridUnit unit, float offsetX, float offsetY)
    {
        float x = offsetX + unit.X * _cellSize;
        float y = offsetY + unit.Y * _cellSize;
        float centerX = x + _cellSize / 2;
        float centerY = y + _cellSize / 2;
        float radius = _cellSize * 0.35f;

        // 选择颜色
        Color unitColor = unit.IsCurrentPlayer ? _currentPlayerColor :
                         unit.IsAlly ? _allyColor : _enemyColor;

        // 绘制单位圆形
        canvas.FillColor = unitColor;
        canvas.FillCircle(centerX, centerY, radius);

        // 绘制边框 - 当前玩家使用金色边框
        canvas.StrokeColor = unit.IsCurrentPlayer ? Color.FromArgb("#FFD700") : Colors.White;
        canvas.StrokeSize = unit.IsCurrentPlayer ? 3f : 2f;
        canvas.DrawCircle(centerX, centerY, radius);

        // 绘制 HP 条（只为当前玩家和Boss显示）
        if (unit.IsCurrentPlayer || unit.Symbol == "B")
        {
            DrawHealthBar(canvas, unit, x, y);
        }

        // 当前玩家：在单位上方绘制星形标记
        if (unit.IsCurrentPlayer)
        {
            DrawPlayerStar(canvas, centerX, y + 2);
        }

        // 绘制单位 ID/名称
        DrawUnitLabel(canvas, unit, centerX, centerY);

        // 绘制离线标记
        if (unit.IsOffline)
        {
            DrawOfflineMarker(canvas, centerX, centerY, radius);
        }
    }

    /// <summary>
    /// 绘制当前玩家星形标记
    /// </summary>
    private void DrawPlayerStar(ICanvas canvas, float centerX, float topY)
    {
        float starSize = _cellSize * 0.15f;
        var font = Microsoft.Maui.Graphics.Font.Default;

        canvas.FontColor = Color.FromArgb("#FFD700");
        canvas.FontSize = starSize;
        canvas.Font = font;

        var textSize = canvas.GetStringSize("★", font, starSize);
        canvas.DrawString("★", centerX - textSize.Width / 2, topY, HorizontalAlignment.Left);
    }

    /// <summary>
    /// 绘制生命值条
    /// </summary>
    private void DrawHealthBar(ICanvas canvas, GridUnit unit, float x, float y)
    {
        if (unit.MaxHP <= 0) return;

        float barWidth = _cellSize * 0.8f;
        float barHeight = 4f;
        float barX = x + (_cellSize - barWidth) / 2;
        float barY = y + _cellSize - barHeight - 2;

        // 背景（浅灰色）
        canvas.FillColor = Color.FromArgb("#D0D7DE");
        canvas.FillRectangle(barX, barY, barWidth, barHeight);

        // HP 条（根据血量变色）
        float hpPercent = (float)unit.HP / unit.MaxHP;
        float hpWidth = barWidth * hpPercent;

        Color hpColor = hpPercent > 0.6f ? Color.FromArgb("#1A7F37") :
                       hpPercent > 0.3f ? Color.FromArgb("#BF8700") : Color.FromArgb("#CF222E");

        canvas.FillColor = hpColor;
        canvas.FillRectangle(barX, barY, hpWidth, barHeight);
    }

    /// <summary>
    /// 绘制单位标签
    /// </summary>
    private void DrawUnitLabel(ICanvas canvas, GridUnit unit, float centerX, float centerY)
    {
        // 优先使用Symbol（网格符号：1-9, B, !等），否则使用Name首字符
        string label = !string.IsNullOrEmpty(unit.Symbol) ? unit.Symbol :
                       (!string.IsNullOrEmpty(unit.Name) ? unit.Name.Substring(0, Math.Min(1, unit.Name.Length)) : unit.Id);

        // 更小的字体尺寸 - 0.25倍cellSize
        float fontSize = _cellSize * 0.25f;
        var font = Microsoft.Maui.Graphics.Font.DefaultBold;

        canvas.FontColor = Colors.White;
        canvas.FontSize = fontSize;
        canvas.Font = font;

        // 使用 DrawString 的内置对齐功能，确保文本在圆心正中
        canvas.DrawString(label, centerX, centerY, HorizontalAlignment.Center);
    }

    /// <summary>
    /// 绘制离线标记
    /// </summary>
    private void DrawOfflineMarker(ICanvas canvas, float centerX, float centerY, float radius)
    {
        // 绘制红色感叹号
        var font = Microsoft.Maui.Graphics.Font.DefaultBold;
        float fontSize = radius * 1.5f;

        canvas.FontColor = Color.FromArgb("#CF222E");
        canvas.FontSize = fontSize;
        canvas.Font = font;

        var textSize = canvas.GetStringSize("!", font, fontSize);
        canvas.DrawString("!", centerX - textSize.Width / 2, centerY - textSize.Height / 2, HorizontalAlignment.Left);

        // 绘制半透明红色圆环
        canvas.StrokeColor = Color.FromArgb("#CF222E").WithAlpha(0.7f);
        canvas.StrokeSize = 3f;
        canvas.DrawCircle(centerX, centerY, radius * 1.1f);
    }

    /// <summary>
    /// 绘制坐标标签
    /// </summary>
    private void DrawCoordinateLabels(ICanvas canvas, float offsetX, float offsetY)
    {
        var font = Microsoft.Maui.Graphics.Font.Default;
        float fontSize = Math.Max(8f, _cellSize * 0.3f);

        canvas.FontColor = Color.FromArgb("#656D76");
        canvas.FontSize = fontSize;
        canvas.Font = font;

        // X 轴坐标（顶部）
        for (int x = 0; x < _gridData.Width; x++)
        {
            string label = (x % 10).ToString();
            float xPos = offsetX + x * _cellSize + _cellSize / 2;
            var textSize = canvas.GetStringSize(label, font, fontSize);
            canvas.DrawString(label, xPos - textSize.Width / 2, offsetY - 15, HorizontalAlignment.Left);
        }

        // Y 轴坐标（右侧）
        float gridRightEdge = offsetX + _gridData.Width * _cellSize;
        for (int y = 0; y < _gridData.Height; y++)
        {
            string label = y.ToString();
            float yPos = offsetY + y * _cellSize + _cellSize / 2;
            var textSize = canvas.GetStringSize(label, font, fontSize);
            canvas.DrawString(label, gridRightEdge + 5, yPos - textSize.Height / 2, HorizontalAlignment.Left);
        }
    }

    /// <summary>
    /// 绘制高亮格子
    /// </summary>
    private void DrawHighlightedCells(ICanvas canvas, float offsetX, float offsetY)
    {
        if (_highlightedCells == null || _highlightedCells.Count == 0)
            return;

        // 根据交互模式选择颜色
        Color highlightColor = _interactionMode switch
        {
            InteractionMode.Move => _moveRangeColor,
            InteractionMode.Attack => _attackRangeColor,
            InteractionMode.CastSkill => _highlightColor,
            _ => _highlightColor
        };

        foreach (var (x, y) in _highlightedCells)
        {
            if (x < 0 || x >= _gridData.Width || y < 0 || y >= _gridData.Height)
                continue;

            float cellX = offsetX + x * _cellSize;
            float cellY = offsetY + y * _cellSize;

            // 绘制半透明填充
            canvas.FillColor = highlightColor.WithAlpha(0.3f);
            canvas.FillRectangle(cellX, cellY, _cellSize, _cellSize);

            // 绘制边框
            canvas.StrokeColor = highlightColor;
            canvas.StrokeSize = 2f;
            canvas.DrawRectangle(cellX, cellY, _cellSize, _cellSize);
        }
    }

    /// <summary>
    /// 绘制选中单位的指示器
    /// </summary>
    private void DrawSelectedIndicator(ICanvas canvas, float offsetX, float offsetY)
    {
        if (_selectedUnit == null)
            return;

        float x = offsetX + _selectedUnit.X * _cellSize;
        float y = offsetY + _selectedUnit.Y * _cellSize;
        float centerX = x + _cellSize / 2;
        float centerY = y + _cellSize / 2;
        float radius = _cellSize * 0.45f;

        // 绘制脉动圆环
        canvas.StrokeColor = _selectedColor;
        canvas.StrokeSize = 3f;
        canvas.DrawCircle(centerX, centerY, radius);

        // 绘制四个角的指示器
        float cornerSize = _cellSize * 0.15f;
        canvas.StrokeColor = _selectedColor;
        canvas.StrokeSize = 2f;

        // 左上角
        canvas.DrawLine(x, y, x + cornerSize, y);
        canvas.DrawLine(x, y, x, y + cornerSize);

        // 右上角
        canvas.DrawLine(x + _cellSize, y, x + _cellSize - cornerSize, y);
        canvas.DrawLine(x + _cellSize, y, x + _cellSize, y + cornerSize);

        // 左下角
        canvas.DrawLine(x, y + _cellSize, x + cornerSize, y + _cellSize);
        canvas.DrawLine(x, y + _cellSize, x, y + _cellSize - cornerSize);

        // 右下角
        canvas.DrawLine(x + _cellSize, y + _cellSize, x + _cellSize - cornerSize, y + _cellSize);
        canvas.DrawLine(x + _cellSize, y + _cellSize, x + _cellSize, y + _cellSize - cornerSize);
    }

    /// <summary>
    /// 将屏幕坐标转换为网格坐标
    /// </summary>
    public (int x, int y) ScreenToGrid(PointF screenPoint, RectF canvasRect)
    {
        float offsetX = Math.Max(0, (canvasRect.Width - _gridData.Width * _cellSize) / 2) + _panOffset.X;
        float offsetY = Math.Max(0, (canvasRect.Height - _gridData.Height * _cellSize) / 2) + _panOffset.Y;

        int gridX = (int)((screenPoint.X - offsetX) / _cellSize);
        int gridY = (int)((screenPoint.Y - offsetY) / _cellSize);

        return (gridX, gridY);
    }
}
