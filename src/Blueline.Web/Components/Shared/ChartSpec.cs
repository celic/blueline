using Blueline.Core.Dtos;

namespace Blueline.Web.Components.Shared;

/// <summary>Shape handed to trend-chart.js. Kept as plain records so it serializes cleanly.</summary>
/// <param name="Markers">
/// Draws each series' own shape along its line, so the lines are told apart by more than colour.
///
/// Only worth it when the chart carries several subjects. On a single-subject chart the two series
/// are a solid line and a dashed one, which is already a non-colour distinction, and markers would
/// be noise on eighty-two points.
/// </param>
public record ChartSpec(
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartDataset> Datasets,
    string XLabel,
    string YLabel,
    bool BeginAtZero = true,
    bool Precise = false,
    IReadOnlyList<string>? Subtitles = null,
    bool TimeAxis = false,
    bool Markers = false);

/// <param name="Dates">
/// Set only for a time axis, where each series carries its own x values. On the shared category
/// axis the series are aligned by index instead and this stays null.
/// </param>
/// <param name="PointStyle">
/// A Chart.js point shape, carried so a series can be identified without seeing its colour. Also
/// what the legend draws, and what the comparison chips repeat.
/// </param>
public record ChartDataset(
    string Label,
    IReadOnlyList<double?> Data,
    string Color,
    string Kind = "line",
    bool Fill = false,
    bool Dashed = false,
    IReadOnlyList<string>? Dates = null,
    string PointStyle = "circle");

public static class TrendDatasets
{
    /// <summary>
    /// Builds one series for whichever axis is in use.
    ///
    /// The two axes need different shapes. A category axis aligns every series by position, so a
    /// shorter season is padded with nulls to keep the game numbers lined up. A time axis gives
    /// each series its own dates, where padding would be meaningless — and where the gaps that
    /// padding exists to hide are exactly what the reader wants to see.
    /// </summary>
    public static ChartDataset From(
        string label,
        IReadOnlyList<TrendPoint> points,
        Func<TrendPoint, double?> select,
        string colour,
        bool timeAxis,
        int categoryLength,
        string kind = "line",
        bool fill = false,
        string pointStyle = "circle")
    {
        var values = points.Select(select).ToList();

        if (timeAxis)
        {
            return new ChartDataset(label, values, colour, kind, fill,
                Dates: points.Select(p => p.Date.ToString("yyyy-MM-dd")).ToList(),
                PointStyle: pointStyle);
        }

        while (values.Count < categoryLength) values.Add(null);
        return new ChartDataset(label, values, colour, kind, fill, PointStyle: pointStyle);
    }
}

public static class ChartPalette
{
    /// <summary>
    /// Distinguishable at a glance and legible on the dark surface. Six is a deliberate ceiling
    /// rather than an arbitrary one: past roughly this many lines a trend chart stops being
    /// readable however good the colours are, so the comparison cap is set to match.
    /// </summary>
    public static readonly string[] Series =
        ["#38bdf8", "#f472b6", "#facc15", "#4ade80", "#c084fc", "#fb923c"];

    /// <summary>Subjects one chart can carry, the primary included.</summary>
    public static int MaxSeries => Series.Length;

    /// <summary>Comparisons that can be added alongside the primary subject.</summary>
    public const int MaxComparisons = 5;

    /// <summary>
    /// A shape per series, paired with the colour and never used instead of it.
    ///
    /// Colour alone was the only thing telling two compared players apart, which fails for the
    /// eight percent or so of men with a colour vision deficiency — and for anyone printing the
    /// page, or reading it in sunlight. The shapes are drawn on the lines, in the legend and on
    /// the chips, so the same mark identifies a subject everywhere it appears.
    /// </summary>
    private static readonly (string ChartStyle, string Glyph)[] Markers =
    [
        ("circle", "●"),
        ("triangle", "▲"),
        ("rect", "■"),
        ("rectRot", "◆"),
        ("star", "★"),
        ("crossRot", "✖"),
    ];

    public static string For(int index) => Series[index % Series.Length];

    /// <summary>The Chart.js point style for a series.</summary>
    public static string MarkerFor(int index) => Markers[index % Markers.Length].ChartStyle;

    /// <summary>The same mark as a character, for a chip or a legend rendered in HTML.</summary>
    public static string GlyphFor(int index) => Markers[index % Markers.Length].Glyph;
}
