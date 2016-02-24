using System.Collections.Generic;

namespace BananaSplit
{
  public static class CollectionUtils
  {
    public static void Swap<T>(this IList<T> list, int i, int j)
    {
      T temp = list[i];
      list[i] = list[j];
      list[j] = temp;
    }
  }
}