namespace OneGood.Api;

/// <summary>
/// Pings the app's own health endpoint every 14 minutes to prevent
/// Render free tier from putting the service to sleep (sleeps after 15 min).
/// Only active when KeepAlive:Url is configured (production only).
/// </summary>
public class KeepAliveService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeepAliveService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(14);

    public KeepAliveService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<KeepAliveService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var selfUrl = _configuration["KeepAlive:Url"];
        if (string.IsNullOrEmpty(selfUrl))
        {
            _logger.LogInformation("KeepAlive disabled — set KeepAlive:Url to enable");
            return;
        }

        _logger.LogInformation("KeepAlive enabled — pinging {Url} every {Minutes} min",
            selfUrl, Interval.TotalMinutes);

        // Wait before the first ping so the app finishes starting
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync(selfUrl, stoppingToken);
                _logger.LogDebug("KeepAlive ping: {Status}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KeepAlive ping failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
