using System;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros.Implementations;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Templates;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace BananaSplit
{
  public abstract class MergeCallChainContextAction : ContextActionBase
  {
    [NotNull] private readonly ICSharpContextActionDataProvider myProvider;
    

    [CanBeNull] private IInvocationExpression myOuterInvocation;
    [CanBeNull] private ICSharpIdentifier myExisingLambdaParameterName;

    protected MergeCallChainContextAction([NotNull] ICSharpContextActionDataProvider provider)
    {
      myProvider = provider;
      Factory = CSharpElementFactory.GetInstance(provider.PsiModule);
    }

    [NotNull]
    protected CSharpElementFactory Factory { get; }

    [NotNull]
    protected abstract string ChainedMethodName { get; }

    protected sealed override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
      MergeInvocations(myOuterInvocation.NotNull());

      var hotspotInfo = CreateHostHotspotInfo();

      return Utils.ExecuteHotspotSession(solution, new[] {hotspotInfo});
    }

    public sealed override bool IsAvailable(IUserDataHolder cache)
    {
      myOuterInvocation = null;
      myExisingLambdaParameterName = null;

      var topLevelNode = myProvider.GetTopLevelNode();
      if (topLevelNode == null) return false;

      myOuterInvocation = FindChain(topLevelNode);
      return myOuterInvocation != null;
    }

    [NotNull]
    protected abstract void Merge([NotNull] ILambdaExpression lambda, [NotNull] ILambdaExpression accumulatorLambda);

    [CanBeNull]
    private IInvocationExpression FindChain([NotNull] ITreeNode topLevelNode)
    {
      foreach (var invocation in topLevelNode.Descendants().OfType<IInvocationExpression>())
      {
        if (MatchChain(invocation)) return invocation;
      }

      return null;
    }

    private bool MatchChain([NotNull] IInvocationExpression invocation)
    {
      int currentInvocationsCount = 0;

      while (true)
      {
        if (!MatchInvocation(invocation, checkValidity: true)) break;

        currentInvocationsCount = currentInvocationsCount + 1;

        var lambdaParameterName = ExtractLambdaParameterName(invocation);
        if (lambdaParameterName != null)
        {
          myExisingLambdaParameterName = lambdaParameterName;
        }

        var innerInvocation = invocation.GetInnerInvocation();
        if (innerInvocation == null) break;

        invocation = innerInvocation;
      }

      return currentInvocationsCount >= 2;
    }

    private bool MatchInvocation([NotNull] IInvocationExpression invocation, bool checkValidity)
    {
      if (checkValidity && !invocation.IsValid()) return false;
      if (invocation.Arguments.Count != 1) return false;

      var invokedExpression = invocation.InvokedExpression as IReferenceExpression;

      if (invokedExpression == null) return false;
      if (invokedExpression.NameIdentifier.Name != ChainedMethodName) return false;
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

    [CanBeNull]
    private ICSharpIdentifier ExtractLambdaParameterName([NotNull] IInvocationExpression invocation)
    {
      var argument = invocation.Arguments[0];

      var lambdaExpression = argument.Value as ILambdaExpression;
      if (lambdaExpression == null) return null;

      return lambdaExpression.ParameterDeclarations[0].NameIdentifier;
    }

    private void MergeInvocations([NotNull] IInvocationExpression outerInvocation)
    {
      // TODO Change merge direction from `outer-to-inner` to `inner-to-outer`

      var outerInvocationArgument = outerInvocation.Arguments[0];

      var accumulateLambda = outerInvocationArgument.Value as ILambdaExpression;
      if (accumulateLambda == null)
      {
        var methodGroup = (IReferenceExpression)outerInvocationArgument.Value;

        accumulateLambda = CreateLambdaFromMethodGroup(methodGroup, outerInvocation.InvokedExpression);
      }

      var parameter = accumulateLambda.ParameterDeclarations[0];

      var currentInvocation = outerInvocation;
      var invokedExpression = currentInvocation.InvokedExpression;

      while (true)
      {
        currentInvocation = currentInvocation.GetInnerInvocation();

        if (currentInvocation == null) break;

        if (!MatchInvocation(currentInvocation, checkValidity: false)) break;

        var currentLambda = ExtractLambda(currentInvocation, parameter.NameIdentifier);

        Merge(currentLambda, accumulateLambda);

        invokedExpression = currentInvocation.InvokedExpression;
      }

      outerInvocationArgument.SetValue(accumulateLambda);

      outerInvocation.SetInvokedExpression(invokedExpression);
    }

    private ILambdaExpression CreateLambdaFromMethodGroup(
      [NotNull] IReferenceExpression methodGroup, [NotNull] ITreeNode collectionNameSource)
    {
      if (myExisingLambdaParameterName != null)
      {
        return (ILambdaExpression)Factory.CreateExpression("$0 => $1($0)", myExisingLambdaParameterName, methodGroup);
      }

      var lambda = (ILambdaExpression)Factory.CreateExpression("$0 => $1($0)", "__", methodGroup);

      var lambdaParameter = lambda.ParameterDeclarations[0].DeclaredElement;
      var suggestedName = Utils.SuggestCollectionItemName(collectionNameSource, lambdaParameter);

      lambda.ParameterDeclarations[0].SetName(suggestedName);

      return lambda;
    }

    [NotNull]
    private ILambdaExpression ExtractLambda([NotNull] IInvocationExpression invocation, ICSharpIdentifier lambdaParameterName)
    {
      var argument = invocation.Arguments[0];

      var lambda = argument.Value as ILambdaExpression;
      if (lambda != null)
      {
        var parameterName = lambda.ParameterDeclarations[0].DeclaredName;
        var bodyExpression = lambda.BodyExpression;

        if (parameterName == lambdaParameterName.Name)
        {
          return lambda;
        }

        var declaredParameter = lambda.ParameterDeclarations[0];

        RenameUsages(declaredParameter.DeclaredElement, bodyExpression, lambdaParameterName);

        declaredParameter.SetNameIdentifier(lambdaParameterName);

        return lambda;
      }

      var methodGroup = (IReferenceExpression)argument.Value;

      return (ILambdaExpression) Factory.CreateExpression("$0 => $1($0)", lambdaParameterName, methodGroup);
    }

    private static void RenameUsages(
      [NotNull] IDeclaredElement declaredElement, [NotNull] ITreeNode scope, [NotNull] ICSharpIdentifier name)
    {
      foreach (var referenceExpression in scope.Descendants<IReferenceExpression>())
      {
        var currentElement = referenceExpression.Reference.Resolve().DeclaredElement;
        if (currentElement == null) continue;

        if (currentElement.Equals(declaredElement))
        {
          referenceExpression.SetNameIdentifier(name);
        }
      }
    }

    private HotspotInfo CreateHostHotspotInfo()
    {
      var lambda = (ILambdaExpression)myOuterInvocation.NotNull().Arguments[0].Value;
      var lambdaParameter = lambda.ParameterDeclarations[0];

      var name = lambdaParameter.DeclaredName;
      var templateField = new TemplateField(name, new MacroCallExpressionNew(new SuggestVariableNameMacroDef()), 0);

      var documentRange = lambdaParameter.GetNameDocumentRange();

      var documentRanges = new LocalList<DocumentRange>();
      documentRanges.Add(documentRange);

      CollectUsageRanges(lambdaParameter.DeclaredElement, lambda.BodyExpression, ref documentRanges);

      return new HotspotInfo(templateField, documentRanges.ToArray());
    }

    private static void CollectUsageRanges([NotNull] IDeclaredElement declaredElement, [NotNull] ITreeNode scope, ref LocalList<DocumentRange> ranges)
    {
      foreach (var referenceExpression in scope.Descendants<IReferenceExpression>())
      {
        var currentElement = referenceExpression.Reference.Resolve().DeclaredElement;
        if (currentElement == null) continue;

        if (currentElement.Equals(declaredElement))
        {
          var range = referenceExpression.GetDocumentRange();
          ranges.Add(range);
        }
      }
    }
  }
}