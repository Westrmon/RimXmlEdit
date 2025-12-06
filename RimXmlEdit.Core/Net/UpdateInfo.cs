namespace RimXmlEdit.Core.Net;

public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public bool IsMandatory { get; set; }
    public string LatestVersion { get; set; }
    public string CurrentVersion { get; set; }
    public string DownloadUrl { get; set; }
    public string ReleaseNotes { get; set; }
}