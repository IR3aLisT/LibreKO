using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.World;

public class ChatRoomHistoryPruneService(
    IChatRoomHistory history,
    ILogger<ChatRoomHistoryPruneService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var pruned = await history.PruneAsync();
                if (pruned.Rows > 0)
                    logger.LogInformation("Chat room history pruned: {Rows} rows, {Files} voice note files", pruned.Rows, pruned.Files);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Chat room history pruning failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
