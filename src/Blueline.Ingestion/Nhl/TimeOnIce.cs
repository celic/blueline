namespace Blueline.Ingestion.Nhl;

public static class TimeOnIce
{
    /// <summary>Converts the league's "MM:SS" time-on-ice strings to seconds. Unparseable input is zero.</summary>
    public static int ToSeconds(string? toi)
    {
        if (string.IsNullOrWhiteSpace(toi)) return 0;

        var parts = toi.Split(':');
        if (parts.Length != 2) return 0;
        if (!int.TryParse(parts[0], out var minutes)) return 0;
        if (!int.TryParse(parts[1], out var seconds)) return 0;

        return minutes * 60 + seconds;
    }
}
