using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using RimXmlEdit.ViewModels;

namespace RimXmlEdit.Views;

public class QuickSearchBox : TemplatedControl
{
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var searchTextBox = e.NameScope.Find<TextBox>("PART_SearchTextBox");
        var resultsListBox = e.NameScope.Find<ListBox>("PART_ResultsListBox");

        if (searchTextBox != null)
        {
            searchTextBox.KeyDown += HandleKeyDown;
        }
        if (resultsListBox != null)
        {
            resultsListBox.KeyDown += HandleKeyDown;
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (this.DataContext is QuickSearchBoxViewModel viewModel && viewModel.ConfirmSelectionCommand.CanExecute(null))
            {
                viewModel.ConfirmSelectionCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
