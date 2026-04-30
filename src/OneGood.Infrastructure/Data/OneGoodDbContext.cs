using Microsoft.EntityFrameworkCore;
using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Infrastructure.Data;

public class OneGoodDbContext : DbContext
{
    public OneGoodDbContext(DbContextOptions<OneGoodDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Cause> Causes => Set<Cause>();
    public DbSet<DailyAction> DailyActions => Set<DailyAction>();
    public DbSet<UserAction> UserActions => Set<UserAction>();
    public DbSet<Outcome> Outcomes => Set<Outcome>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<CauseTranslation> CauseTranslations => Set<CauseTranslation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Profile).WithOne()
             .HasForeignKey<UserProfile>(x => x.UserId);
            e.HasMany(x => x.CompletedActions).WithOne(x => x.User)
             .HasForeignKey(x => x.UserId);
            e.Property(x => x.Email).HasMaxLength(256);
        });

        builder.Entity<UserProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MaxDonationPerAction).HasPrecision(18, 2);
            e.Property(x => x.TotalDonated).HasPrecision(18, 2);
            e.Property(x => x.PreferredCategories)
             .HasConversion(
                 v => string.Join(',', v.Select(c => (int)c)),
                 v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => (CauseCategory)int.Parse(s))
                       .ToList());
        });

        builder.Entity<Cause>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Actions).WithOne(x => x.Cause)
             .HasForeignKey(x => x.CauseId);
            e.HasMany(x => x.Translations).WithOne(x => x.Cause)
             .HasForeignKey(x => x.CauseId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.FundingGoal).HasPrecision(18, 2);
            e.Property(x => x.FundingCurrent).HasPrecision(18, 2);
            e.HasIndex(x => x.UrgencyScore);
            e.HasIndex(x => x.IsActive);
        });

        builder.Entity<CauseTranslation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CauseId, x.Language }).IsUnique();
            e.Property(x => x.Language).HasMaxLength(10);
        });

        builder.Entity<DailyAction>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Outcome).WithOne(x => x.DailyAction)
             .HasForeignKey<Outcome>(x => x.DailyActionId);
            e.HasMany(x => x.UserActions).WithOne(x => x.DailyAction)
             .HasForeignKey(x => x.DailyActionId);
            e.Property(x => x.SuggestedAmount).HasPrecision(18, 2);
            e.Property(x => x.Language).HasMaxLength(10).HasDefaultValue("en");
        });

        builder.Entity<UserAction>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.CompletedAt });
            e.Property(x => x.AmountDonated).HasPrecision(18, 2);
        });

        builder.Entity<Outcome>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalDonationsRaised).HasPrecision(18, 2);
        });
    }
}
