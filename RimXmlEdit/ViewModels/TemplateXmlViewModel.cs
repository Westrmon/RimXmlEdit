using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;

namespace RimXmlEdit.ViewModels;

public partial class TemplateXmlViewModel : ViewModelBase
{
    private readonly List<Category> _allCategories;
    private readonly ExampleXmlManager _xmlManager;

    [ObservableProperty] private string _filePath = string.Empty;
    private bool _isReflush;

    [ObservableProperty] public bool _isVisible;

    [ObservableProperty] private Category? _selectedPrimaryCategory;

    [ObservableProperty] private SubCategory? _selectedSecondaryCategory;

    public TemplateXmlViewModel(ExampleXmlManager xmlManager)
    {
        _xmlManager = xmlManager;
        _xmlManager.Init();

        _allCategories = GetMockData();
        _allCategories.Insert(0, new Category { Name = "None" });
        Categories = new ObservableCollection<Category>(_allCategories);
        SelectedPrimaryCategory = Categories.First();
    }

    public ObservableCollection<Category> Categories { get; }
    public ObservableCollection<SubCategory> SecondaryCategories { get; } = new();
    public ObservableCollection<OptionModel> Options { get; } = new();

    partial void OnSelectedPrimaryCategoryChanged(Category? value)
    {
        _isReflush = true;
        SecondaryCategories.Clear();
        SelectedSecondaryCategory = null;
        Options.Clear();

        if (value == null) return;
        foreach (var sub in value.SubCategories)
            SecondaryCategories.Add(sub);

        _isReflush = false;
        IsVisible = SecondaryCategories.Any();
    }

    partial void OnSelectedSecondaryCategoryChanged(SubCategory? value)
    {
        if (_isReflush) return;
        Options.Clear();
        if (value != null) GenerateOptions(value.Name);
    }

    private void GenerateOptions(string subCategoryName)
    {
        var filters = _xmlManager.GetFilterInfos(subCategoryName);
        filters.ForEach(t => Options.Add(new OptionModel
        {
            Title = t.Name,
            Description = t.Description
        }));
    }

    private List<Category> GetMockData()
    {
        var types = _xmlManager.GetAllTemplateType();
        return types.Select(t => new Category
        {
            Name = t,
            SubCategories = _xmlManager.GetTemplateByType(t).Select(g => new SubCategory
            {
                Name = g,
                Code = _xmlManager.GetDescription(g)
            }).ToList()
        }).ToList();
    }
}