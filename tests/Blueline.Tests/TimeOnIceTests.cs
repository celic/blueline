using Blueline.Ingestion.Nhl;

namespace Blueline.Tests;

public class TimeOnIceTests
{
    [TestCase("21:03", 1263)]
    [TestCase("00:00", 0)]
    [TestCase("5:30", 330)]
    [TestCase("60:00", 3600)]
    public void ToSeconds_parses_the_leagues_mm_ss_format(string toi, int expected) =>
        Assert.That(TimeOnIce.ToSeconds(toi), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("nonsense")]
    [TestCase("21")]
    [TestCase("21:03:45")]
    [TestCase("aa:bb")]
    public void ToSeconds_returns_zero_rather_than_throwing_on_unusable_input(string? toi) =>
        Assert.That(TimeOnIce.ToSeconds(toi), Is.Zero);
}
