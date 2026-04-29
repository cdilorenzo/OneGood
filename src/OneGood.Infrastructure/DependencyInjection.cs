using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneGood.Core.AI;
using OneGood.Core.Classification;
using OneGood.Core.Interfaces;
using OneGood.Infrastructure.AI;
using OneGood.Infrastructure.Caching;
using OneGood.Infrastructure.Classification;
using OneGood.Infrastructure.Data;
using OneGood.Infrastructure.ExternalApis;
using OneGood.Infrastructure.Repositories;
using OneGood.Infrastructure.Services;
using OneGood.Infrastructure.Translation;

namespace OneGood.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adds all infrastructure services to the service collection.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database — controlled by "Database:Provider"
        //   Sqlite     - local file (default for local dev)
        //   Postgres   - external DB (recommended for Render, e.g. free Neon.tech)
        //   SqlServer  - Azure / on-prem
        //   InMemory   - tests only (data lost on restart)
        var dbProvider = configuration["Database:Provider"] ?? "Sqlite";

        services.AddDbContext<OneGoodDbContext>(options =>
        {
            switch (dbProvider.ToLowerInvariant())
            {
                case "postgres":
                case "postgresql":
                    var pgConn = configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException(
                            "Database:Provider is Postgres but ConnectionStrings:DefaultConnection is empty. "
                            + "Set it to your Neon.tech PostgreSQL connection string.");
                    options.UseNpgsql(pgConn);
                    break;

                case "sqlserver":
                    var sqlConn = configuration.GetConnectionString("DefaultConnection");
                    options.UseSqlServer(sqlConn);
                    break;

                case "inmemory":
                    options.UseInMemoryDatabase("OneGood");
                    break;

                case "sqlite":
                default:
                    // Default path works on Render (/opt/render/project/src) and locally.
                    var sqlitePath = configuration["Database:SqlitePath"] ?? "onegood.db";
                    options.UseSqlite($"Data Source={sqlitePath}");
                    break;
            }
        });

        // Cause Classifier (AI-powered via configured LLM provider)
        services.AddSingleton<ICauseClassifier, AiCauseClassifier>();

        // Repositories
        services.AddScoped<ICauseRepository, CauseRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOutcomeTracker, OutcomeRepository>();

        // HTTP Client for external APIs
        services.AddHttpClient();
        services.AddHttpClient("MyMemory"); // For translation service

        // External API clients (ALL FREE - no API keys required except GlobalGiving!)
        // === German Donations ===
        services.AddScoped<BetterplaceClient>();

        // === German Petitions (Sign action) ===
        services.AddScoped<OpenPetitionClient>();
        services.AddScoped<WeActClient>();

        // === German Parliament (Write action) ===
        services.AddScoped<AbgeordnetenwatchClient>();

        // === International (requires API key) ===
        services.AddScoped<GlobalGivingClient>();

        // Translation Service (FREE - MyMemory API, no key required)
        services.AddScoped<ITranslationService, MyMemoryTranslationService>();

        // AI Configuration
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        // Use FallbackAiService for immediate failover between providers
        // No retry delays - immediately switches to next provider if one fails
        services.AddSingleton<IAiService, FallbackAiService>();

        // AI Engine (wraps IAiService with OneGood-specific logic)
        services.AddScoped<IAiEngine, AiEngineService>();

        // Action Engine (core business logic)
        services.AddScoped<IActionEngine, ActionEngine>();

        // Cause Aggregator (fetches & scores causes from external APIs)
        services.AddScoped<CauseAggregatorService>();

        // Optional Redis cache (if configured), otherwise use in-memory cache
        var redisConnection = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
                options.Configuration = redisConnection);
        }
        else
        {
            // Use in-memory distributed cache for development/testing
            services.AddDistributedMemoryCache();
        }

        // Content cache for AI-generated content (uses IDistributedCache under the hood)
        services.AddSingleton<IContentCache, DistributedContentCache>();

        return services;
    }
}
