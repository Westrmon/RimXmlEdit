using System;
using RimXmlEdit.Models;

namespace RimXmlEdit.ViewModels;

public interface ISimpleFileExplorer
{
    event EventHandler<FileSystemItem> OnOpenFile;

    event EventHandler<TemplateXmlViewModel>? OnCreateTemplate;
    
    event Action<string>? OnDeleteFile;
}