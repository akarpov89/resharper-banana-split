﻿using System.Linq;

public class C
{
  void M(string s)
  {
    int count = {caret}s.ToArray().Select(x => x).Where(x => true).Count();
  }
}