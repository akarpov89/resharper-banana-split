using System.Collections.Generic;
using System.Linq;

public class C
{
  void M(string s)
  {
    {selstart}char[] array = s.ToArray();
    IEnumerable<char> enumerable = array.Select(x => x);
    IEnumerable<char> @where = enumerable.Where(x => true);
    IEnumerable<char> c = @where.Where(z => false);
    IEnumerable<char> @select = c.Select(r => r);
    IEnumerable<char> enumerable1 = @select.Where(Foo);
    int count = enumerable1.Count();{selend}{caret}
  }

  static bool Foo(char c) => true;
}