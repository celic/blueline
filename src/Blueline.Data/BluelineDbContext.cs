using Blueline.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blueline.Data;

public class BluelineDbContext(DbContextOptions<BluelineDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<SkaterGameStat> SkaterGameStats => Set<SkaterGameStat>();
    public DbSet<GoalieGameStat> GoalieGameStats => Set<GoalieGameStat>();
    public DbSet<TeamGameStat> TeamGameStats => Set<TeamGameStat>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Team>(e =>
        {
            // League-assigned ids, so never generate our own.
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Abbrev).HasMaxLength(3);
            e.HasIndex(t => t.Abbrev).IsUnique();
        });

        b.Entity<Player>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Position).HasMaxLength(2);
            e.Ignore(p => p.IsGoalie);
            e.Ignore(p => p.FullName);
            e.HasIndex(p => p.LastName);
        });

        b.Entity<Game>(e =>
        {
            e.Property(g => g.Id).ValueGeneratedNever();
            e.Ignore(g => g.IsFinal);
            e.HasOne(g => g.HomeTeam).WithMany().HasForeignKey(g => g.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.AwayTeam).WithMany().HasForeignKey(g => g.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            // Every trend query filters by season then orders by date, so index them together.
            e.HasIndex(g => new { g.SeasonId, g.GameType, g.GameDate });
        });

        b.Entity<SkaterGameStat>(e =>
        {
            e.HasKey(s => new { s.GameId, s.PlayerId });
            e.HasOne(s => s.Game).WithMany(g => g.SkaterGameStats).HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Player).WithMany(p => p.SkaterGameStats).HasForeignKey(s => s.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Team).WithMany().HasForeignKey(s => s.TeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.PlayerId);
        });

        b.Entity<GoalieGameStat>(e =>
        {
            e.HasKey(s => new { s.GameId, s.PlayerId });
            e.Ignore(s => s.SavePctg);
            e.HasOne(s => s.Game).WithMany(g => g.GoalieGameStats).HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Player).WithMany(p => p.GoalieGameStats).HasForeignKey(s => s.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Team).WithMany().HasForeignKey(s => s.TeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.PlayerId);
        });

        b.Entity<TeamGameStat>(e =>
        {
            e.HasKey(s => new { s.GameId, s.TeamId });
            e.Property(s => s.Result).HasMaxLength(3);
            e.HasOne(s => s.Game).WithMany(g => g.TeamGameStats).HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Team).WithMany(t => t.GameStats).HasForeignKey(s => s.TeamId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<IngestionRun>(e =>
        {
            e.Property(r => r.Kind).HasMaxLength(32);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        });
    }
}
