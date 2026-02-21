using Content.Shared.Psychiatry;
using NUnit.Framework;

namespace Content.Tests.Shared.Psychiatry;

[TestFixture]
public sealed class SchizophreniaRulesTests : ContentUnitTest
{
    [TestCase(0f, SchizophreniaStage.Remission)]
    [TestCase(19.99f, SchizophreniaStage.Remission)]
    [TestCase(20f, SchizophreniaStage.Prodromal)]
    [TestCase(44.99f, SchizophreniaStage.Prodromal)]
    [TestCase(45f, SchizophreniaStage.Active)]
    [TestCase(74.99f, SchizophreniaStage.Active)]
    [TestCase(75f, SchizophreniaStage.Break)]
    [TestCase(100f, SchizophreniaStage.Break)]
    public void SeverityThresholdsMapToExpectedStages(float severity, SchizophreniaStage expectedStage)
    {
        Assert.That(SchizophreniaRules.SeverityToStage(severity), Is.EqualTo(expectedStage));
    }

    [TestCase(SchizophreniaStage.Remission, "Protocol-RM0")]
    [TestCase(SchizophreniaStage.Prodromal, "Protocol-PR1")]
    [TestCase(SchizophreniaStage.Active, "Protocol-ACT2")]
    [TestCase(SchizophreniaStage.Break, "Protocol-BRK4")]
    public void StageToProtocolReturnsExpectedCode(SchizophreniaStage stage, string expectedProtocol)
    {
        Assert.That(SchizophreniaRules.StageToProtocol(stage), Is.EqualTo(expectedProtocol));
    }
}
