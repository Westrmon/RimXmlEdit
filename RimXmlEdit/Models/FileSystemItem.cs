using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Models;

public class FileSystemItem
{
    public string Name { get; }
    public string FullName { get; }
    public bool IsDirectory { get; }
    public long Size { get; }
    public DateTime LastModified { get; }

    public FileSystemItem(string path)
    {
        FullName = path;
        IsDirectory = Directory.Exists(path);

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
}
