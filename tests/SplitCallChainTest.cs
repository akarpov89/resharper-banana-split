using JetBrains.ReSharper.FeaturesTestFramework.Intentions;
using NUnit.Framework;

namespace BananaSplit.Tests
{
  [TestFixture]
  public class SplitCallChainTest : CSharpContextActionExecuteTestBase<SplitCallChainContextAction>
  {
    protected override string ExtraPath => "SplitCallChain";
    protected override string RelativeTestDataPath => "SplitCallChain";

    [Test] public void Test01() => DoNamedTest();
  }
}