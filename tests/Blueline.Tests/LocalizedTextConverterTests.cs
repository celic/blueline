using System.Text.Json;
using Blueline.Ingestion.Nhl;

namespace Blueline.Tests;

/// <summary>
/// The league returns the same logical field as a bare string on some endpoints and as a
/// locale object on others. Getting this wrong silently drops whole responses, so both shapes
/// are pinned down here.
/// </summary>
public class LocalizedTextConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new LocalizedTextConverter() },
    };

    private record Holder(LocalizedText? Abbrev);

    [Test]
    public void Reads_a_bare_string_as_used_by_score_and_club_schedule()
    {
        var result = JsonSerializer.Deserialize<Holder>("""{"abbrev":"DAL"}""", Options);

        Assert.That(result!.Abbrev!.Default, Is.EqualTo("DAL"));
    }

    [Test]
    public void Reads_a_locale_object_as_used_by_boxscore_and_standings()
    {
        var result = JsonSerializer.Deserialize<Holder>("""{"abbrev":{"default":"CHI"}}""", Options);

        Assert.That(result!.Abbrev!.Default, Is.EqualTo("CHI"));
    }

    [Test]
    public void Ignores_locales_other_than_default()
    {
        var json = """{"abbrev":{"default":"Toronto","fr":"de Toronto"}}""";

        var result = JsonSerializer.Deserialize<Holder>(json, Options);

        Assert.That(result!.Abbrev!.Default, Is.EqualTo("Toronto"));
    }

    [Test]
    public void Tolerates_a_locale_object_with_no_default_key()
    {
        var result = JsonSerializer.Deserialize<Holder>("""{"abbrev":{"fr":"de Toronto"}}""", Options);

        Assert.That(result!.Abbrev!.Default, Is.Empty);
    }

    [Test]
    public void Reads_null_as_null()
    {
        var result = JsonSerializer.Deserialize<Holder>("""{"abbrev":null}""", Options);

        Assert.That(result!.Abbrev, Is.Null);
    }

    [Test]
    public void Skips_nested_structures_without_failing_the_parse()
    {
        var json = """{"abbrev":{"nested":{"deep":[1,2,3]},"default":"BOS"}}""";

        var result = JsonSerializer.Deserialize<Holder>(json, Options);

        Assert.That(result!.Abbrev!.Default, Is.EqualTo("BOS"));
    }

    [Test]
    public void Survives_a_response_streamed_in_small_chunks()
    {
        // Reading from a stream is what exposed the original Skip() bug: the serializer hands
        // the converter a partial buffer, where Skip throws but TrySkip does not.
        var padding = new string('x', 40_000);
        var json = $$"""{"abbrev":{"padding":"{{padding}}","default":"VAN"} }""";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var result = JsonSerializer.Deserialize<Holder>(stream, Options);

        Assert.That(result!.Abbrev!.Default, Is.EqualTo("VAN"));
    }
}
