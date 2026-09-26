using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SqlUI.App.Views;

public class SqlCompletionData : ICompletionData
{
    public SqlCompletionData(string text) => Text = text;

    public string Text { get; }
    public object Content => Text;
    public object Description => Text;
    public double Priority => 0;
    public ImageSource? Image => null;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        => textArea.Document.Replace(completionSegment, Text);
}
