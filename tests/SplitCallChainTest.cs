﻿using JetBrains.ReSharper.FeaturesTestFramework.Intentions;
using NUnit.Framework;

namespace BananaSplit.Tests
{
  [TestFixture]
  public class SplitCallChainTest : CSharpContextActionExecuteTestBase<SplitCallChainContextAction>
  {
    protected override string ExtraPath => "SplitCallChain";
    protected override string RelativeTestDataPath => "SplitCallChain";

    [Test] public void Test01() => DoNamedTest();

    [Test] public void Test02() => DoNamedTest();

    [Test] public void Test03() => DoNamedTest();

    [Test] public void Test04() => DoNamedTest();

    [Test] public void Test05() => DoNamedTest();
  }
}