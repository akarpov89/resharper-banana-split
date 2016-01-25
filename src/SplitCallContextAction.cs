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

        protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
        {
            var topLevelNode = GetTopLevelNode().NotNull();
            var invocation = TryFindInvocationChain(topLevelNode).NotNull();

            invocation = ((IInvocationExpression) StatementUtil.EnsureStatementExpression(invocation)).NotNull();

            var declarations = new List<IDeclarationStatement>();
            SplitInvocation(invocation, declarations);
            InsertDeclarations(invocation, declarations);

            var hotspots = CreateHotspotsForNewVariables(invocation, declarations);

            return Utils.ExecuteHotspotSession(solution, hotspots);
        }

        public override string Text => "Split call";

        public override bool IsAvailable(IUserDataHolder cache)
        {
            var topLevelNode = GetTopLevelNode();
            if (topLevelNode == null) return false;

            return TryFindInvocationChain(topLevelNode) != null;
        }

        [CanBeNull]
        private ICSharpTreeNode GetTopLevelNode()
        {
            var selectedElement = myProvider.GetSelectedElement<ICSharpTreeNode>();
            return StatementUtil.GetContainingStatementLike(selectedElement);
        }

        [CanBeNull]
        private IInvocationExpression TryFindInvocationChain(ICSharpTreeNode topLevelNode)
        {
            foreach (var invocation in topLevelNode.Descendants().OfType<IInvocationExpression>())
            {
                if (HasChainedExpressions(invocation)) return invocation;
            }

            return null;
        }

        private static bool HasChainedExpressions(IInvocationExpression invocation) => MatchChain(invocation, 0);

        private static bool MatchChain(IInvocationExpression invocation, int currentInvocationsCount)
        {
            if (currentInvocationsCount + 1 >= 2) return true;

            var innerInvocation = TryGetInnerInvocation(invocation);
            if (innerInvocation == null) return false;

            bool isInnerInvocationMatch = MatchChain(innerInvocation, currentInvocationsCount + 1);

            if (!isInnerInvocationMatch) return false;

            return true;
        }

        private static IInvocationExpression TryGetInnerInvocation(IInvocationExpression invocation)
        {
            var referenceExpression = invocation.InvokedExpression as IReferenceExpression;
            return referenceExpression?.QualifierExpression as IInvocationExpression;
        }

        private void SplitInvocation(IInvocationExpression invocation, List<IDeclarationStatement> declarations) =>
            SplitInvocation(invocation, declarations, new JetHashSet<string>());

        private void SplitInvocation(
            IInvocationExpression invocation, List<IDeclarationStatement> declarations, JetHashSet<string> names)
        {
            var innerInvocation = TryGetInnerInvocation(invocation);
            if (innerInvocation == null) return;

            SplitInvocation(innerInvocation, declarations, names);
            var identifier = AddDeclaration(innerInvocation, declarations, names);

            SetInvocationTarget(invocation, identifier);
        }

        private string AddDeclaration(
            IInvocationExpression expression, List<IDeclarationStatement> declarations, JetHashSet<string> names)
        {
            string variableName = Utils.NaiveSuggestVariableName(expression, names);

            var initializer = myFactory.CreateVariableInitializer(expression);
            var declaration = myFactory.CreateStatement("var $0 = $1;", variableName, initializer);
            declarations.Add((IDeclarationStatement)declaration);
            return variableName;
        }

        private void SetInvocationTarget(IInvocationExpression invocation, string variableName)
        {
            var identifier = myFactory.CreateExpression("$0", variableName);
            var referenceExpression = (IReferenceExpression) invocation.InvokedExpression;
            referenceExpression.SetQualifierExpression(identifier);
        }

        private static void InsertDeclarations(IInvocationExpression invocation, List<IDeclarationStatement> declarations)
        {
            IBlock block = invocation.GetContainingNode<IBlock>(true).NotNull();
            ICSharpStatement anchor = invocation.GetContainingStatement();

            for (var i = declarations.Count - 1; i >= 0; i--)
            {
                declarations[i] = block.AddStatementBefore(declarations[i], anchor);
                anchor = declarations[i];
            }
        }

        private static HotspotInfo[] CreateHotspotsForNewVariables(
            IInvocationExpression invocation, List<IDeclarationStatement> declarations)
        {
            var hotspots = new HotspotInfo[declarations.Count];

            for (int i = 0; i < declarations.Count - 1; i++)
            {
                hotspots[i] = CreateHotspotForVariable(declarations[i], declarations[i + 1]);
            }

            hotspots[hotspots.Length - 1] = CreateHotspotForVariable(declarations[declarations.Count - 1], invocation);

            return hotspots;
        }

        private static HotspotInfo CreateHotspotForVariable(IDeclarationStatement current, IDeclarationStatement next)
        {
            var nextInvocation = (IInvocationExpression) next.VariableDeclarations[0].Initial.FirstChild;
            return CreateHotspotForVariable(current, nextInvocation);
        }

        private static HotspotInfo CreateHotspotForVariable(IDeclarationStatement current, IInvocationExpression nextInvocation)
        {
            var name = current.VariableDeclarations[0].DeclaredName;
            var templateField = new TemplateField(name, new MacroCallExpressionNew(new SuggestVariableNameMacroDef()), 0);

            var first = current.VariableDeclarations[0].GetNameDocumentRange();
            var second = ((IReferenceExpression)nextInvocation.InvokedExpression).QualifierExpression.GetDocumentRange();

            return new HotspotInfo(templateField, first, second);
        }
    }
}