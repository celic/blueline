namespace Blueline.Core.Dtos;

/// <summary>
/// One subject's recent run, measured against what that subject normally does.
/// </summary>
/// <param name="GamesInWindow">
/// How many games actually fell inside the window. Fixed for a games window; for a days window it
/// is the interesting part — eight games in a fortnight is a different fortnight from four.
/// </param>
/// <param name="Total">
/// What the window adds up to. For a rate this is the rate over the whole window, weighted by its
/// denominator rather than averaged across appearances.
/// </param>
/// <param name="Baseline">
/// The subject's season figure per game, which is what the window is judged against. It includes
/// the window itself: excluding it would leave a player whose only production came in the window
/// with a baseline of zero, and nothing sensible to divide by.
/// </param>
/// <param name="Lift">
/// How far the window departs from the baseline — a multiple for a counting stat, so 3.2 means
/// three times the usual rate, and a difference for a rate, where .015 means fifteen points of
/// save percentage above normal. The board says which it is.
/// </param>
/// <param name="Recent">
/// The window's own per-game figures, oldest first, for drawing the shape of the run.
///
/// Carried on the leader rather than fetched per subject: the rows were already in hand when the
/// board was built, and asking again would turn one query per panel into one per player on it.
/// </param>
public record StreakLeader(
    int SubjectId,
    string SubjectName,
    string? TeamAbbrev,
    string? HeadshotUrl,
    int GamesInWindow,
    double Total,
    double PerGame,
    double Baseline,
    double Lift,
    IReadOnlyList<double> Recent);

/// <summary>
/// A ranked set of runs for one stat over one window — the unit a dashboard panel shows.
/// </summary>
/// <param name="AsOf">
/// The last day of hockey the window ends on, which is the newest game stored rather than today.
/// In the off-season these are the closing weeks of the last season played, and a page showing
/// them has to say so rather than presenting them as current form.
/// </param>
/// <param name="IsRate">
/// Decides how <see cref="StreakLeader.Lift"/> reads: a multiple for counting stats, a difference
/// for rates.
/// </param>
/// <param name="Considered">
/// How many subjects held a full window before any of the floors were applied.
///
/// It is what separates the two ways a board can come back empty, which read identically on the
/// page and mean opposite things: nobody stood out this week, or the season is too young for
/// anyone to have played the window at all.
/// </param>
public record StreakBoard(
    string Stat,
    string StatLabel,
    int Window,
    WindowUnit WindowUnit,
    DateOnly AsOf,
    bool IsRate,
    IReadOnlyList<StreakLeader> Leaders,
    int Considered = 0)
{
    /// <summary>True when the window is longer than anything the season has yet produced.</summary>
    public bool TooEarly => Considered == 0;

    /// <summary>Adjectival, for a panel heading: "points, last 14 days".</summary>
    public string WindowLabel => WindowUnit == WindowUnit.Days ? $"last {Window} days" : $"last {Window} games";
}
