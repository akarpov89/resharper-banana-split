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

    public static string SuggestVariableName(
      [NotNull] ITreeNode nameSource, [NotNull] IDeclaredElement variable, [NotNull] IType variableType)
    {
      var psiServices = nameSource.GetPsiServices();
      var suggestionManager = psiServices.Naming.Suggestion;

      var collection = suggestionManager.CreateEmptyCollection(
        PluralityKinds.Unknown, nameSource.Language, true, nameSource);

      collection.Add(nameSource, new EntryOptions
      {
        SubrootPolicy = SubrootPolicy.Decompose,
        PredefinedPrefixPolicy = PredefinedPrefixPolicy.Remove,
        PluralityKind = PluralityKinds.Unknown
      });

      if (variableType.IsResolved)
      {
        collection.Add(variableType, new EntryOptions
        {
          PluralityKind = PluralityKinds.Single,
          SubrootPolicy = SubrootPolicy.Decompose
        });
      }

      collection.Prepare(variable, new SuggestionOptions
      {
        UniqueNameContext = nameSource.GetContainingNode<ITypeMemberDeclaration>()
      });

      return collection.FirstName();
    }
  }
}