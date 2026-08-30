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
        await Page.WaitForFunctionAsync(
            "() => window.Chart && Object.values(Chart.instances).length > 0 && Object.values(Chart.instances)[0].data.datasets.length > 0");

    protected void AssertNoConsoleErrors()
    {
        Assert.That(_consoleErrors, Is.Empty,
            $"the browser reported errors:{Environment.NewLine}{string.Join(Environment.NewLine, _consoleErrors)}");
    }
}
