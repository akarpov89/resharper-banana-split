using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
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
      var variableNameSuggestions = new LocalList<IList<string>>(myInvocationsCount - 1);

      SplitInvocation(invocation, ref declarations, ref variableNameSuggestions);
      InsertDeclarations(invocation, ref declarations);

      var hotspots = CreateHotspots(invocation, ref declarations, ref variableNameSuggestions);

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
      ref LocalList<IDeclarationStatement> declarations,
      ref LocalList<IList<string>> variableNameSuggestions)
    {
      var innerInvocation = invocation.GetInnerInvocation();
      if (innerInvocation == null) return;

      var returnType = innerInvocation.Type();

      SplitInvocation(innerInvocation, ref declarations, ref variableNameSuggestions);
      var identifier = AddDeclaration(innerInvocation, returnType, ref declarations, ref variableNameSuggestions);

      SetInvocationTarget(invocation, identifier);
    }

    [NotNull]
    private string AddDeclaration(
      [NotNull] IInvocationExpression invocation,
      [NotNull] IType variableType,
      ref LocalList<IDeclarationStatement> declarations,
      ref LocalList<IList<string>> variableNameSuggestions)
    {
      var declaration = (IDeclarationStatement)myFactory.CreateStatement("$0 $1 = $2;", variableType, "__", invocation);

      var variable = declaration.VariableDeclarations[0];

      var variableNames = NameHelper.SuggestVariableNames(invocation, variable.DeclaredElement, variableType);

      NameHelper.EnsureFirstSuggestionIsUnique(variableNames, ref variableNameSuggestions);

      variableNameSuggestions.Add(variableNames);

      variable.SetName(variableNames[0]);

      declarations.Add(declaration);

      return variableNames[0];
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
    private static HotspotInfo[] CreateHotspots(
      [NotNull] IInvocationExpression invocation, 
      ref LocalList<IDeclarationStatement> declarations,
      ref LocalList<IList<string>> variableNameSuggestions)
    {
      bool isTypeInferenceSupported = invocation.IsCSharp3Supported();

      if (isTypeInferenceSupported)
      {
        var hotspots = new HotspotInfo[declarations.Count * 2];

        for (int i = 0; i < declarations.Count - 1; i++)
        {
          hotspots[2 * i] = CreateVariableTypeHotspot(declarations[i]);
          hotspots[2 * i + 1] = CreateVariableNameHotspot(declarations[i], declarations[i + 1], variableNameSuggestions[i]);
        }

        var lastDeclaration = declarations[declarations.Count - 1];
        var lastSuggestions = variableNameSuggestions[variableNameSuggestions.Count - 1];

        hotspots[hotspots.Length - 2] = CreateVariableTypeHotspot(lastDeclaration);
        hotspots[hotspots.Length - 1] = CreateVariableNameHotspot(lastDeclaration, invocation, lastSuggestions);

        return hotspots;
      }
      else
      {
        var hotspots = new HotspotInfo[declarations.Count];

        for (int i = 0; i < hotspots.Length - 1; i++)
        {
          hotspots[i] = CreateVariableNameHotspot(declarations[i], declarations[i + 1], variableNameSuggestions[i]);
        }

        var lastDeclaration = declarations[declarations.Count - 1];
        var lastSuggestions = variableNameSuggestions[variableNameSuggestions.Count - 1];

        hotspots[hotspots.Length - 1] = CreateVariableNameHotspot(lastDeclaration, invocation, lastSuggestions);

        return hotspots;
      }
    }

    [NotNull]
    private static HotspotInfo CreateVariableTypeHotspot([NotNull] IDeclarationStatement declaration)
    {
      var variableDeclaration = declaration.VariableDeclarations[0];
      var variableType = variableDeclaration.TypeUsage;

      var typeText = variableType.GetText();
      var typeRange = variableType.GetDocumentRange();

      var uniqueFieldName = typeRange.TextRange.StartOffset.ToString();

      var templateField = new TemplateField(uniqueFieldName, new NameSuggestionsExpression(new[] {typeText, "var"}), 0);

      return new HotspotInfo(templateField, typeRange);
    }

    [NotNull]
    private static HotspotInfo CreateVariableNameHotspot(
      [NotNull] IDeclarationStatement current,
      [NotNull] IDeclarationStatement next,
      [NotNull] IList<string> nameSuggestions)
    {
      var nextInvocation = (IInvocationExpression) next.VariableDeclarations[0].Initial.FirstChild.NotNull();
      return CreateVariableNameHotspot(current, nextInvocation, nameSuggestions);
    }

    [NotNull]
    private static HotspotInfo CreateVariableNameHotspot(
      [NotNull] IDeclarationStatement current, 
      [NotNull] IInvocationExpression nextInvocation,
      [NotNull] IList<string> nameSuggestions)
    {
      var name = current.VariableDeclarations[0].DeclaredName;
      var templateField = new TemplateField(name, new NameSuggestionsExpression(nameSuggestions), 0);

      var first = current.VariableDeclarations[0].GetNameDocumentRange();
      var second = ((IReferenceExpression) nextInvocation.InvokedExpression).QualifierExpression.GetDocumentRange();

      return new HotspotInfo(templateField, first, second);
    }
  }
}