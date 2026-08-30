namespace Blueline.Web.Components.Shared;

/// <summary>Shape handed to trend-chart.js. Kept as plain records so it serializes cleanly.</summary>
public record ChartSpec(
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartDataset> Datasets,
    string XLabel,
    string YLabel,
    bool BeginAtZero = true,
    bool Precise = false,
    IReadOnlyList<string>? Subtitles = null);

public record ChartDataset(
    string Label,
    IReadOnlyList<double?> Data,
    string Color,
    string Kind = "line",
    bool Fill = false,
    bool Dashed = false);

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

    public static string For(int index) => Series[index % Series.Length];
}
