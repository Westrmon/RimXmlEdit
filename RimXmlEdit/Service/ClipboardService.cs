using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Service;

public static class ClipboardService
{
    public static List<string> CopiedFiles { get; private set; } = new();
    public static bool IsCut { get; set; } = false;

    public static void SetCopiedFiles(IEnumerable<string> paths, bool isCut)
    {
        CopiedFiles = new List<string>(paths);
        IsCut = isCut;
    }

    public static void Clear()
    {
        CopiedFiles.Clear();
        IsCut = false;
    }
}
