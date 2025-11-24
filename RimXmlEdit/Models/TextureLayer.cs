using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace RimXmlEdit.Models;

public partial class TextureLayer : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _filePath;

    [ObservableProperty]
    private Bitmap? _image;

    // X轴偏移
    [DefaultValue(0)]
    [ObservableProperty]
    private double _offsetX;

    // Y轴偏移
    [DefaultValue(0)]
    [ObservableProperty]
    private double _offsetY;

    // 缩放 (默认1.0)
    [ObservableProperty]
    private double _scale = 1;

    [DefaultValue(false)]
    [ObservableProperty]
    private bool _isSelected;

    [DefaultValue(0)]
    [ObservableProperty]
    private double _rotation;

    public TextureLayer(string filePath, string name = "Layer")
    {
        FilePath = filePath;
        Name = name;
        try
        {
            Image = new Bitmap(filePath);
        }
        catch (Exception)
        {
            // 处理加载失败，比如设置一个错误占位符
            Name = $"{name} (Load Error)";
        }
    }

    public void Dispose()
    {
        Image?.Dispose();
    }
}
