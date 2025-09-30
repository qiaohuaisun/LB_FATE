using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using LB_FATE.AvaloniaClient.ViewModels;
using ETBBS;

namespace LB_FATE.AvaloniaClient.Controls;

public partial class GameBoardControl : UserControl
{
    private const int CellSize = 50;
    private const int GridPadding = 10;

    private GameViewModel? _viewModel;

    public GameBoardControl()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        GameCanvas.PointerPressed += OnCanvasPointerPressed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Units.CollectionChanged -= OnUnitsChanged;
        }

        _viewModel = DataContext as GameViewModel;

        if (_viewModel != null)
        {
            _viewModel.Units.CollectionChanged += OnUnitsChanged;
            RedrawBoard();
        }
    }

    private void OnUnitsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawBoard();
    }

    private void RedrawBoard()
    {
        if (_viewModel == null) return;

        GameCanvas.Children.Clear();

        var width = _viewModel.GridWidth;
        var height = _viewModel.GridHeight;

        // Draw grid
        DrawGrid(width, height);

        // Draw units
        DrawUnits();
    }

    private void DrawGrid(int width, int height)
    {
        var totalWidth = width * CellSize;
        var totalHeight = height * CellSize;

        GameCanvas.Width = totalWidth + GridPadding * 2;
        GameCanvas.Height = totalHeight + GridPadding * 2;

        var gridBrush = new SolidColorBrush(Color.Parse("#444444"));
        var pen = new Pen(gridBrush, 1);

        // Draw vertical lines
        for (int x = 0; x <= width; x++)
        {
            var line = new Line
            {
                StartPoint = new Point(GridPadding + x * CellSize, GridPadding),
                EndPoint = new Point(GridPadding + x * CellSize, GridPadding + totalHeight),
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            GameCanvas.Children.Add(line);
        }

        // Draw horizontal lines
        for (int y = 0; y <= height; y++)
        {
            var line = new Line
            {
                StartPoint = new Point(GridPadding, GridPadding + y * CellSize),
                EndPoint = new Point(GridPadding + totalWidth, GridPadding + y * CellSize),
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            GameCanvas.Children.Add(line);
        }

        // Draw coordinate labels
        for (int x = 0; x < width; x++)
        {
            var label = new TextBlock
            {
                Text = x.ToString(),
                Foreground = Brushes.Gray,
                FontSize = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            Canvas.SetLeft(label, GridPadding + x * CellSize + CellSize / 2 - 5);
            Canvas.SetTop(label, 0);
            GameCanvas.Children.Add(label);
        }

        for (int y = 0; y < height; y++)
        {
            var label = new TextBlock
            {
                Text = y.ToString(),
                Foreground = Brushes.Gray,
                FontSize = 10,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, GridPadding + y * CellSize + CellSize / 2 - 7);
            GameCanvas.Children.Add(label);
        }
    }

    private void DrawUnits()
    {
        if (_viewModel == null) return;

        foreach (var unit in _viewModel.Units)
        {
            DrawUnit(unit);
        }
    }

    private void DrawUnit(UnitViewModel unit)
    {
        var x = unit.Position.X;
        var y = unit.Position.Y;

        var cellX = GridPadding + x * CellSize;
        var cellY = GridPadding + y * CellSize;

        // Draw unit background
        var background = new Border
        {
            Width = CellSize - 4,
            Height = CellSize - 4,
            Background = GetClassColor(unit.ClassName),
            CornerRadius = new CornerRadius(5),
            Opacity = unit.IsOffline ? 0.3 : 0.8
        };
        Canvas.SetLeft(background, cellX + 2);
        Canvas.SetTop(background, cellY + 2);
        GameCanvas.Children.Add(background);

        // Draw symbol
        var symbolText = new TextBlock
        {
            Text = unit.Symbol.ToString(),
            Foreground = Brushes.White,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Canvas.SetLeft(symbolText, cellX + CellSize / 2 - 8);
        Canvas.SetTop(symbolText, cellY + 5);
        GameCanvas.Children.Add(symbolText);

        // Draw HP bar
        var hpBarBackground = new Border
        {
            Width = CellSize - 8,
            Height = 4,
            Background = Brushes.DarkRed,
            CornerRadius = new CornerRadius(2)
        };
        Canvas.SetLeft(hpBarBackground, cellX + 4);
        Canvas.SetTop(hpBarBackground, cellY + CellSize - 8);
        GameCanvas.Children.Add(hpBarBackground);

        var hpBarFill = new Border
        {
            Width = (CellSize - 8) * unit.HpPercentage / 100.0,
            Height = 4,
            Background = Brushes.LimeGreen,
            CornerRadius = new CornerRadius(2)
        };
        Canvas.SetLeft(hpBarFill, cellX + 4);
        Canvas.SetTop(hpBarFill, cellY + CellSize - 8);
        GameCanvas.Children.Add(hpBarFill);

        // Make unit clickable
        background.PointerPressed += (s, e) => OnUnitClicked(unit, e);
        background.Cursor = new Cursor(StandardCursorType.Hand);
    }

    private IBrush GetClassColor(string className)
    {
        return className switch
        {
            "Saber" => new SolidColorBrush(Color.Parse("#00CED1")),
            "Archer" => new SolidColorBrush(Color.Parse("#32CD32")),
            "Lancer" => new SolidColorBrush(Color.Parse("#4169E1")),
            "Rider" => new SolidColorBrush(Color.Parse("#FFD700")),
            "Caster" => new SolidColorBrush(Color.Parse("#FF00FF")),
            "Assassin" => new SolidColorBrush(Color.Parse("#008B8B")),
            "Berserker" => new SolidColorBrush(Color.Parse("#DC143C")),
            _ => new SolidColorBrush(Color.Parse("#808080"))
        };
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null) return;

        var point = e.GetPosition(GameCanvas);
        var gridX = (int)((point.X - GridPadding) / CellSize);
        var gridY = (int)((point.Y - GridPadding) / CellSize);

        if (gridX >= 0 && gridX < _viewModel.GridWidth &&
            gridY >= 0 && gridY < _viewModel.GridHeight)
        {
            var coord = new Coord(gridX, gridY);
            OnCellClicked(coord);
        }
    }

    private void OnUnitClicked(UnitViewModel unit, PointerPressedEventArgs e)
    {
        if (_viewModel == null) return;

        // Right click to attack, left click to select
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            _viewModel.AttackCommand.Execute(unit.Id);
        }

        e.Handled = true;
    }

    private void OnCellClicked(Coord coord)
    {
        if (_viewModel == null || !_viewModel.IsMyTurn) return;

        // Left click empty cell to move
        _viewModel.MoveCommand.Execute(coord);
    }
}