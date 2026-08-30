using System.Text.Json.Serialization;

namespace Blueline.Core.Dtos;

/// <summary>What a rolling window is measured in.</summary>
/// <remarks>
/// Serialised by name. The default would put a bare <c>1</c> in the API response, which tells a
/// reader nothing and silently changes meaning if the members are ever reordered.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<WindowUnit>))]
public enum WindowUnit
{
    /// <summary>The last N games a subject actually played, however long that took.</summary>
    Games,

    /// <summary>The last N days of the calendar, however many games fell inside them.</summary>
    Days,
}

/// <summary>
/// How far back a rolling average looks.
///
/// Both units are kept because they answer different questions, and neither substitutes for the
/// other. "Last 10 games" is the right question for per-game pace and for comparing players who
/// have played different numbers of games. "Last 14 days" is the right question for who is hot
/// right now — and it is the only one that can be asked of goalies fairly, since two weeks is
/// four starts for one and eight for another.
///
/// A games window ignores the calendar entirely: across a 21-day injury layoff, a 10-game average
/// still spans ten games, so a chart on the date axis shows a line whose width bears no relation
/// to the time it covers.
/// </summary>
public readonly record struct RollingWindow(int Size, WindowUnit Unit)
{
    /// <summary>Half a season. Past this a "rolling" average is barely distinguishable from the cumulative one.</summary>
    public const int MaxGames = 41;

    /// <summary>Roughly the same span in calendar terms, an 82-game season running about six months.</summary>
    public const int MaxDays = 90;

    public static RollingWindow Default => Games(10);

    public static RollingWindow Games(int size) => new(Math.Clamp(size, 1, MaxGames), WindowUnit.Games);

    public static RollingWindow Days(int size) => new(Math.Clamp(size, 1, MaxDays), WindowUnit.Days);

    /// <summary>
    /// A bare number is games, which is what every caller meant before days existed. The
    /// conversion keeps those call sites reading naturally rather than restating the unit.
    /// </summary>
    public static implicit operator RollingWindow(int games) => Games(games);

    /// <summary>
    /// Parses <c>10</c>, <c>10g</c> or <c>14d</c>. Anything unrecognised falls back to the
    /// default rather than erroring, so a stale bookmark still renders — the same bargain
    /// <see cref="GameScopes.Parse"/> makes.
    /// </summary>
    public static RollingWindow Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;

        var text = value.Trim();
        var unit = char.ToLowerInvariant(text[^1]) switch
        {
            'd' => WindowUnit.Days,
            'g' => WindowUnit.Games,
            _ => (WindowUnit?)null,
        };

        var digits = unit is null ? text : text[..^1];

        return int.TryParse(digits, out var size) && size > 0
            ? unit == WindowUnit.Days ? Days(size) : Games(size)
            : Default;
    }

    /// <summary>Round-trips through <see cref="Parse"/>: what a URL or a select option carries.</summary>
    public string Token => Unit == WindowUnit.Days ? $"{Size}d" : $"{Size}g";

    /// <summary>Adjectival, for "a 14-day average" or "best 10-game stretch".</summary>
    public string Label => Unit == WindowUnit.Days ? $"{Size}-day" : $"{Size}-game";

    /// <summary>Standalone, for a dropdown option.</summary>
    public string OptionLabel => Unit == WindowUnit.Days ? $"{Size} days" : $"{Size} games";

    /// <summary>What each control offers. Days start at a week, below which most subjects have one game.</summary>
    public static readonly RollingWindow[] Choices =
    [
        Games(5), Games(10), Games(15), Games(20),
        Days(7), Days(14), Days(30),
    ];
}
