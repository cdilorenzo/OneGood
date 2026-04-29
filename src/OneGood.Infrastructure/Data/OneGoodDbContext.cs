using Microsoft.EntityFrameworkCore;
using OneGood.Core.Enums;
using OneGood.Core.Models;

namespace OneGood.Infrastructure.Data;

public class OneGoodDbContext : DbContext
{
    public OneGoodDbContext(DbContextOptions<OneGoodDbContext> options)
        : base(options) { }

    /// <summary>
    /// Creates the database if needed and applies any schema updates.
    /// Call this once at startup from any host (API, Worker, etc.).
    /// </summary>
    public async Task InitializeAsync()
    {
        await Database.EnsureCreatedAsync();

        var isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        if (isPostgres)
        {
            // PostgreSQL supports IF NOT EXISTS on ALTER TABLE
            await Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "CauseTranslations" (
                    "Id" uuid NOT NULL,
                    "CauseId" uuid NOT NULL,
                    "Language" character varying(10) NOT NULL,
                    "Summary" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_CauseTranslations" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_CauseTranslations_Causes_CauseId" FOREIGN KEY ("CauseId") REFERENCES "Causes" ("Id") ON DELETE CASCADE
                )
                """);
            await Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_CauseTranslations_CauseId_Language"
                ON "CauseTranslations" ("CauseId", "Language")
                """);
            await Database.ExecuteSqlRawAsync("""
                ALTER TABLE "DailyActions" ADD COLUMN IF NOT EXISTS "IsAiGenerated" boolean NOT NULL DEFAULT false
                """);
            await Database.ExecuteSqlRawAsync("""
                ALTER TABLE "DailyActions" ADD COLUMN IF NOT EXISTS "Language" character varying(10) NOT NULL DEFAULT 'en'
                """);
        }
        // SQLite and other providers: tables are created by EnsureCreatedAsync on a fresh DB.
        // On an existing DB, check and add missing columns manually.
        else
        {
            // Check if IsAiGenerated column exists
            var conn = Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('DailyActions')";
                using var reader = await cmd.ExecuteReaderAsync();
                var hasColumn = false;
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1) == "IsAiGenerated") { hasColumn = true; break; }
                }
                if (!hasColumn)
                {
                    await Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "DailyActions" ADD COLUMN "IsAiGenerated" integer NOT NULL DEFAULT 0""");
                }

                // Check if Language column exists
                cmd.CommandText = "PRAGMA table_info('DailyActions')";
                using var reader2 = await cmd.ExecuteReaderAsync();
                var hasLangColumn = false;
                while (await reader2.ReadAsync())
                {
                    if (reader2.GetString(1) == "Language") { hasLangColumn = true; break; }
                }
                if (!hasLangColumn)
                {
                    await Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "DailyActions" ADD COLUMN "Language" TEXT NOT NULL DEFAULT 'en'""");
                }
            }
            finally
            {
                await conn.CloseAsync();
            }

            // CauseTranslations — SQLite supports CREATE TABLE IF NOT EXISTS
            await Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "CauseTranslations" (
                    "Id" TEXT NOT NULL,
                    "CauseId" TEXT NOT NULL,
                    "Language" TEXT NOT NULL,
                    "Summary" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "PK_CauseTranslations" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_CauseTranslations_Causes_CauseId" FOREIGN KEY ("CauseId") REFERENCES "Causes" ("Id") ON DELETE CASCADE
                )
                """);
            await Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_CauseTranslations_CauseId_Language"
                ON "CauseTranslations" ("CauseId", "Language")
                """);
        }
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Cause> Causes => Set<Cause>();
    public DbSet<DailyAction> DailyActions => Set<DailyAction>();
    public DbSet<UserAction> UserActions => Set<UserAction>();
    public DbSet<Outcome> Outcomes => Set<Outcome>();
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
