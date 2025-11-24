using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public partial class GetTextViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isFolder;
}
