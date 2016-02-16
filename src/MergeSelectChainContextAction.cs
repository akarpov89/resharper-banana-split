using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

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

    protected override void Merge(ILambdaExpression lambda, ILambdaExpression accumulatorLambda)
    {
      var declaredElement = accumulatorLambda.ParameterDeclarations[0].DeclaredElement;

      var replacement = lambda.BodyExpression;

      foreach (var referenceExpression in accumulatorLambda.BodyExpression.Descendants<IReferenceExpression>())
      {
        var currentElement = referenceExpression.Reference.Resolve().DeclaredElement;
        if (currentElement == null) continue;

        if (currentElement.Equals(declaredElement))
        {
          referenceExpression.ReplaceBy(replacement);
        }
      }
    }
  }
}