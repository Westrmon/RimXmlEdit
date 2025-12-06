using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;

namespace RimXmlEdit.Views.DialogViews;

public partial class TemplateXmlView : UserControl
{
    public TemplateXmlView()
    {
        InitializeComponent();
        DataContext = GlobalSingletonHelper.Service.GetRequiredService<TemplateXmlViewModel>();
    }
}