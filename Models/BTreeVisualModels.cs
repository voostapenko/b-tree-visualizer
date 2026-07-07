using System.Collections.Generic;
using System.Linq;

namespace Лаб3ПА.Models;

public enum VisualState
{
    Normal,
    Active,
    Visited,
    Found,
    Inserted,
    Deleted,
    Edited,
    Promoted,
    Borrowed,
    Merged,
    Warning,
    Missing
}

public sealed class VisualRecordSnapshot
{
    public int Key { get; init; }
    public string Data { get; init; } = string.Empty;
    public VisualState State { get; init; }
}

public sealed class VisualNodeSnapshot
{
    public int Id { get; init; }
    public VisualState State { get; init; }
    public bool IsLeaf { get; init; }
    public IReadOnlyList<VisualRecordSnapshot> Records { get; init; } = new List<VisualRecordSnapshot>();
    public IReadOnlyList<VisualNodeSnapshot> Children { get; init; } = new List<VisualNodeSnapshot>();
}

public sealed class BTreeSnapshot
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public VisualNodeSnapshot? Root { get; init; }
    public string? PlaceholderText { get; init; }

    public int TotalRecordCount => CountRecords(Root);

    public static BTreeSnapshot Placeholder(string title, string description, string placeholderText) =>
        new()
        {
            Title = title,
            Description = description,
            PlaceholderText = placeholderText,
            Root = null
        };

    private static int CountRecords(VisualNodeSnapshot? node)
    {
        if (node is null)
        {
            return 0;
        }

        return node.Records.Count + node.Children.Sum(CountRecords);
    }
}

public sealed class OperationStep
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public BTreeSnapshot Snapshot { get; init; } = new();
}

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Value { get; init; }
    public long Comparisons { get; init; }
    public IReadOnlyList<OperationStep> Steps { get; init; } = new List<OperationStep>();
}

public readonly record struct SearchResult(BTreeNode? Node, int Index, string? Value);
