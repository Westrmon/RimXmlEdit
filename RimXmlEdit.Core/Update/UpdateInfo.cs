using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Core.Update;

public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public bool IsMandatory { get; set; }
    public string LatestVersion { get; set; }
    public string CurrentVersion { get; set; }
    public string DownloadUrl { get; set; }
    public string ReleaseNotes { get; set; }
}
