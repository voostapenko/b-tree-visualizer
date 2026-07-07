using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Лаб3ПА.Models;

namespace Лаб3ПА.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int DeletePreviewDelayMs = 900;

    private readonly BTree _tree;
    private bool _isBusy;

    private decimal _inputKey;
    public decimal InputKey
    {
        get => _inputKey;
        set => SetProperty(ref _inputKey, value);
    }

    private string _inputData = string.Empty;
    public string InputData
    {
        get => _inputData;
        set => SetProperty(ref _inputData, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _experimentStats = string.Empty;
    public string ExperimentStats
    {
        get => _experimentStats;
        set => SetProperty(ref _experimentStats, value);
    }

    private string _resultText = string.Empty;
    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    private string _treeSummary = string.Empty;
    public string TreeSummary
    {
        get => _treeSummary;
        set => SetProperty(ref _treeSummary, value);
    }

    private string _visualizationNotice = string.Empty;
    public string VisualizationNotice
    {
        get => _visualizationNotice;
        set
        {
            if (SetProperty(ref _visualizationNotice, value))
            {
                OnPropertyChanged(nameof(HasVisualizationNotice));
            }
        }
    }

    public bool HasVisualizationNotice => !string.IsNullOrWhiteSpace(VisualizationNotice);

    private BTreeSnapshot? _currentSnapshot;
    public BTreeSnapshot? CurrentSnapshot
    {
        get => _currentSnapshot;
        set => SetProperty(ref _currentSnapshot, value);
    }

    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (SetProperty(ref _zoom, value))
            {
                OnPropertyChanged(nameof(ZoomLabel));
            }
        }
    }

    public string ZoomLabel => $"Масштаб: {Zoom:0.00}x";

    public List<SearchStat> LastExperimentStats { get; private set; } = new();

    public IAsyncRelayCommand AddRecordCommand { get; }
    public IAsyncRelayCommand SearchRecordCommand { get; }
    public IAsyncRelayCommand EditRecordCommand { get; }
    public IAsyncRelayCommand DeleteRecordCommand { get; }
    public IAsyncRelayCommand ClearDatabaseCommand { get; }
    public IAsyncRelayCommand RunExperimentCommand { get; }

    public MainWindowViewModel()
    {
        _tree = new BTree(25);
        _tree.LoadData();

        AddRecordCommand = new AsyncRelayCommand(AddRecordAsync);
        SearchRecordCommand = new AsyncRelayCommand(SearchRecordAsync);
        EditRecordCommand = new AsyncRelayCommand(EditRecordAsync);
        DeleteRecordCommand = new AsyncRelayCommand(DeleteRecordAsync);
        ClearDatabaseCommand = new AsyncRelayCommand(ClearDatabaseAsync);
        RunExperimentCommand = new AsyncRelayCommand(RunExperimentAsync);

        RefreshTreeView();
        StatusMessage = "База даних завантажена.";
        ResultText = "Дерево показує поточний стан структури, а змінені записи підсвічуються на самому дереві.";
    }

    private async Task AddRecordAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var result = _tree.ExecuteInsert((int)InputKey, InputData.Trim());
        await ApplyStaticHighlightAsync(
            result,
            VisualState.Inserted,
            VisualState.Promoted,
            VisualState.Merged,
            VisualState.Warning);
    }

    private async Task SearchRecordAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var result = _tree.ExecuteSearch((int)InputKey);
        await ApplyStaticHighlightAsync(
            result,
            VisualState.Found,
            VisualState.Visited,
            VisualState.Active,
            VisualState.Warning,
            VisualState.Missing);
    }

    private async Task EditRecordAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var result = _tree.ExecuteEdit((int)InputKey, InputData.Trim());
        await ApplyStaticHighlightAsync(
            result,
            VisualState.Edited,
            VisualState.Found,
            VisualState.Warning);
    }

    private async Task DeleteRecordAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var result = _tree.ExecuteDelete((int)InputKey);
        await ApplyDeletePreviewAsync(result);
    }

    private async Task ClearDatabaseAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _tree.Clear();
        StatusMessage = "Базу даних очищено.";
        ResultText = "Дерево скинуто до порожнього кореня.";
        RefreshTreeView();
        await Task.CompletedTask;
    }

    private async Task RunExperimentAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            StatusMessage = "Формується тестова база на 10000 записів...";
            ResultText = "Після експерименту повна візуалізація дерева вимикається, щоб не перевантажувати інтерфейс.";

            var experimentResult = await Task.Run(() =>
            {
                var random = new Random();
                var keys = Enumerable.Range(1, 10000)
                    .OrderBy(_ => random.Next())
                    .ToList();

                _tree.Clear(persist: false);

                foreach (var key in keys)
                {
                    _tree.Insert(key, $"Data_{key}", persist: false);
                }

                _tree.SaveData();

                var attempts = keys.OrderBy(_ => random.Next()).Take(25).ToList();
                var stats = new List<SearchStat>();
                long sum = 0;

                for (int i = 0; i < attempts.Count; i++)
                {
                    _tree.Search(attempts[i]);
                    stats.Add(new SearchStat
                    {
                        AttemptNumber = i + 1,
                        Key = attempts[i],
                        Comparisons = _tree.LastSearchComparisons
                    });
                    sum += _tree.LastSearchComparisons;
                }

                return new
                {
                    Stats = stats,
                    Average = sum / 25.0
                };
            });

            LastExperimentStats = experimentResult.Stats;
            ExperimentStats = $"10000 записів створено. Середнє число порівнянь за 25 пошуків: {experimentResult.Average:0.00}.";
            StatusMessage = "Експеримент завершено.";
            ResultText = "Для демонстрації роботи дерева використовуй звичайні операції з невеликою кількістю записів. Таблицю 25 пошуків можна відкрити окремо.";
            RefreshTreeView();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ApplyStaticHighlightAsync(OperationResult result, params VisualState[] preferredStates)
    {
        _isBusy = true;
        try
        {
            UpdateTreeMeta();

            var snapshot = SelectSnapshot(result, preferredStates);
            if (snapshot is not null)
            {
                CurrentSnapshot = snapshot;
            }
            else
            {
                RefreshTreeView();
            }

            StatusMessage = result.Message;
            ResultText = BuildResultText(result);
            await Task.CompletedTask;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ApplyDeletePreviewAsync(OperationResult result)
    {
        _isBusy = true;
        try
        {
            UpdateTreeMeta();
            StatusMessage = result.Message;
            ResultText = BuildResultText(result);

            if (!result.Success)
            {
                CurrentSnapshot = SelectSnapshot(result, VisualState.Warning, VisualState.Missing) ?? CurrentSnapshot;
                return;
            }

            var previewSnapshot = SelectSnapshot(
                result,
                VisualState.Deleted,
                VisualState.Merged,
                VisualState.Borrowed,
                VisualState.Promoted,
                VisualState.Warning,
                VisualState.Active);

            if (previewSnapshot is not null)
            {
                CurrentSnapshot = previewSnapshot;
                await Task.Delay(DeletePreviewDelayMs);
            }

            RefreshTreeView();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private BTreeSnapshot? SelectSnapshot(OperationResult result, params VisualState[] preferredStates)
    {
        if (result.Steps is null || result.Steps.Count == 0)
        {
            return null;
        }

        foreach (var state in preferredStates)
        {
            for (int i = result.Steps.Count - 1; i >= 0; i--)
            {
                var snapshot = result.Steps[i].Snapshot;
                if (SnapshotContainsState(snapshot, state))
                {
                    return snapshot;
                }
            }
        }

        return result.Steps[^1].Snapshot;
    }

    private static bool SnapshotContainsState(BTreeSnapshot? snapshot, VisualState targetState)
    {
        if (snapshot?.Root is null)
        {
            return false;
        }

        return NodeContainsState(snapshot.Root, targetState);
    }

    private static bool NodeContainsState(VisualNodeSnapshot node, VisualState targetState)
    {
        if (node.State == targetState)
        {
            return true;
        }

        if (node.Records.Any(record => record.State == targetState))
        {
            return true;
        }

        return node.Children.Any(child => NodeContainsState(child, targetState));
    }

    private string BuildResultText(OperationResult result)
    {
        if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.Value))
            {
                return $"Дані: {result.Value}";
            }

            return "Дерево оновлено і підсвітило змінений елемент.";
        }

        return "Операцію не виконано. Подивись на поточний стан дерева та повідомлення вище.";
    }

    private void RefreshTreeView()
    {
        UpdateTreeMeta();

        if (_tree.CountRecords() > 250)
        {
            CurrentSnapshot = BTreeSnapshot.Placeholder(
                "Поточний стан дерева",
                VisualizationNotice,
                VisualizationNotice);
        }
        else
        {
            CurrentSnapshot = _tree.CreateSnapshot(
                "Поточний стан дерева",
                "Актуальний стан B-tree після останньої операції.");
        }
    }

    private void UpdateTreeMeta()
    {
        TreeSummary = $"B-tree: t = {_tree.Degree} | вузлів: {CountNodes(_tree.GetRoot())} | записів: {_tree.CountRecords()} | висота: {_tree.GetHeight()}";

        if (_tree.CountRecords() > 250)
        {
            VisualizationNotice = "Дерево надто велике для повної читабельної візуалізації. Після експерименту на 10000 записів відображається лише службове повідомлення.";
        }
        else
        {
            VisualizationNotice = string.Empty;
        }
    }

    private static int CountNodes(BTreeNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        return 1 + node.Children.Sum(CountNodes);
    }
}
