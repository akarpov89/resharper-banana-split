using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.DocumentManagers.Transactions;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;
using IBlock = JetBrains.ReSharper.Psi.CSharp.Tree.IBlock;
using IReferenceExpression = JetBrains.ReSharper.Psi.CSharp.Tree.IReferenceExpression;

namespace BananaSplit
{
  [ContextAction(
    Name = "Inline calls",
    Description = "Inline calls",
    Group = CSharpContextActions.GroupID)]
  public class InlineCallsContextAction : ContextActionBase
  {
    [NotNull] private readonly ICSharpContextActionDataProvider myProvider;

    public InlineCallsContextAction([NotNull] ICSharpContextActionDataProvider provider)
    {
      myProvider = provider;
    }

    public override string Text => "Inline calls";

    protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
      var block = myProvider.GetSelectedElement<IBlock>().NotNull();
      var statements = block.GetStatementsRange(myProvider.SelectedTreeRange).Statements;

      var invocation = ExtractInvocation(statements[0]).NotNull();

      for (int i = 1; i < statements.Count - 1; i++)
      {
        var nextInvocation = ExtractInvocation(statements[i]).NotNull();
        invocation = MergeInvocations(invocation, nextInvocation);
      }

      var lastDeclaredVariable = ExtractDeclaredVariableName(statements[statements.Count - 2]);
      var lastInvocationUse =
        FindLastInvocationUse(statements[statements.Count - 1], lastDeclaredVariable.Name).NotNull();
      var lastInvocation = lastInvocationUse.GetContainingNode<IInvocationExpression>().NotNull();

      MergeInvocations(invocation, lastInvocation);

      for (int i = 0; i < statements.Count - 1; i++)
      {
        block.RemoveStatement((ICSharpStatement) statements[i]);
      }

      var lastStatementCoords = DocumentHelper.GetPositionAfterStatement(statements[statements.Count - 1], myProvider.Document);

      return textControl =>
      {
        textControl.Selection.RemoveSelection(false);
        textControl.Caret.MoveTo(lastStatementCoords, CaretVisualPlacement.DontScrollIfVisible);
      };
    }

    [NotNull]
    private static IInvocationExpression MergeInvocations(
      [NotNull] IInvocationExpression first, [NotNull] IInvocationExpression next)
    {
      var invokedExpression = (IReferenceExpression) next.InvokedExpression;
      invokedExpression.SetQualifierExpression(first);
      return next;
    }

    public override bool IsAvailable(IUserDataHolder cache)
    {
      if (!DocumentHelper.IsWholeStatementRangeSelected(myProvider.Selection, myProvider.Document)) return false;

      var block = myProvider.GetSelectedElement<IBlock>();
      var statementsRange = block?.GetStatementsRange(myProvider.SelectedTreeRange);
      if (statementsRange == null || statementsRange.Statements.Count < 2) return false;

      var statements = statementsRange.Statements;

      if (!AreDeclarationsBeforeLastStatement(statements)) return false;
      if (!AreDeclarationsFollowPattern(statements)) return false;
      if (!IsNoOtherVariableReferences(statements, block)) return false;

      return true;
    }

    private bool AreDeclarationsBeforeLastStatement([NotNull] IList<IStatement> statements)
    {
      for (int i = 0; i < statements.Count - 1; i++)
      {
        var statement = statements[i] as IDeclarationStatement;
        if (statement?.VariableDeclarations.Count != 1) return false;
      }

      return true;
    }

    private bool AreDeclarationsFollowPattern([NotNull] IList<IStatement> statements)
    {
      for (int i = 0; i < statements.Count - 2; i++)
      {
        var declaredVariable = ExtractDeclaredVariableName(statements[i]);
        var nextInvocationTarget = ExtractInvocationTargetName(statements[i + 1]);

        if (declaredVariable.Name != nextInvocationTarget?.Name) return false;
      }

      var lastDeclaredVariable = ExtractDeclaredVariableName(statements[statements.Count - 2]);
      var lastInvocationUse = FindLastInvocationUse(statements[statements.Count - 1], lastDeclaredVariable.Name);

      return lastInvocationUse != null;
    }

    [NotNull]
    private static ICSharpIdentifier ExtractDeclaredVariableName(IStatement statement)
      => ((IDeclarationStatement) statement).VariableDeclarations[0].NameIdentifier;

    [CanBeNull]
    private static ICSharpIdentifier ExtractInvocationTargetName([NotNull] IStatement statement)
    {
      var invocation = ExtractInvocation(statement);
      return ExtractInvocationTargetName(invocation);
    }

    [CanBeNull]
    private static IInvocationExpression ExtractInvocation([NotNull] IStatement statement)
    {
      var declaration = ((IDeclarationStatement) statement).VariableDeclarations[0];
      return declaration?.Initial?.FirstChild as IInvocationExpression;
    }

    [CanBeNull]
    private static ICSharpIdentifier ExtractInvocationTargetName([CanBeNull] IInvocationExpression invocation)
    {
      var invokedExpression = invocation?.InvokedExpression as IReferenceExpression;
      var target = invokedExpression?.QualifierExpression as IReferenceExpression;
      return target?.NameIdentifier;
    }

    [CanBeNull]
    private static ICSharpIdentifier FindLastInvocationUse([NotNull] IStatement statement, [NotNull] string name)
    {
      ICSharpIdentifier lastInvocationUse = null;

      foreach (var invocation in statement.Descendants<IInvocationExpression>())
      {
        var currentInvocationTarget = ExtractInvocationTargetName(invocation);

        if (name == currentInvocationTarget?.Name)
        {
          lastInvocationUse = currentInvocationTarget;
          break;
        }
      }

      return lastInvocationUse;
    }

    private static bool IsNoOtherVariableReferences([NotNull] IList<IStatement> statements, [NotNull] IBlock block)
    {
      var blockStatements = block.Statements;
      int selectionStart = blockStatements.IndexOf((ICSharpStatement) statements[0]);

      for (int current = 0; current < statements.Count - 1; current++)
      {
        var declaredVariable = ExtractDeclaredVariableName(statements[current]);

        var nextInvocationTarget = current == statements.Count - 2
          ? FindLastInvocationUse(statements[current + 1], declaredVariable.Name)
          : ExtractInvocationTargetName(statements[current + 1]).NotNull();

        for (int next = selectionStart + current + 1; next < blockStatements.Count; next++)
        {
          if (IsReferencedExcept(declaredVariable.Name, blockStatements[next], nextInvocationTarget))
          {
            return false;
          }
        }
      }

      return true;
    }

    private static bool IsReferencedExcept(
      [NotNull] string name, [NotNull] IStatement statement, [CanBeNull] ICSharpIdentifier exception)
    {
      foreach (var identifier in statement.Descendants<ICSharpIdentifier>())
      {
        if (identifier != exception && name == identifier.Name)
          return true;
      }

      return false;
    }
  }
}