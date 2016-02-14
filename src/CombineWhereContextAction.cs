using System;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.DeclaredElements;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Naming.Extentions;
using JetBrains.ReSharper.Psi.Naming.Impl;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace BananaSplit
{
  [ContextAction(
    Name = "Combine Where calls",
    Description = "Combines subsequent Where calls into the one call",
    Group = CSharpContextActions.GroupID)]
  public class CombineWhereContextAction : ContextActionBase
  {
    private readonly ICSharpContextActionDataProvider myProvider;
    private readonly CSharpElementFactory myFactory;

    public CombineWhereContextAction([NotNull] ICSharpContextActionDataProvider provider)
    {
      myProvider = provider;
      myFactory = CSharpElementFactory.GetInstance(provider.PsiModule);
    }

    public override string Text => "Combine subsequent Where";

    protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
      var topLevelNode = myProvider.GetTopLevelNode().NotNull();
      var outerWhere = FindWhereChain(topLevelNode).NotNull();

      CombineWhereInvocations(outerWhere);

      return null;
    }

    public override bool IsAvailable(IUserDataHolder cache)
    {
      var topLevelNode = myProvider.GetTopLevelNode();
      if (topLevelNode == null) return false;

      return FindWhereChain(topLevelNode) != null;
    }

    [CanBeNull]
    private IInvocationExpression FindWhereChain([NotNull] ICSharpTreeNode topLevelNode)
    {
      foreach (var invocation in topLevelNode.Descendants().OfType<IInvocationExpression>())
      {
        if (MatchWhereChain(invocation, 0)) return invocation;
      }

      return null;
    }

    private static bool MatchWhereChain([NotNull] IInvocationExpression invocation, int currentInvocationsCount)
    {
      while (true)
      {
        if (currentInvocationsCount + 1 >= 2) return true;

        if (!IsWhereInvocation(invocation)) return false;

        var innerInvocation = invocation.GetInnerInvocation();
        if (innerInvocation == null) return false;

        invocation = innerInvocation;
        currentInvocationsCount = currentInvocationsCount + 1;
      }
    }

    private static bool IsWhereInvocation([NotNull] IInvocationExpression invocation)
    {
      if (!invocation.IsValid()) return false;
      if (invocation.Arguments.Count != 1) return false;

      var invokedExpression = invocation.InvokedExpression as IReferenceExpression;

      if (invokedExpression == null) return false;
      if (invokedExpression.NameIdentifier.Name != "Where") return false;
      if (invokedExpression.QualifierExpression == null) return false;

      var argument = invocation.Arguments[0];

      if (argument.Kind != ParameterKind.VALUE) return false;
      if (argument.Value is IReferenceExpression) return true;

      var lambdaExpression = argument.Value as ILambdaExpression;
      if (lambdaExpression == null) return false;
      if (lambdaExpression.BodyBlock != null) return false;
      if (lambdaExpression.ParameterDeclarations.Count != 1) return false;

      return true;
    }

    private void CombineWhereInvocations([NotNull] IInvocationExpression outerWhere)
    {
      var filterArgument = outerWhere.Arguments[0];

      var filterLambda = filterArgument.Value as ILambdaExpression;
      if (filterLambda == null)
      {
        var methodGroup = (IReferenceExpression) filterArgument.Value;
        filterLambda = (ILambdaExpression) myFactory.CreateExpression("$0 => $1($0)", "x", methodGroup);

        // TODO Suggest name for lambda parameter
        // var lambdaParameter = filterLambda.ParameterDeclarations[0].DeclaredElement;
        // var itemNames = Utils.SuggestCollectionItemName(outerWhere.InvokedExpression, lambdaParameter);
      }

      var parameter = filterLambda.ParameterDeclarations[0];
      string parameterName = parameter.DeclaredName;

      var filterExpression = filterLambda.BodyExpression;

      var currentInvocation = outerWhere;

      while (true)
      {
        currentInvocation = currentInvocation.GetInnerInvocation();

        if (currentInvocation == null) break;
        if (!IsWhereInvocation(currentInvocation)) break;

        var filter = ExtractFilter(currentInvocation, parameterName);

        filterExpression = myFactory.CreateExpression("$0 && $1", filter, filterExpression);

        outerWhere.SetInvokedExpression(currentInvocation.InvokedExpression);
      }

      filterLambda.SetBodyExpression(filterExpression);
      filterArgument.SetValue(filterLambda);
    }

    [NotNull]
    private ICSharpExpression ExtractFilter([NotNull] IInvocationExpression whereInvocation, string lambdaParameter)
    {
      var argument = whereInvocation.Arguments[0];

      var filterLambda = argument.Value as ILambdaExpression;
      if (filterLambda != null)
      {
        var parameterName = filterLambda.ParameterDeclarations[0].DeclaredName;
        var bodyExpression = filterLambda.BodyExpression;

        if (parameterName == lambdaParameter)
        {
          return bodyExpression;
        }

        var declaredParameter = filterLambda.ParameterDeclarations[0];

        var finder = declaredParameter.GetPsiServices().Finder;
        var references = finder.FindAllReferences(declaredParameter.DeclaredElement);

        if (references.Length == 0) return bodyExpression;

        var newParameter = myFactory.CreateReferenceName("$0", lambdaParameter).NameIdentifier;

        foreach (var reference in references)
        {
          var parameterUsage = (IReferenceExpression) reference.GetTreeNode();
          parameterUsage.SetNameIdentifier(newParameter);
        }

        return bodyExpression;
      }

      var methodGroup = (IReferenceExpression) argument.Value;
      return myFactory.CreateExpression("$0($1)", methodGroup, lambdaParameter);
    }
  }
}