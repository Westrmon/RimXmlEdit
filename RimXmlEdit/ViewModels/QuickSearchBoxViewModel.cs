using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RimXmlEdit.Core.Parse;
using RimXmlEdit.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public partial class QuickSearchBoxViewModel : ViewModelBase, IQuickSearch
{
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isSelectionChanging = false;

    [ObservableProperty]
    private Dictionary<string, int> _weights;

    [ObservableProperty]
    private List<string> _dataSource;

    /// <summary>
    /// 用户输入的搜索文本，双向绑定到TextBox
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 过滤后的搜索结果，绑定到ListBox
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _filteredResults;

    /// <summary>
    /// 在结果列表中当前选中的项
    /// </summary>
    [ObservableProperty]
    private SearchResultItem? _selectedItem;

    /// <summary>
    /// 控制结果弹窗是否打开
    /// </summary>
    [ObservableProperty]
    private bool _isPopupOpen;

    /// <summary>
    /// 当用户确认选择后触发的事件
    /// </summary>
    public event Action<string>? OnItemSelected;

    public QuickSearchBoxViewModel(ModParser modParse)
    {
        FilteredResults = [];
        Task.Run(() =>
        {
            modParse.Parse();
            Weights = modParse.ModReferenceCount;
            DataSource = modParse.ModReferenceCount.Select(kvp => kvp.Key).ToList();
        });
    }

    private void LoadSimpleData()
    {
        _filteredResults = new ObservableCollection<SearchResultItem>();

        var items = new List<string>();
        for (int i = 0; i < 12000; i++)
        {
            items.Add($"Item number {i} - Some random text here");
        }
        items.Add("Apple");
        items.Add("Application");
        items.Add("Avalonia");
        items.Add("Avalonia UI Framework");

        var weights = new Dictionary<string, int>
            {
                { "Avalonia", 100 },
                { "Avalonia UI Framework", 90 },
                { "Apple", 50 }
            };
        Weights = weights;
        DataSource = items;
    }

    async partial void OnSearchTextChanged(string value)
    {
        if (_isSelectionChanging) return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(value))
        {
            IsPopupOpen = false;
            FilteredResults.Clear();
            return;
        }

        try
        {
            await Task.Delay(300, _cancellationTokenSource.Token);
            await PerformSearchAsync(value, _cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
        }
    }

    // 当 SelectedItem 属性变化时，此方法会被自动调用
    partial void OnSelectedItemChanged(SearchResultItem? value)
    {
        if (value == null) return;

        _isSelectionChanging = true;
        SearchText = value.FullText;
        _isSelectionChanging = false;

        IsPopupOpen = false;
        FilteredResults.Clear();

        OnItemSelected?.Invoke(value.FullText);
        SearchText = string.Empty;
    }

    private async Task PerformSearchAsync(string searchText, CancellationToken token)
    {
        var results = await Task.Run(() =>
        {
            if (token.IsCancellationRequested) return null;

            // 核心搜索与排序逻辑
            return _dataSource
                .Where(item => item.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Select(item => new
                {
                    Item = item,
                    Weight = _weights.TryGetValue(item, out var weight) ? weight : 0,
                    StartsWith = item.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)
                })
                .OrderByDescending(x => x.StartsWith)
                .ThenByDescending(x => x.Weight)
                .ThenBy(x => x.Item.Length)
                .ThenBy(x => x.Item)
                .Take(100)
                .Select(x => new SearchResultItem(x.Item, searchText))
                .ToList();
        }, token);

        if (results != null && !token.IsCancellationRequested)
        {
            FilteredResults.Clear();
            foreach (var item in results)
            {
                FilteredResults.Add(item);
            }
            IsPopupOpen = FilteredResults.Any();
        }
    }

    /// <summary>
    /// 用于处理键盘导航中的回车键
    /// </summary>
    [RelayCommand]
    private void ConfirmSelection()
    {
        if (SelectedItem != null)
        {
            OnItemSelected?.Invoke(SelectedItem.FullText);
            SearchText = string.Empty;
        }
    }
}
