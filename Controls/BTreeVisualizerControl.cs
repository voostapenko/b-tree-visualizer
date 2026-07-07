using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Лаб3ПА.Models;

namespace Лаб3ПА.Controls;

public class BTreeVisualizerControl : UserControl
{
    private const double BaseVerticalSpacing = 50;
    private const double BaseHorizontalSpacing = 110;

    public static readonly StyledProperty<BTreeSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<BTreeVisualizerControl, BTreeSnapshot?>(nameof(Snapshot));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<BTreeVisualizerControl, double>(nameof(Zoom), 1.0);

    private readonly Canvas _canvas;

    public BTreeVisualizerControl()
    {
        _canvas = new Canvas();
        Content = _canvas;

        SnapshotProperty.Changed.AddClassHandler<BTreeVisualizerControl>((control, _) => control.Rebuild());
        ZoomProperty.Changed.AddClassHandler<BTreeVisualizerControl>((control, _) => control.Rebuild());
    }

    public BTreeSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private void Rebuild()
    {
        _canvas.Children.Clear();

        var snapshot = Snapshot;
        if (snapshot is null)
        {
            ShowPlaceholder("Дерево ще не побудовано.");
            return;
        }

        if (snapshot.Root is null)
        {
            ShowPlaceholder(snapshot.PlaceholderText ?? snapshot.Description);
            return;
        }

        var layout = BuildLayout(
            snapshot.Root,
            BaseVerticalSpacing * Zoom,
            BaseHorizontalSpacing * Zoom);
        PositionNodes(layout, 24 * Zoom, 24 * Zoom);
        RenderLines(layout);
        RenderNodes(layout);

        _canvas.Width = Math.Max(layout.SubtreeWidth + 48 * Zoom, 600 * Zoom);
        _canvas.Height = Math.Max(GetTreeBottom(layout) + 60 * Zoom, 300 * Zoom);
    }

    private void ShowPlaceholder(string text)
    {
        _canvas.Width = 900;
        _canvas.Height = 320;
        _canvas.Children.Add(new Border
        {
            Width = 860,
            Padding = new Thickness(20),
            Background = BrushFromHex("#102033"),
            BorderBrush = BrushFromHex("#406080"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16,
                Foreground = Brushes.White
            }
        });

        if (_canvas.Children[0] is Control control)
        {
            Canvas.SetLeft(control, 20);
            Canvas.SetTop(control, 20);
        }
    }

    private LayoutNode BuildLayout(VisualNodeSnapshot node, double verticalSpacing, double horizontalSpacing)
    {
        double cellWidth = 62 * Zoom;
        double cellHeight = 44 * Zoom;
        double nodePadding = 10 * Zoom;
        double nodeWidth = Math.Max((node.Records.Count * cellWidth) + nodePadding * 2, 80 * Zoom);
        double nodeHeight = cellHeight + nodePadding * 2;

        var children = node.Children.Select(child => BuildLayout(child, verticalSpacing, horizontalSpacing)).ToList();
        double childrenWidth = children.Count == 0
            ? 0
            : children.Sum(child => child.SubtreeWidth) + horizontalSpacing * (children.Count - 1);

        return new LayoutNode
        {
            Snapshot = node,
            NodeWidth = nodeWidth,
            NodeHeight = nodeHeight,
            SubtreeWidth = Math.Max(nodeWidth, childrenWidth),
            VerticalSpacing = verticalSpacing,
            HorizontalSpacing = horizontalSpacing,
            Children = children
        };
    }

    private void PositionNodes(LayoutNode node, double left, double top)
    {
        node.SubtreeLeft = left;
        node.X = left + (node.SubtreeWidth - node.NodeWidth) / 2;
        node.Y = top;

        double childLeft = left;
        foreach (var child in node.Children)
        {
            PositionNodes(child, childLeft, top + node.NodeHeight + node.VerticalSpacing);
            childLeft += child.SubtreeWidth + node.HorizontalSpacing;
        }
    }

    private void RenderLines(LayoutNode node)
    {
        foreach (var child in node.Children)
        {
            var line = new Line
            {
                StartPoint = new Point(node.X + node.NodeWidth / 2, node.Y + node.NodeHeight),
                EndPoint = new Point(child.X + child.NodeWidth / 2, child.Y),
                Stroke = BrushFromHex("#6d7b8a"),
                StrokeThickness = Math.Max(1, 1.5 * Zoom)
            };

            _canvas.Children.Add(line);
            RenderLines(child);
        }
    }

    private void RenderNodes(LayoutNode node)
    {
        var nodeBorder = new Border
        {
            Background = NodeBrush(node.Snapshot.State),
            BorderBrush = NodeBorderBrush(node.Snapshot.State),
            BorderThickness = new Thickness(Math.Max(1, 1.5 * Zoom)),
            CornerRadius = new CornerRadius(12 * Zoom),
            Padding = new Thickness(8 * Zoom),
            Child = BuildRecordPanel(node.Snapshot)
        };

        ToolTip.SetTip(nodeBorder, node.Snapshot.IsLeaf ? "Листовий вузол" : "Внутрішній вузол");
        Canvas.SetLeft(nodeBorder, node.X);
        Canvas.SetTop(nodeBorder, node.Y);
        _canvas.Children.Add(nodeBorder);

        foreach (var child in node.Children)
        {
            RenderNodes(child);
        }
    }

    private Control BuildRecordPanel(VisualNodeSnapshot node)
    {
        var root = new StackPanel
        {
            Spacing = 6 * Zoom
        };

        root.Children.Add(new TextBlock
        {
            Text = node.IsLeaf ? "Leaf" : "Node",
            FontSize = 11 * Zoom,
            FontWeight = FontWeight.SemiBold,
            Foreground = BrushFromHex("#dce8f5"),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4 * Zoom,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        foreach (var record in node.Records)
        {
            var cell = new Border
            {
                Width = 62 * Zoom,
                Height = 44 * Zoom,
                Background = RecordBrush(record.State),
                BorderBrush = RecordBorderBrush(record.State),
                BorderThickness = new Thickness(Math.Max(1, 1.2 * Zoom)),
                CornerRadius = new CornerRadius(8 * Zoom),
                Child = new TextBlock
                {
                    Text = record.Key.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 14 * Zoom,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                }
            };

            ToolTip.SetTip(cell, $"Ключ: {record.Key}\nДані: {record.Data}");
            row.Children.Add(cell);
        }

        root.Children.Add(row);
        return root;
    }

    private double GetTreeBottom(LayoutNode node)
    {
        double currentBottom = node.Y + node.NodeHeight;
        return node.Children.Count == 0 ? currentBottom : Math.Max(currentBottom, node.Children.Max(GetTreeBottom));
    }

    private static IBrush BrushFromHex(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static IBrush RecordBrush(VisualState state) => state switch
    {
        VisualState.Active => BrushFromHex("#f59e0b"),
        VisualState.Visited => BrushFromHex("#2563eb"),
        VisualState.Found => BrushFromHex("#16a34a"),
        VisualState.Inserted => BrushFromHex("#22c55e"),
        VisualState.Deleted => BrushFromHex("#ef4444"),
        VisualState.Edited => BrushFromHex("#14b8a6"),
        VisualState.Promoted => BrushFromHex("#8b5cf6"),
        VisualState.Borrowed => BrushFromHex("#0ea5e9"),
        VisualState.Merged => BrushFromHex("#7c3aed"),
        VisualState.Warning => BrushFromHex("#dc2626"),
        VisualState.Missing => BrushFromHex("#6b7280"),
        _ => BrushFromHex("#334155")
    };

    private static IBrush RecordBorderBrush(VisualState state) => state switch
    {
        VisualState.Normal => BrushFromHex("#64748b"),
        _ => Brushes.White
    };

    private static IBrush NodeBrush(VisualState state) => state switch
    {
        VisualState.Active => BrushFromHex("#1f2937"),
        VisualState.Visited => BrushFromHex("#0f172a"),
        VisualState.Warning => BrushFromHex("#2b1220"),
        VisualState.Merged => BrushFromHex("#24133a"),
        VisualState.Borrowed => BrushFromHex("#082f49"),
        _ => BrushFromHex("#111827")
    };

    private static IBrush NodeBorderBrush(VisualState state) => state switch
    {
        VisualState.Active => BrushFromHex("#f59e0b"),
        VisualState.Visited => BrushFromHex("#60a5fa"),
        VisualState.Warning => BrushFromHex("#f87171"),
        VisualState.Merged => BrushFromHex("#a78bfa"),
        VisualState.Borrowed => BrushFromHex("#38bdf8"),
        _ => BrushFromHex("#475569")
    };

    private sealed class LayoutNode
    {
        public VisualNodeSnapshot Snapshot { get; init; } = null!;
        public double NodeWidth { get; init; }
        public double NodeHeight { get; init; }
        public double SubtreeWidth { get; init; }
        public double VerticalSpacing { get; init; }
        public double HorizontalSpacing { get; init; }
        public List<LayoutNode> Children { get; init; } = new();
        public double SubtreeLeft { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
