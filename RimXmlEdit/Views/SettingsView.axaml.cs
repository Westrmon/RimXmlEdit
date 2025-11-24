using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using RimXmlEdit.Models;
using RimXmlEdit.ViewModels;

namespace RimXmlEdit;

public partial class SettingsView : Window
{
    public SettingsView()
    {
        InitializeComponent();
        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.CloseAction = () => this.Close();
            }
        };
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetPosition(this).Y < 35)
            this.BeginMoveDrag(e);
    }

    private void OnNavigationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is NavigationItem navItem)
        {
            // 1. 根据 TargetControlName 找到控件
            var targetControl = this.FindControl<Control>(navItem.TargetControlName);

            if (targetControl != null)
            {
                // 2. 将控件滚动到可视区域
                targetControl.BringIntoView();
            }
        }
    }
}
