using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Models;

public partial class SearchResultItem : ObservableObject
{
    [ObservableProperty]
    private string _fullText;

    [ObservableProperty]
    private IEnumerable<Inline> _formattedText;

    public SearchResultItem(string fullText, string searchText)
    {
        _fullText = fullText;
        Dispatcher.UIThread.Invoke(() =>
        {
            _formattedText = HighlightSearchText(fullText, searchText);
        });
    }

    private static IEnumerable<Inline> HighlightSearchText(string fullText, string searchText)
    {
        if (string.IsNullOrEmpty(searchText) || !fullText.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new Run(fullText) };
        }

        var inlines = new List<Inline>();
        var lastIndex = 0;
        var currentIndex = fullText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);

        while (currentIndex != -1)
        {
            if (currentIndex > lastIndex)
            {
                inlines.Add(new Run(fullText.Substring(lastIndex, currentIndex - lastIndex)));
            }

            inlines.Add(new Run(fullText.Substring(currentIndex, searchText.Length))
            {
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.CornflowerBlue)
            });

            lastIndex = currentIndex + searchText.Length;
            currentIndex = fullText.IndexOf(searchText, lastIndex, StringComparison.OrdinalIgnoreCase);
        }

        if (lastIndex < fullText.Length)
        {
            inlines.Add(new Run(fullText.Substring(lastIndex)));
        }
        return inlines;
    }
}
