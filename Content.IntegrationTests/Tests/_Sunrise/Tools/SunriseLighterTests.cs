using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.FixedPoint;
using Content.Shared.Tools.Systems;

namespace Content.IntegrationTests.Tests._Sunrise.Tools;

[TestFixture]
[TestOf(typeof(SharedToolSystem))]
public sealed class SunriseLighterTests : GameTest
{
    private const string LighterPrototype = "FlippoLighterSunriseBlack";

    [SidedDependency(Side.Server)] private readonly SharedToolSystem _tool = default!;

    [Test]
    public async Task LighterStartsFueled()
    {
        var fuel = FixedPoint2.Zero;
        var capacity = FixedPoint2.Zero;

        await Server.WaitPost(() =>
        {
            var lighter = SSpawn(LighterPrototype);
            (fuel, capacity) = _tool.GetWelderFuelAndCapacity(lighter);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(fuel, Is.EqualTo(FixedPoint2.New(12)));
            Assert.That(capacity, Is.EqualTo(FixedPoint2.New(12)));
        });
    }
}
