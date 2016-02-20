using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.CSharp.Analyses.Bulbs;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;

namespace BananaSplit
{
  internal static class TreeNodeUtils
  {
    public static bool IsMethodInvoked(IInvocationExpression invocationExpression, IMethod method)
    {
      var calledMethod = invocationExpression.InvocationExpressionReference.Resolve().Result.DeclaredElement as IMethod;
      return method.Equals(calledMethod);
    }

    [CanBeNull]
    public static ICSharpTreeNode GetTopLevelNode([NotNull] this ICSharpContextActionDataProvider provider)
    {
      var selectedElement = provider.GetSelectedElement<ICSharpTreeNode>();
      return StatementUtil.GetContainingStatementLike(selectedElement);
    }

    [CanBeNull]
    public static IInvocationExpression GetInnerInvocation([NotNull] this IInvocationExpression invocation)
    {
      var referenceExpression = invocation.InvokedExpression as IReferenceExpression;
      return referenceExpression?.QualifierExpression as IInvocationExpression;
    }
  }
}