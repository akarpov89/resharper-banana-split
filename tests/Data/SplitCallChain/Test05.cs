using System.Linq;

public class C
{
  void M(string s)
  {
    int count = {caret}s.ToArray().Select(x => x).Where(x => true).Where(z => false).Select(r => r).Where(Foo).Count();
  }
  
  static bool Foo(char c) => true;
}