using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RimXmlEdit.Models;

// defType
public class Category
{
    public string Name { get; set; } = string.Empty;
    public List<SubCategory> SubCategories { get; set; } = new();
}

// 具体def模板
public class SubCategory
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

// 过滤器
public partial class OptionModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}