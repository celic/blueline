using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Blueline.UiTests;

/// <summary>
/// Shared base for the browser tests: navigates relative to the running site and collects console
/// errors, so a page that renders but throws underneath still fails the test.
/// </summary>
public abstract class BluelinePageTest : PageTest
{
    private readonly List<string> _consoleErrors = [];

    [SetUp]
    public void WatchConsole()
    {
        _consoleErrors.Clear();

        Page.Console += (_, message) =>
        {
            if (message.Type == "error") _consoleErrors.Add(message.Text);
        };

        Page.PageError += (_, error) => _consoleErrors.Add(error);
    }

    /// <summary>Loads a page and waits for Blazor to take over, not merely for the HTML to arrive.</summary>
    protected async Task GoToAsync(string path)
    {
        await Page.GotoAsync($"{BluelineAppFixture.BaseUrl}{path}");
        await WaitForCircuitAsync();
    }

    /// <summary>
    /// Waits for the Blazor runtime to load. Blazor Server serves pre-rendered HTML first and
    /// connects afterwards, so navigating alone proves nothing about interactivity.
    ///
    /// This does not attempt to detect the exact moment the circuit opens — there is no supported
    /// flag for it. Interactive assertions instead go through <c>Expect</c>, which polls, so a
    /// control that is not yet wired simply keeps failing until it is or the test times out.
    /// </summary>
    protected async Task WaitForCircuitAsync() =>
        await Page.WaitForFunctionAsync("() => window.Blazor !== undefined");

    /// <summary>Reads the live Chart.js instance, since a canvas offers no DOM to assert against.</summary>
    protected async Task<T> ReadChartAsync<T>(string expression) =>
        await Page.EvaluateAsync<T>($"() => {{ const chart = Object.values(Chart.instances)[0]; return {expression}; }}");

    protected async Task WaitForChartAsync() =>
        await WaitForChartAsync("chart.data.datasets.length > 0");

    /// <summary>
    /// Polls a condition against the live chart, tolerating the moment when there is not one.
    ///
    /// Changing a control rebuilds the chart component, so between the old chart being destroyed
    /// and the new one being created there is no instance at all. A predicate that dereferences
    /// it directly throws during that window instead of simply not matching yet, which turns a
    /// normal repaint into a flaky failure.
    /// </summary>
    protected async Task WaitForChartAsync(string predicate) =>
        await Page.WaitForFunctionAsync(
            $"() => {{ if (!window.Chart) return false; " +
            $"const chart = Object.values(Chart.instances)[0]; " +
            $"return !!chart && ({predicate}); }}");

    protected void AssertNoConsoleErrors()
    {
        Assert.That(_consoleErrors, Is.Empty,
            $"the browser reported errors:{Environment.NewLine}{string.Join(Environment.NewLine, _consoleErrors)}");
    }
}
