using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public class ChatRoomHistoryPruneService(
    IChatRoomHistory history,
    INationChatService rooms,
    IOptions<GameServerSettings> settings,
    ILogger<ChatRoomHistoryPruneService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await rooms.RestoreKeptAsync();
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var pruned = await history.PruneAsync();
                if (pruned.Rows > 0)
                    logger.LogInformation("Chat room history pruned: {Rows} rows, {Files} voice note files", pruned.Rows, pruned.Files);
                int kept = await rooms.PruneKeptAsync(DateTime.UtcNow.AddDays(-settings.Value.ChatRooms.HistoryDays));
                if (kept > 0)
                    logger.LogInformation("Chat rooms unused for {Days} days removed: {Count}", settings.Value.ChatRooms.HistoryDays, kept);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Chat room history pruning failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
