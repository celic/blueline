using Blueline.Data;
using Blueline.Ingestion;
using Blueline.Web.Api;
using Blueline.Web.Components;
using Blueline.Web.Health;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database, read-side queries and the league API client.
builder.Services.AddBluelineCore(builder.Configuration);
builder.Services.AddBluelineDailyIngestion();
builder.Services.AddOpenApi();

// So a failed API call answers in the format its caller asked for. Without this the exception
// handler below re-executes the /Error page, and a JSON client gets a page of HTML with its 500.
builder.Services.AddProblemDetails();
builder.Services.AddBluelineHealthChecks();

// Per-call HTTP and SQL logs would drown out everything else during a backfill.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
// The resilience pipeline logs every attempt, including the successes.
builder.Logging.AddFilter("Polly", LogLevel.Warning);

// Behind a reverse proxy the original scheme and client address arrive as headers. Opt-in,
// because trusting these when nothing is in front of the app would let a caller spoof them.
var useForwardedHeaders = builder.Configuration.GetValue("Blueline:UseForwardedHeaders", false);
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // The proxy is inside the host's own network and its address is not known ahead of time,
        // which is the normal situation for a container platform.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (useForwardedHeaders) app.UseForwardedHeaders();

// Apply migrations at startup so a fresh deployment comes up with a usable schema.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BluelineDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Using database at {Path}", BluelineDbPath.DatabaseFile);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    // Registered after the page handler, and therefore inside it: whichever handler is innermost
    // sees the exception first, so an API call is answered here and never reaches the one that
    // re-executes an HTML page. With no path or handler configured this writes a ProblemDetails
    // body, which is what a caller of /api can actually read.
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/api"),
        api => api.UseExceptionHandler(new ExceptionHandlerOptions()));

    app.UseHsts();
}
else
{
    app.MapOpenApi();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapStatsApi();
app.MapBluelineHealthChecks();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
