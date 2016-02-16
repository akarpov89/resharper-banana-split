using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.ContextActions;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace BananaSplit
{
  [ContextAction(
    Name = "Merge Where calls",
    Description = "Merges subsequent Where calls into the one call",
    Group = CSharpContextActions.GroupID)]
  public sealed class MergeWhereChainContextAction : MergeCallChainContextAction
  {
    public MergeWhereChainContextAction([NotNull] ICSharpContextActionDataProvider provider)
      : base(provider)
    {
    }

    public override string Text => "Merge subsequent Where";

    protected override string ChainedMethodName => "Where";

    protected override void Merge(ILambdaExpression accumulatorLambda, ILambdaExpression lambda)
    {
      var newBody = Factory.CreateExpression("$0 && $1", accumulatorLambda.BodyExpression, lambda.BodyExpression);
      accumulatorLambda.SetBodyExpression(newBody);
    }
  }
}