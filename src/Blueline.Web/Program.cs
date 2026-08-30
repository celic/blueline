using Blueline.Data;
using Blueline.Ingestion;
using Blueline.Web.Api;
using Blueline.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database, read-side queries and the league API client.
builder.Services.AddBluelineCore(builder.Configuration);
builder.Services.AddBluelineDailyIngestion();
builder.Services.AddOpenApi();

// Per-call HTTP and SQL logs would drown out everything else during a backfill.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
// The resilience pipeline logs every attempt, including the successes.
builder.Logging.AddFilter("Polly", LogLevel.Warning);

var app = builder.Build();

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
