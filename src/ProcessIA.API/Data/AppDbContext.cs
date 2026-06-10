using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProcessIA.API.Models;

namespace ProcessIA.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<LegalProcess> Processes => Set<LegalProcess>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<LegalProcess>(e =>
        {
            e.HasOne(p => p.User)
             .WithMany(u => u.Processes)
             .HasForeignKey(p => p.UserId);

            e.HasOne(p => p.Report)
             .WithOne(r => r.Process)
             .HasForeignKey<Report>(r => r.ProcessId);

            e.Property(p => p.Status).HasConversion<string>();
        });

        builder.Entity<Report>(e =>
        {
            e.Property(r => r.Parties).HasColumnType("jsonb");
            e.Property(r => r.NextDeadlines).HasColumnType("jsonb");
            e.Property(r => r.RiskLevel).HasConversion<string>();
        });

        builder.Entity<User>(e =>
        {
            e.Property(u => u.SubscriptionStatus).HasConversion<string>();
        });
    }
}
