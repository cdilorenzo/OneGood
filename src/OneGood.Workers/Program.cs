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

// Apply EF Core migrations automatically on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OneGoodDbContext>();
    db.Database.Migrate();
}

await host.RunAsync();
