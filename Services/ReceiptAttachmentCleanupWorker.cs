namespace ClintonFrankland.Services;

public sealed class ReceiptAttachmentCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReceiptAttachmentCleanupWorker> _logger;

    public ReceiptAttachmentCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<ReceiptAttachmentCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var storage = scope.ServiceProvider.GetRequiredService<ReceiptAttachmentStorageService>();
                await storage.CleanupOrphansAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt attachment cleanup worker failed");
            }
        }
    }
}
