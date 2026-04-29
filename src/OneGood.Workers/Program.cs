using Microsoft.EntityFrameworkCore;
using OneGood.Infrastructure;
using OneGood.Infrastructure.Data;
using OneGood.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Add infrastructure services (database, AI, repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add background workers
builder.Services.AddHostedService<CauseRefreshWorker>();
builder.Services.AddHostedService<OutcomeAggregatorWorker>();

var host = builder.Build();

// Ensure database is created (needed for SQLite on first run)
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OneGoodDbContext>();
    await db.InitializeAsync();
}

await host.RunAsync();
