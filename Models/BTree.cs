using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Лаб3ПА.Models;

public class BTree
{
    private BTreeNode _root;
    private readonly int _t;
    private readonly string _dbFilePath;

    public long LastSearchComparisons { get; private set; }
    public int Degree => _t;

    public BTree(int t)
    {
        if (t < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(t), "Мінімальний степінь B-дерева має бути не менше 2.");
        }

        _t = t;
        _root = new BTreeNode(true);
        _dbFilePath = Path.Combine(AppContext.BaseDirectory, "database.json");
    }

    public BTreeNode GetRoot() => _root;

    public int CountRecords() => CountRecords(_root);

    public int GetHeight() => GetHeight(_root);

    public SearchResult Search(int key) => Search(key, null);

    public OperationResult ExecuteSearch(int key)
    {
        var steps = new List<OperationStep>();
        var result = Search(key, steps);

        return new OperationResult
        {
            Success = result.Node is not null,
            Message = result.Node is not null
                ? $"Ключ {key} знайдено. Дані: {result.Value}. Кількість порівнянь: {LastSearchComparisons}."
                : $"Ключ {key} не знайдено. Кількість порівнянь: {LastSearchComparisons}.",
            Value = result.Value,
            Comparisons = LastSearchComparisons,
            Steps = steps
        };
    }

    public OperationResult ExecuteInsert(int key, string data)
    {
        var steps = new List<OperationStep>();
        var existing = Search(key);
        if (existing.Node is not null)
        {
            AddStep(
                steps,
                "Додавання",
                $"Ключ {key} вже існує, тому дубль не додається.",
                recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Warning });

            return new OperationResult
            {
                Success = false,
                Message = $"Ключ {key} вже існує.",
                Steps = steps
            };
        }

        AddStep(
            steps,
            "Додавання",
            $"Починаємо вставку ключа {key}.",
            recordStates: new Dictionary<int, VisualState>(),
            rootState: VisualState.Active);

        InsertInternal(key, data, persist: true, steps);

        AddStep(
            steps,
            "Додавання",
            $"Ключ {key} успішно додано до B-дерева.",
            recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Inserted });

        return new OperationResult
        {
            Success = true,
            Message = $"Ключ {key} успішно додано.",
            Steps = steps
        };
    }

    public OperationResult ExecuteEdit(int key, string newData)
    {
        var steps = new List<OperationStep>();
        var result = Search(key, steps);
        if (result.Node is null)
        {
            AddStep(
                steps,
                "Редагування",
                $"Ключ {key} не знайдено, тому редагування неможливе.",
                rootState: VisualState.Warning);

            return new OperationResult
            {
                Success = false,
                Message = $"Ключ {key} не знайдено.",
                Steps = steps
            };
        }

        result.Node.Records[result.Index].Data = newData;
        SaveData();

        AddStep(
            steps,
            "Редагування",
            $"Дані для ключа {key} змінено на “{newData}”.",
            recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Edited });

        return new OperationResult
        {
            Success = true,
            Message = $"Запис з ключем {key} відредаговано.",
            Value = newData,
            Steps = steps
        };
    }

    public OperationResult ExecuteDelete(int key)
    {
        var steps = new List<OperationStep>();

        AddStep(
            steps,
            "Видалення",
            $"Починаємо видалення ключа {key}.",
            rootState: VisualState.Active);

        bool deleted = DeleteInternal(_root, key, steps);

        if (!deleted)
        {
            AddStep(
                steps,
                "Видалення",
                $"Ключ {key} відсутній у дереві.",
                rootState: VisualState.Warning);

            return new OperationResult
            {
                Success = false,
                Message = $"Ключ {key} не знайдено.",
                Steps = steps
            };
        }

        if (_root.Records.Count == 0)
        {
            _root = _root.IsLeaf ? new BTreeNode(true) : _root.Children[0];
        }

        SaveData();

        AddStep(
            steps,
            "Видалення",
            $"Ключ {key} успішно видалено.",
            rootState: VisualState.Active);

        return new OperationResult
        {
            Success = true,
            Message = $"Ключ {key} успішно видалено.",
            Steps = steps
        };
    }

    public void Insert(int key, string data, bool persist = true)
    {
        InsertInternal(key, data, persist, null);
    }

    public void Clear(bool persist = true)
    {
        _root = new BTreeNode(true);
        if (persist)
        {
            SaveData();
        }
    }

    public BTreeSnapshot CreateSnapshot(string title, string description)
    {
        return BuildSnapshot(title, description, null, null, VisualState.Normal);
    }

    public void SaveData()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_root, options);
        File.WriteAllText(_dbFilePath, json);
    }

    public void LoadData()
    {
        if (!File.Exists(_dbFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_dbFilePath);
            var loaded = JsonSerializer.Deserialize<BTreeNode>(json);
            _root = loaded ?? new BTreeNode(true);
            BTreeNode.ResetIdsFromTree(_root);
        }
        catch
        {
            _root = new BTreeNode(true);
        }
    }

    private void InsertInternal(int key, string data, bool persist, List<OperationStep>? steps)
    {
        var root = _root;
        if (root.Records.Count == 2 * _t - 1)
        {
            AddStep(
                steps,
                "Додавання",
                $"Корінь переповнений. Створюємо новий корінь і розщеплюємо вузол.",
                nodeStates: new Dictionary<int, VisualState> { [root.Id] = VisualState.Warning });

            var newRoot = new BTreeNode(false);
            _root = newRoot;
            newRoot.Children.Add(root);
            SplitChild(newRoot, 0, steps);
            InsertNonFull(newRoot, key, data, steps);
        }
        else
        {
            InsertNonFull(root, key, data, steps);
        }

        if (persist)
        {
            SaveData();
        }
    }

    private void InsertNonFull(BTreeNode node, int key, string data, List<OperationStep>? steps)
    {
        int i = node.Records.Count - 1;

        if (node.IsLeaf)
        {
            AddStep(
                steps,
                "Додавання",
                $"Знайдено листовий вузол для вставки ключа {key}.",
                nodeStates: new Dictionary<int, VisualState> { [node.Id] = VisualState.Active });

            while (i >= 0 && node.Records[i].Key > key)
            {
                i--;
            }

            node.Records.Insert(i + 1, new Record { Key = key, Data = data });

            AddStep(
                steps,
                "Додавання",
                $"Ключ {key} вставлено у листовий вузол.",
                recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Inserted },
                nodeStates: new Dictionary<int, VisualState> { [node.Id] = VisualState.Active });
        }
        else
        {
            while (i >= 0 && node.Records[i].Key > key)
            {
                i--;
            }

            i++;
            AddStep(
                steps,
                "Додавання",
                $"Спускаємося до дочірнього вузла #{i + 1} для ключа {key}.",
                nodeStates: new Dictionary<int, VisualState>
                {
                    [node.Id] = VisualState.Visited,
                    [node.Children[i].Id] = VisualState.Active
                });

            if (node.Children[i].Records.Count == 2 * _t - 1)
            {
                SplitChild(node, i, steps);
                if (key > node.Records[i].Key)
                {
                    i++;
                }
            }

            InsertNonFull(node.Children[i], key, data, steps);
        }
    }

    private void SplitChild(BTreeNode parent, int index, List<OperationStep>? steps)
    {
        var fullChild = parent.Children[index];
        var rightNode = new BTreeNode(fullChild.IsLeaf);
        var promotedRecord = fullChild.Records[_t - 1];

        AddStep(
            steps,
            "Розщеплення вузла",
            $"Розщеплюємо переповнений вузол. Ключ {promotedRecord.Key} піднімається вгору.",
            recordStates: new Dictionary<int, VisualState> { [promotedRecord.Key] = VisualState.Promoted },
            nodeStates: new Dictionary<int, VisualState>
            {
                [fullChild.Id] = VisualState.Warning,
                [parent.Id] = VisualState.Active
            });

        for (int j = 0; j < _t - 1; j++)
        {
            rightNode.Records.Add(fullChild.Records[j + _t]);
        }

        if (!fullChild.IsLeaf)
        {
            for (int j = 0; j < _t; j++)
            {
                rightNode.Children.Add(fullChild.Children[j + _t]);
            }
        }

        fullChild.Records.RemoveRange(_t, _t - 1);
        if (!fullChild.IsLeaf)
        {
            fullChild.Children.RemoveRange(_t, _t);
        }

        parent.Children.Insert(index + 1, rightNode);
        parent.Records.Insert(index, promotedRecord);
        fullChild.Records.RemoveAt(_t - 1);

        AddStep(
            steps,
            "Розщеплення вузла",
            $"Після розщеплення батьківський вузол отримав ключ {promotedRecord.Key}, а правий сусід створено окремо.",
            recordStates: new Dictionary<int, VisualState> { [promotedRecord.Key] = VisualState.Promoted },
            nodeStates: new Dictionary<int, VisualState>
            {
                [parent.Id] = VisualState.Active,
                [fullChild.Id] = VisualState.Visited,
                [rightNode.Id] = VisualState.Inserted
            });
    }

    private SearchResult Search(int key, List<OperationStep>? steps)
    {
        LastSearchComparisons = 0;
        return SearchInternal(_root, key, steps);
    }

    private SearchResult SearchInternal(BTreeNode node, int key, List<OperationStep>? steps)
    {
        AddStep(
            steps,
            "Пошук",
            $"Перевіряємо вузол з ключами: {string.Join(", ", node.Records.Select(r => r.Key))}",
            nodeStates: new Dictionary<int, VisualState> { [node.Id] = VisualState.Active });

        int index = BinarySearchInNode(node.Records, key);

        if (index < node.Records.Count)
        {
            LastSearchComparisons++;
            if (node.Records[index].Key == key)
            {
                AddStep(
                    steps,
                    "Пошук",
                    $"Ключ {key} знайдено.",
                    recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Found },
                    nodeStates: new Dictionary<int, VisualState> { [node.Id] = VisualState.Active });

                return new SearchResult(node, index, node.Records[index].Data);
            }
        }

        if (node.IsLeaf)
        {
            AddStep(
                steps,
                "Пошук",
                $"Досягнули листка. Ключ {key} відсутній.",
                nodeStates: new Dictionary<int, VisualState> { [node.Id] = VisualState.Warning });

            return new SearchResult(null, -1, null);
        }

        AddStep(
            steps,
            "Пошук",
            $"Ключ {key} не у поточному вузлі, спускаємося до дочірнього вузла #{index + 1}.",
            nodeStates: new Dictionary<int, VisualState>
            {
                [node.Id] = VisualState.Visited,
                [node.Children[index].Id] = VisualState.Active
            });

        return SearchInternal(node.Children[index], key, steps);
    }

    private int BinarySearchInNode(List<Record> records, int key)
    {
        int left = 0;
        int right = records.Count - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            LastSearchComparisons++;
            if (records[mid].Key == key)
            {
                return mid;
            }

            LastSearchComparisons++;
            if (records[mid].Key < key)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return left;
    }

    private bool DeleteInternal(BTreeNode node, int key, List<OperationStep> steps)
    {
        int index = FindKey(node, key);

        if (index < node.Records.Count && node.Records[index].Key == key)
        {
            if (node.IsLeaf)
            {
                RemoveFromLeaf(node, index, steps);
            }
            else
            {
                RemoveFromNonLeaf(node, index, steps);
            }

            return true;
        }

        if (node.IsLeaf)
        {
            return false;
        }

        bool wasLastChild = index == node.Records.Count;

        AddStep(
            steps,
            "Видалення",
            $"Ключ {key} не знайдено у поточному вузлі, спускаємося в дочірній вузол #{index + 1}.",
            nodeStates: new Dictionary<int, VisualState>
            {
                [node.Id] = VisualState.Visited,
                [node.Children[index].Id] = VisualState.Active
            });

        if (node.Children[index].Records.Count < _t)
        {
            Fill(node, index, steps);
        }

        if (wasLastChild && index > node.Records.Count)
        {
            return DeleteInternal(node.Children[index - 1], key, steps);
        }

        return DeleteInternal(node.Children[index], key, steps);
    }

    private void RemoveFromLeaf(BTreeNode node, int index, List<OperationStep> steps)
    {
        int key = node.Records[index].Key;

        AddStep(
            steps,
            "Видалення з листка",
            $"Ключ {key} знайдено у листовому вузлі і буде видалено без додаткових перебудов.",
            recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Deleted },
            nodeStates: new Dictionary<int, VisualState> { [node.Id] = VisualState.Active });

        node.Records.RemoveAt(index);
    }

    private void RemoveFromNonLeaf(BTreeNode node, int index, List<OperationStep> steps)
    {
        int key = node.Records[index].Key;

        if (node.Children[index].Records.Count >= _t)
        {
            var predecessor = GetPredecessor(node, index);
            node.Records[index] = predecessor;

            AddStep(
                steps,
                "Видалення з внутрішнього вузла",
                $"Замінюємо ключ {key} попередником {predecessor.Key} і далі видаляємо попередник з лівого піддерева.",
                recordStates: new Dictionary<int, VisualState>
                {
                    [predecessor.Key] = VisualState.Promoted
                },
                nodeStates: new Dictionary<int, VisualState>
                {
                    [node.Id] = VisualState.Active,
                    [node.Children[index].Id] = VisualState.Visited
                });

            DeleteInternal(node.Children[index], predecessor.Key, steps);
        }
        else if (node.Children[index + 1].Records.Count >= _t)
        {
            var successor = GetSuccessor(node, index);
            node.Records[index] = successor;

            AddStep(
                steps,
                "Видалення з внутрішнього вузла",
                $"Замінюємо ключ {key} наступником {successor.Key} і далі видаляємо наступник з правого піддерева.",
                recordStates: new Dictionary<int, VisualState>
                {
                    [successor.Key] = VisualState.Promoted
                },
                nodeStates: new Dictionary<int, VisualState>
                {
                    [node.Id] = VisualState.Active,
                    [node.Children[index + 1].Id] = VisualState.Visited
                });

            DeleteInternal(node.Children[index + 1], successor.Key, steps);
        }
        else
        {
            AddStep(
                steps,
                "Злиття під час видалення",
                $"Обидва сусідні піддерева мають мінімум ключів, тому об'єднуємо їх разом із ключем {key}.",
                recordStates: new Dictionary<int, VisualState> { [key] = VisualState.Merged },
                nodeStates: new Dictionary<int, VisualState>
                {
                    [node.Id] = VisualState.Warning,
                    [node.Children[index].Id] = VisualState.Active,
                    [node.Children[index + 1].Id] = VisualState.Active
                });

            Merge(node, index, steps);
            DeleteInternal(node.Children[index], key, steps);
        }
    }

    private Record GetPredecessor(BTreeNode node, int index)
    {
        var current = node.Children[index];
        while (!current.IsLeaf)
        {
            current = current.Children[^1];
        }

        return current.Records[^1];
    }

    private Record GetSuccessor(BTreeNode node, int index)
    {
        var current = node.Children[index + 1];
        while (!current.IsLeaf)
        {
            current = current.Children[0];
        }

        return current.Records[0];
    }

    private void Fill(BTreeNode node, int index, List<OperationStep> steps)
    {
        if (index != 0 && node.Children[index - 1].Records.Count >= _t)
        {
            BorrowFromPrevious(node, index, steps);
        }
        else if (index != node.Records.Count && node.Children[index + 1].Records.Count >= _t)
        {
            BorrowFromNext(node, index, steps);
        }
        else
        {
            if (index != node.Records.Count)
            {
                Merge(node, index, steps);
            }
            else
            {
                Merge(node, index - 1, steps);
            }
        }
    }

    private void BorrowFromPrevious(BTreeNode node, int index, List<OperationStep> steps)
    {
        var child = node.Children[index];
        var sibling = node.Children[index - 1];
        int borrowedKey = node.Records[index - 1].Key;

        child.Records.Insert(0, node.Records[index - 1]);
        if (!child.IsLeaf)
        {
            child.Children.Insert(0, sibling.Children[^1]);
            sibling.Children.RemoveAt(sibling.Children.Count - 1);
        }

        node.Records[index - 1] = sibling.Records[^1];
        sibling.Records.RemoveAt(sibling.Records.Count - 1);

        AddStep(
            steps,
            "Позичання з лівого сусіда",
            $"Щоб не порушити властивості B-дерева, позичаємо ключ через батьківський вузол. Ключ {borrowedKey} змістився вниз.",
            recordStates: new Dictionary<int, VisualState> { [node.Records[index - 1].Key] = VisualState.Borrowed },
            nodeStates: new Dictionary<int, VisualState>
            {
                [node.Id] = VisualState.Active,
                [child.Id] = VisualState.Borrowed,
                [sibling.Id] = VisualState.Visited
            });
    }

    private void BorrowFromNext(BTreeNode node, int index, List<OperationStep> steps)
    {
        var child = node.Children[index];
        var sibling = node.Children[index + 1];
        int borrowedKey = node.Records[index].Key;

        child.Records.Add(node.Records[index]);
        if (!child.IsLeaf)
        {
            child.Children.Add(sibling.Children[0]);
            sibling.Children.RemoveAt(0);
        }

        node.Records[index] = sibling.Records[0];
        sibling.Records.RemoveAt(0);

        AddStep(
            steps,
            "Позичання з правого сусіда",
            $"Щоб не порушити властивості B-дерева, позичаємо ключ через батьківський вузол. Ключ {borrowedKey} змістився вниз.",
            recordStates: new Dictionary<int, VisualState> { [node.Records[index].Key] = VisualState.Borrowed },
            nodeStates: new Dictionary<int, VisualState>
            {
                [node.Id] = VisualState.Active,
                [child.Id] = VisualState.Borrowed,
                [sibling.Id] = VisualState.Visited
            });
    }

    private void Merge(BTreeNode node, int index, List<OperationStep> steps)
    {
        var child = node.Children[index];
        var sibling = node.Children[index + 1];
        int bridgeKey = node.Records[index].Key;

        child.Records.Add(node.Records[index]);
        child.Records.AddRange(sibling.Records);

        if (!child.IsLeaf)
        {
            child.Children.AddRange(sibling.Children);
        }

        node.Records.RemoveAt(index);
        node.Children.RemoveAt(index + 1);

        AddStep(
            steps,
            "Злиття вузлів",
            $"Два сусідні вузли об'єднано в один. Ключ {bridgeKey} опустився між ними як роздільник.",
            recordStates: new Dictionary<int, VisualState> { [bridgeKey] = VisualState.Merged },
            nodeStates: new Dictionary<int, VisualState>
            {
                [node.Id] = VisualState.Warning,
                [child.Id] = VisualState.Merged
            });
    }

    private int FindKey(BTreeNode node, int key)
    {
        int left = 0;
        int right = node.Records.Count;

        while (left < right)
        {
            int mid = left + (right - left) / 2;
            if (node.Records[mid].Key < key)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        return left;
    }

    private int CountRecords(BTreeNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        return node.Records.Count + node.Children.Sum(CountRecords);
    }

    private int GetHeight(BTreeNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        return node.IsLeaf ? 1 : 1 + GetHeight(node.Children[0]);
    }

    private void AddStep(
        List<OperationStep>? steps,
        string title,
        string description,
        Dictionary<int, VisualState>? recordStates = null,
        Dictionary<int, VisualState>? nodeStates = null,
        VisualState rootState = VisualState.Normal)
    {
        if (steps is null)
        {
            return;
        }

        steps.Add(new OperationStep
        {
            Title = title,
            Description = description,
            Snapshot = BuildSnapshot(title, description, recordStates, nodeStates, rootState)
        });
    }

    private BTreeSnapshot BuildSnapshot(
        string title,
        string description,
        Dictionary<int, VisualState>? recordStates,
        Dictionary<int, VisualState>? nodeStates,
        VisualState rootState)
    {
        if (CountRecords() > 250)
        {
            return BTreeSnapshot.Placeholder(
                title,
                description,
                $"У дереві зараз {CountRecords()} записів. Для читабельної анімації краще працювати з невеликим набором даних, а експеримент на 10000 елементів виконувати без повної візуалізації.");
        }

        return new BTreeSnapshot
        {
            Title = title,
            Description = description,
            Root = BuildVisualNode(_root, recordStates, nodeStates, rootState),
            PlaceholderText = null
        };
    }

    private VisualNodeSnapshot BuildVisualNode(
        BTreeNode node,
        IReadOnlyDictionary<int, VisualState>? recordStates,
        IReadOnlyDictionary<int, VisualState>? nodeStates,
        VisualState rootState)
    {
        var state = nodeStates is not null && nodeStates.TryGetValue(node.Id, out var explicitNodeState)
            ? explicitNodeState
            : node == _root
                ? rootState
                : VisualState.Normal;

        return new VisualNodeSnapshot
        {
            Id = node.Id,
            IsLeaf = node.IsLeaf,
            State = state,
            Records = node.Records
                .Select(record => new VisualRecordSnapshot
                {
                    Key = record.Key,
                    Data = record.Data,
                    State = recordStates is not null && recordStates.TryGetValue(record.Key, out var recordState)
                        ? recordState
                        : VisualState.Normal
                })
                .ToList(),
            Children = node.Children
                .Select(child => BuildVisualNode(child, recordStates, nodeStates, rootState))
                .ToList()
        };
    }
}
