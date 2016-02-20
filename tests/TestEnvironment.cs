using NUnit.Framework;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.TestFramework;
using JetBrains.ReSharper.TestFramework;
using JetBrains.TestFramework.Application.Zones;
// ReSharper disable CheckNamespace

#pragma warning disable 618
[assembly: TestDataPathBase(@"tests\Data")]
#pragma warning restore 618

[ZoneDefinition]
public interface IBananaSplitTestEnvironmentZone : ITestsZone, IRequire<PsiFeatureTestZone>
{
}

[SetUpFixture]
public class ReSharperTestEnvironmentAssembly : ExtensionTestEnvironmentAssembly<IBananaSplitTestEnvironmentZone>
{
}