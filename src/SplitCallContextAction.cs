using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros.Implementations;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Templates;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace BananaSplit
{
  [ContextAction(
    Name = "Split call",
    Description = "Split call",
    Group = CSharpContextActions.GroupID)]
  public class SplitCallContextAction : ContextActionBase
  {
    [NotNull] private readonly ICSharpContextActionDataProvider myProvider;
    [NotNull] private readonly CSharpElementFactory myFactory;

    public SplitCallContextAction([NotNull] ICSharpContextActionDataProvider provider)
    {
      myProvider = provider;
      myFactory = CSharpElementFactory.GetInstance(provider.PsiModule);
    }

    public override string Text => "Split call";

    protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
      var topLevelNode = myProvider.GetTopLevelNode().NotNull();
      var invocation = FindInvocationChain(topLevelNode).NotNull();

      invocation = ((IInvocationExpression) StatementUtil.EnsureStatementExpression(invocation)).NotNull();

      var declarations = new List<IDeclarationStatement>();
      SplitInvocation(invocation, declarations);
      InsertDeclarations(invocation, declarations);

      var hotspots = CreateHotspotsForNewVariables(invocation, declarations);

      return Utils.ExecuteHotspotSession(solution, hotspots);
    }

    public override bool IsAvailable(IUserDataHolder cache)
    {
      var topLevelNode = myProvider.GetTopLevelNode();
      if (topLevelNode == null) return false;

      return FindInvocationChain(topLevelNode) != null;
    }

    [CanBeNull]
    private static IInvocationExpression FindInvocationChain([NotNull] ICSharpTreeNode topLevelNode)
    {
      foreach (var invocation in topLevelNode.Descendants().OfType<IInvocationExpression>())
      {
        if (HasChainedExpressions(invocation)) return invocation;
      }

      return null;
    }

    private static bool HasChainedExpressions([NotNull] IInvocationExpression invocation) => MatchChain(invocation, 0);

    private static bool MatchChain([NotNull] IInvocationExpression invocation, int currentInvocationsCount)
    {
      while (true)
      {
        if (currentInvocationsCount + 1 >= 2) return true;

        var innerInvocation = invocation.GetInnerInvocation();
        if (innerInvocation == null) return false;

        invocation = innerInvocation;
        currentInvocationsCount = currentInvocationsCount + 1;
      }
    }

    private void SplitInvocation(
      [NotNull] IInvocationExpression invocation, [NotNull] List<IDeclarationStatement> declarations)
    {
      SplitInvocation(invocation, declarations, new JetHashSet<string>());
    }

    private void SplitInvocation(
      [NotNull] IInvocationExpression invocation,
      [NotNull] List<IDeclarationStatement> declarations,
      [NotNull] JetHashSet<string> names)
    {
      var innerInvocation = invocation.GetInnerInvocation();
      if (innerInvocation == null) return;

      SplitInvocation(innerInvocation, declarations, names);
      var identifier = AddDeclaration(innerInvocation, declarations, names);

      SetInvocationTarget(invocation, identifier);
    }

    [NotNull]
    private string AddDeclaration(
      [NotNull] IInvocationExpression expression,
      [NotNull] List<IDeclarationStatement> declarations,
      [NotNull] JetHashSet<string> names)
    {
      // TODO: Use Naming.SuggestionManager
      string variableName = Utils.NaiveSuggestVariableName(expression, names);

      var initializer = myFactory.CreateVariableInitializer(expression);
      var declaration = myFactory.CreateStatement("var $0 = $1;", variableName, initializer);
      declarations.Add((IDeclarationStatement) declaration);
      return variableName;
    }

    private void SetInvocationTarget([NotNull] IInvocationExpression invocation, [NotNull] string variableName)
    {
      var identifier = myFactory.CreateExpression("$0", variableName);
      var referenceExpression = (IReferenceExpression) invocation.InvokedExpression;
      referenceExpression.SetQualifierExpression(identifier);
    }

    private static void InsertDeclarations(
      [NotNull] IInvocationExpression invocation, [NotNull] List<IDeclarationStatement> declarations)
    {
      IBlock block = invocation.GetContainingNode<IBlock>(true).NotNull();
      ICSharpStatement anchor = invocation.GetContainingStatement();

      for (var i = declarations.Count - 1; i >= 0; i--)
      {
        declarations[i] = block.AddStatementBefore(declarations[i], anchor);
        anchor = declarations[i];
      }
    }

    [NotNull]
    private static HotspotInfo[] CreateHotspotsForNewVariables(
      [NotNull] IInvocationExpression invocation, [NotNull] List<IDeclarationStatement> declarations)
    {
      var hotspots = new HotspotInfo[declarations.Count];

      for (int i = 0; i < declarations.Count - 1; i++)
      {
        hotspots[i] = CreateHotspotForVariable(declarations[i], declarations[i + 1]);
      }

      hotspots[hotspots.Length - 1] = CreateHotspotForVariable(declarations[declarations.Count - 1], invocation);

      return hotspots;
    }

    [NotNull]
    private static HotspotInfo CreateHotspotForVariable(
      [NotNull] IDeclarationStatement current, [NotNull] IDeclarationStatement next)
    {
      var nextInvocation = (IInvocationExpression) next.VariableDeclarations[0].Initial.FirstChild.NotNull();
      return CreateHotspotForVariable(current, nextInvocation);
    }

    [NotNull]
    private static HotspotInfo CreateHotspotForVariable(
      [NotNull] IDeclarationStatement current, [NotNull] IInvocationExpression nextInvocation)
    {
      var name = current.VariableDeclarations[0].DeclaredName;
      var templateField = new TemplateField(name, new MacroCallExpressionNew(new SuggestVariableNameMacroDef()), 0);

      var first = current.VariableDeclarations[0].GetNameDocumentRange();
      var second = ((IReferenceExpression) nextInvocation.InvokedExpression).QualifierExpression.GetDocumentRange();

      return new HotspotInfo(templateField, first, second);
    }
  }
}