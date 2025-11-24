using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Models;

public partial class ModDependency : ObservableObject
{
    [ObservableProperty]
    private string _packageId;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _steamWorkshopUrl;

    [ObservableProperty]
    private string _downloadUrl;
}
