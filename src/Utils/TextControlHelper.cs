using JetBrains.DocumentModel;
using JetBrains.TextControl;
using JetBrains.Util.dataStructures.TypedIntrinsics;

namespace BananaSplit
{
  public static class TextControlHelper
  {
    public static void MoveCaretToEndOfLine(this ITextControl textControl, Int32<DocLine> line)
    {
      var document = textControl.Document;
      int endOffset = document.GetLineEndOffsetNoLineBreak(line);
      textControl.Caret.MoveTo(endOffset, CaretVisualPlacement.DontScrollIfVisible);
    }
  }
}