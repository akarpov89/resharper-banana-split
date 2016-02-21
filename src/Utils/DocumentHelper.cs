using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.dataStructures.TypedIntrinsics;

namespace BananaSplit
{
  internal static class DocumentHelper
  {
    public static bool IsWholeStatementRangeSelected(TextRange selection, [NotNull] IDocument document)
    {
      if (selection.StartOffset < 0 || selection.EndOffset < 0) return false;

      var startLine = document.GetCoordsByOffset(selection.StartOffset).Line;
      var startLineOffset = document.GetLineStartOffset(startLine);

      var rangeBeforeSelection = new TextRange(startLineOffset, selection.StartOffset);
      var beforeSelectionText = document.GetText(rangeBeforeSelection);
      if (!beforeSelectionText.IsNullOrWhitespace()) return false;

      var endLine = document.GetCoordsByOffset(selection.EndOffset).Line;
      var endLineOffset = document.GetLineEndOffsetNoLineBreak(endLine);
      var endLineOffsetWithLineBreak = document.GetLineEndOffsetWithLineBreak(endLine);

      if (selection.EndOffset == endLineOffset || selection.EndOffset == endLineOffsetWithLineBreak) return true;

      var afterSelectionRange = new TextRange(selection.EndOffset, endLineOffset);
      var afterSelectionText = document.GetText(afterSelectionRange);

      return afterSelectionText.IsNullOrWhitespace();
    }

    public static DocumentCoords GetPositionAfterStatement([NotNull] IStatement statement, [NotNull] IDocument document)
    {
      int lastStatementEndOffset = statement.GetDocumentRange().TextRange.EndOffset;
      return document.GetCoordsByOffset(lastStatementEndOffset);
    }

    public static Int32<DocLine> GetNodeEndLine([NotNull] ITreeNode node, [NotNull] IDocument document)
    {
      var documentRange = node.GetDocumentRange();
      int endOffset = documentRange.TextRange.EndOffset;
      var coords = document.GetCoordsByOffset(endOffset);
      return coords.Line;
    }
  }
}