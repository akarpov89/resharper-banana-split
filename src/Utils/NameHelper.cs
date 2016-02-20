using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Naming.Extentions;
using JetBrains.ReSharper.Psi.Naming.Impl;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace BananaSplit
{
  internal static class NameHelper
  {
    public static string NaiveSuggestVariableName(
      [NotNull] IInvocationExpression expression, [NotNull] JetHashSet<string> names)
    {
      var methodName = ((IReferenceExpression) expression.InvokedExpression).NameIdentifier.Name.Decapitalize();

      var variableName = methodName;

      for (int i = 1; names.Contains(variableName); i++)
      {
        variableName = methodName + i.ToString();
      }

      names.Add(variableName);
      return variableName;
    }

    public static string SuggestCollectionItemName(
      [NotNull] ITreeNode collectionNameSource, [NotNull] IDeclaredElement itemNameTarget)
    {
      var psiServices = collectionNameSource.GetPsiServices();
      var suggestionManager = psiServices.Naming.Suggestion;

      var collection = suggestionManager.CreateEmptyCollection(
        PluralityKinds.Single, collectionNameSource.Language, true, collectionNameSource);

      collection.Add(collectionNameSource, new EntryOptions
      {
        SubrootPolicy = SubrootPolicy.Decompose,
        PredefinedPrefixPolicy = PredefinedPrefixPolicy.Remove,
        PluralityKind = PluralityKinds.Plural
      });

      collection.Prepare(itemNameTarget, new SuggestionOptions
      {
        UniqueNameContext = collectionNameSource.GetContainingNode<ITypeMemberDeclaration>()
      });

      return collection.FirstName();
    }
  }
}