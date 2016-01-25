using System;
using System.Collections.Generic;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.LiveTemplates;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.TextControl;
using JetBrains.Util;

namespace BananaSplit
{
    internal static class Utils
    {
        public static string NaiveSuggestVariableName(IInvocationExpression expression, JetHashSet<string> names)
        {
            var methodName = ((IReferenceExpression)expression.InvokedExpression).NameIdentifier.Name.Decapitalize();

            var variableName = methodName;

            for (int i = 1; names.Contains(variableName); i++)
            {
                variableName = methodName + i.ToString();
            }

            names.Add(variableName);
            return variableName;
        }

        public static Action<ITextControl> ExecuteHotspotSession(ISolution solution, HotspotInfo[] hotspots)
        {
            return textControl =>
            {
                var templatesManager = Shell.Instance.GetComponent<LiveTemplatesManager>();
                var escapeAction = LiveTemplatesManager.EscapeAction.LeaveTextAndCaret;
                var hotspotSession = templatesManager.CreateHotspotSessionAtopExistingText(solution,
                    TextRange.InvalidRange, textControl, escapeAction, hotspots);
                hotspotSession.Execute();
            };
        }

        public static bool IsWholeStatementRangeSelected(TextRange selection, IDocument document)
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

        public static DocumentCoords GetPositionAfterStatement(IStatement statement, IDocument document)
        {
            int lastStatementEndOffset = statement.GetDocumentRange().TextRange.EndOffset;
            return document.GetCoordsByOffset(lastStatementEndOffset);
        }

        public static bool IsMethodInvoked(IInvocationExpression invocationExpression, IMethod method)
        {
            var calledMethod = invocationExpression.InvocationExpressionReference.Resolve().Result.DeclaredElement as IMethod;
            return method.Equals(calledMethod);
        }
    }
}