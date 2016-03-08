using JetBrains.ReSharper.FeaturesTestFramework.Intentions;
using NUnit.Framework;

namespace BananaSplit.Tests
{

  [TestFixture]
  public class InlineCallsTest : CSharpContextActionExecuteTestBase<InlineCallsContextAction>
  {
    protected override string ExtraPath => "InlineCalls";
    protected override string RelativeTestDataPath => "InlineCalls";

    [Test] public void Test01() => DoNamedTest();

    [Test] public void Test02() => DoNamedTest();

    [Test] public void Test03() => DoNamedTest();

    [Test] public void Test04() => DoNamedTest();

    [Test] public void Test05() => DoNamedTest();
  }

}