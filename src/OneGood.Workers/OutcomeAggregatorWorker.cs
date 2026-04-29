using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneGood.Core.Interfaces;

namespace OneGood.Workers;

public class OutcomeAggregatorWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutcomeAggregatorWorker> _logger;

    public OutcomeAggregatorWorker(IServiceProvider services, ILogger<OutcomeAggregatorWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutcomeAggregatorWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var causeAggregator = scope.ServiceProvider.GetRequiredService<OneGood.Infrastructure.Services.CauseAggregatorService>();
                var outcomeRepo = scope.ServiceProvider.GetRequiredService<IOutcomeTracker>();
                var causeRepo = scope.ServiceProvider.GetRequiredService<OneGood.Core.Interfaces.ICauseRepository>();
                _logger.LogInformation("[OutcomeAggregatorWorker] Aggregation cycle started at: {Time}", DateTimeOffset.Now);

                // Fetch all active causes
                var causes = await causeRepo.GetAllCausesAsync();
                foreach (var cause in causes)
                {
                    // Find the latest DailyAction for this cause
                    var dailyAction = await causeRepo.GetLatestActionByCauseIdAsync(cause.Id);
                    if (dailyAction == null) continue;

                    // Create/update Outcome with available data
                    var outcome = new OneGood.Core.Models.Outcome
                    {
                        Id = Guid.NewGuid(),
                        DailyActionId = dailyAction.Id,
                        Headline = cause.Title,
                        Story = cause.Description,
                        PhotoUrl = cause.ImageUrl,
                        OutcomeDate = DateTime.UtcNow,
                        IsPositive = true,
                        PeopleImpacted = 0, // Not available from API
                        SourceUrl = cause.OrganisationUrl,
                        TotalActionsContributed = 0, // Not available from API
                        TotalDonationsRaised = cause.FundingCurrent ?? 0
                    };
                    await outcomeRepo.SaveOutcomeAsync(outcome);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OutcomeAggregatorWorker");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
