using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public interface IQuickSearch
{
    event Action<string>? OnItemSelected;
}
