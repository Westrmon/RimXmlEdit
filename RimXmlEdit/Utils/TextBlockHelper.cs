using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Utils;

public static class TextBlockHelper
{
    /// <summary> 定义一个名为 "Inlines" 的附加属性，其类型为 IEnumerable<Inline> </summary>
    public static readonly AttachedProperty<IEnumerable<Inline>> InlinesProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IEnumerable<Inline>>(
            "Inlines", typeof(TextBlockHelper));

    // 静态构造函数，用于注册属性变化的回调
    static TextBlockHelper()
    {
        InlinesProperty.Changed.AddClassHandler<TextBlock>(OnInlinesChanged);
    }

    /// <summary>
    /// 获取 Inlines 附加属性的值
    /// </summary>
    public static IEnumerable<Inline> GetInlines(TextBlock textBlock)
    {
        return textBlock.GetValue(InlinesProperty);
    }

    /// <summary>
    /// 设置 Inlines 附加属性的值
    /// </summary>
    public static void SetInlines(TextBlock textBlock, IEnumerable<Inline> value)
    {
        textBlock.SetValue(InlinesProperty, value);
    }

    /// <summary>
    /// 当 Inlines 附加属性的值发生变化时，此方法被调用
    /// </summary>
    private static void OnInlinesChanged(TextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        // 检查新值是否是一个 Inline 集合
        if (e.NewValue is IEnumerable<Inline> inlines)
        {
            // 先清空 TextBlock 已有的内容
            textBlock.Inlines.Clear();
            // 然后将新的 Inline 对象集合添加进去
            textBlock.Inlines.AddRange(inlines);
        }
    }
}
