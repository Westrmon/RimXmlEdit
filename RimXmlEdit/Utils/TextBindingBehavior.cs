using Avalonia;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Utils;

namespace RimXmlEdit.Utils;

public class TextBindingBehavior : Behavior<TextEditor>
{
    private TextEditor? _textEditor;

    public static readonly StyledProperty<string> BindableTextProperty =
        AvaloniaProperty.Register<TextBindingBehavior, string>(
            nameof(BindableText),
            defaultBindingMode: BindingMode.TwoWay);

    public string BindableText
    {
        get => GetValue(BindableTextProperty);
        set => SetValue(BindableTextProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is TextEditor textEditor)
        {
            _textEditor = textEditor;
            _textEditor.Document.TextChanged += OnEditorTextChanged;
            this.GetObservable(BindableTextProperty).Subscribe(OnBindableTextChanged);
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (_textEditor != null)
        {
            _textEditor.Document.TextChanged -= OnEditorTextChanged;
        }
    }

    private void OnEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_textEditor?.Document != null)
        {
            // Avoid recursion if the change came from the ViewModel
            if (BindableText != _textEditor.Document.Text)
            {
                BindableText = _textEditor.Document.Text;
            }
        }
    }

    private void OnBindableTextChanged(string text)
    {
        if (_textEditor?.Document != null && text != null)
        {
            if (text != _textEditor.Document.Text)
            {
                _textEditor.Document.Text = text;
            }
        }
    }
}
