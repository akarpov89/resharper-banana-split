using System;
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
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace BananaSplit
{
  [ContextAction(
    Name = "Split call chain",
    Description = "Split call chain",
    Group = CSharpContextActions.GroupID)]
  public class SplitCallChainContextAction : ContextActionBase
  {
    [NotNull] private readonly ICSharpContextActionDataProvider myProvider;
    [NotNull] private readonly CSharpElementFactory myFactory;

    [CanBeNull] private IInvocationExpression myChainedInvocation;
    private int myInvocationsCount;

    public SplitCallChainContextAction([NotNull] ICSharpContextActionDataProvider provider)
    {
      myProvider = provider;
      myFactory = CSharpElementFactory.GetInstance(provider.PsiModule);
    }

    public override string Text => "Split call chain";

    protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
      var invocation = myChainedInvocation.NotNull();

      invocation = ((IInvocationExpression) StatementUtil.EnsureStatementExpression(invocation)).NotNull();

      var declarations = new LocalList<IDeclarationStatement>(myInvocationsCount - 1);
      SplitInvocation(invocation, ref declarations);
      InsertDeclarations(invocation, ref declarations);

      var hotspots = CreateHotspotsForNewVariables(invocation, ref declarations);

      var invocationLine = DocumentHelper.GetNodeEndLine(invocation, myProvider.Document);

      Action<ITextControl> onFinish = textControl => textControl.MoveCaretToEndOfLine(invocationLine);

      return HotspotHelper.ExecuteHotspotSession(solution, hotspots, onFinish);
    }

    public override bool IsAvailable(IUserDataHolder cache)
    {
      var topLevelNode = myProvider.GetTopLevelNode();
      if (topLevelNode == null) return false;

      myChainedInvocation = FindInvocationChain(topLevelNode);

      return myChainedInvocation != null;
    }

    [CanBeNull]
    private IInvocationExpression FindInvocationChain([NotNull] ICSharpTreeNode topLevelNode)
    {
      foreach (var invocation in topLevelNode.Descendants().OfType<IInvocationExpression>())
      {
        if (MatchChain(invocation)) return invocation;
      }

      return null;
    }

    private bool MatchChain([NotNull] IInvocationExpression invocation)
    {
      myInvocationsCount = 1;

      while (true)
      {
        var innerInvocation = invocation.GetInnerInvocation();
        if (innerInvocation == null) break;

        invocation = innerInvocation;
        myInvocationsCount = myInvocationsCount + 1;
      }

      return myInvocationsCount >= 2;
    }

    private void SplitInvocation(
      [NotNull] IInvocationExpression invocation,
      ref LocalList<IDeclarationStatement> declarations)
    {
      var innerInvocation = invocation.GetInnerInvocation();
      if (innerInvocation == null) return;

      var returnType = innerInvocation.Type();

      SplitInvocation(innerInvocation, ref declarations);
      var identifier = AddDeclaration(innerInvocation, returnType, ref declarations);

      SetInvocationTarget(invocation, identifier);
    }

    [NotNull]
    private string AddDeclaration(
      [NotNull] IInvocationExpression invocation,
      [NotNull] IType variableType,
      ref LocalList<IDeclarationStatement> declarations)
    {
      string variableName = "__";

      var initializer = myFactory.CreateVariableInitializer(invocation);
      var declaration = (IDeclarationStatement) myFactory.CreateStatement("var $0 = $1;", variableName, initializer);

      var variable = declaration.VariableDeclarations[0];

      variableName = NameHelper.SuggestVariableName(invocation, variable.DeclaredElement, variableType);
      variable.SetName(variableName);

      declarations.Add(declaration);

      return variableName;
    }

    private void SetInvocationTarget([NotNull] IInvocationExpression invocation, [NotNull] string variableName)
    {
      var identifier = myFactory.CreateExpression("$0", variableName);
      var referenceExpression = (IReferenceExpression) invocation.InvokedExpression;
      referenceExpression.SetQualifierExpression(identifier);
    }

    private static void InsertDeclarations(
      [NotNull] IInvocationExpression invocation, ref LocalList<IDeclarationStatement> declarations)
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
      [NotNull] IInvocationExpression invocation, ref LocalList<IDeclarationStatement> declarations)
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