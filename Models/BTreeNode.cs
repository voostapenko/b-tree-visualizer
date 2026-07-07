using System.Collections.Generic;

namespace Лаб3ПА.Models;

public class BTreeNode
{
    private static int _nextId = 1;

    public int Id { get; set; }
    public List<Record> Records { get; set; } = new();
    public List<BTreeNode> Children { get; set; } = new();
    public bool IsLeaf { get; set; }

    public BTreeNode(bool isLeaf)
    {
        Id = _nextId++;
        IsLeaf = isLeaf;
    }

    public BTreeNode()
    {
        Id = _nextId++;
    }

    public static void ResetIdsFromTree(BTreeNode? root)
    {
        int maxId = 0;
        Traverse(root, node => maxId = System.Math.Max(maxId, node.Id));
        _nextId = System.Math.Max(maxId + 1, 1);
    }

    private static void Traverse(BTreeNode? node, System.Action<BTreeNode> visitor)
    {
        if (node is null)
        {
            return;
        }

        visitor(node);
        foreach (var child in node.Children)
        {
            Traverse(child, visitor);
        }
    }
}
