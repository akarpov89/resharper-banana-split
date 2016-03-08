using System.Collections.Generic;
using System.Linq;

public class C
{
  void M(string s)
  {
    {selstart}char[] array = s.ToArray();
    IEnumerable<char> enumerable = array.Select(x => x);
    int count = enumerable.Count();{selend}{caret}
  }
}