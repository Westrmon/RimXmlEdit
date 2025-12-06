using System;
using System.IO;

namespace RimXmlEdit.Models;

public class FileSystemItem
{
    public FileSystemItem(string path, bool needLoadXml = true)
    {
        FullName = path;
        IsDirectory = Directory.Exists(path);
        NeedLoadXml = needLoadXml;
        if (IsDirectory)
        {
            var dirInfo = new DirectoryInfo(path);
            Name = dirInfo.Name;
            LastModified = dirInfo.LastWriteTime;
            Size = -1; // Directories don't have a simple size
        }
        else
        {
            var fileInfo = new FileInfo(path);
            Name = fileInfo.Name;
            LastModified = fileInfo.LastWriteTime;
            Size = fileInfo.Length;
        }
    }

    public string Name { get; }
    public string FullName { get; }
    public bool IsDirectory { get; }
    public long Size { get; }
    public DateTime LastModified { get; }
    public bool NeedLoadXml { get; }
}