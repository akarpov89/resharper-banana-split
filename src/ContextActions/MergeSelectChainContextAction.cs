using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace BananaSplit
{
  [ContextAction(
    Name = "Merge Select calls",
    Description = "Merges subsequent Select calls into the one call",
    Group = CSharpContextActions.GroupID)]
  public class MergeSelectChainContextAction : MergeCallChainContextAction
  {
    public MergeSelectChainContextAction([NotNull] ICSharpContextActionDataProvider provider)
      : base(provider)
    {
    }

    public override string Text => "Merge subsequent Select";

    protected override string ChainedMethodName => "Select";

    protected override void Merge(ILambdaExpression accumulatorLambda, ILambdaExpression lambda)
    {
      var declaredElement = lambda.ParameterDeclarations[0].DeclaredElement;

      var replacement = accumulatorLambda.BodyExpression;

      var toReplace = new LocalList<IReferenceExpression>();

      foreach (var referenceExpression in lambda.BodyExpression.Descendants<IReferenceExpression>())
      {
        var currentElement = referenceExpression.Reference.Resolve().DeclaredElement;

        if (declaredElement.Equals(currentElement) ||
            referenceExpression.QualifierExpression == null && 
            referenceExpression.NameIdentifier.Name == declaredElement.ShortName)
        {
          toReplace.Add(referenceExpression);
        }
      }

      for (int i = 0; i < toReplace.Count; i++)
      {
        toReplace[i].ReplaceBy(replacement);
      }

      accumulatorLambda.SetBodyExpression(lambda.BodyExpression);
    }
  }
}