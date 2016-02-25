using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
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

    [NotNull]
    public static IList<string> SuggestVariableNames(
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

      return collection.AllNames();
    }

    public static void EnsureFirstSuggestionIsUnique(
      [NotNull] IList<string> suggestions, ref LocalList<IList<string>> previousSuggestions)
    {
      if (previousSuggestions.Count == 0) return;

      int uniqueIndex = FindIndexOfFirstUniqueSuggestion(suggestions, ref previousSuggestions);

      if (uniqueIndex == 0) return;

      if (uniqueIndex > 0)
      {
        suggestions.Swap(0, uniqueIndex);
        return;
      }

      MakeFirstSuggestionUniqueWithNumericSuffix(suggestions, ref previousSuggestions);
    }

    private static int FindIndexOfFirstUniqueSuggestion(
      [NotNull] IList<string> suggestions, ref LocalList<IList<string>> previousSuggestions)
    {
      for (int index = 0; index < suggestions.Count; index++)
      {
        string currentSuggestion = suggestions[index];

        bool isUnique = !previousSuggestions.Any(previous => previous[0] == currentSuggestion);

        if (isUnique) return index;
      }

      return -1;
    }

    private static void MakeFirstSuggestionUniqueWithNumericSuffix(
      [NotNull] IList<string> suggestions, ref LocalList<IList<string>> previousSuggestions)
    {
      string originalSuggestion = suggestions[0];

      for (int counter = 1;; counter++)
      {
        string suggestion = originalSuggestion + counter.ToString();

        bool isUnique = !previousSuggestions.Any(previous => previous[0] == suggestion);

        if (isUnique)
        {
          suggestions[0] = suggestion;
          return;
        }
      }
    }
  }
}