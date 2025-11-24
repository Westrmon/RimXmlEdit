using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.IO;

namespace RimXmlEdit;

public partial class CrashReportWindow : Window
{
    public CrashReportWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Environment.Exit(0);
    }

    private void Button_Click_1(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Process.Start("explorer.exe", Path.Combine(Environment.CurrentDirectory, "Logs"));
        Environment.Exit(0);
    }
}
