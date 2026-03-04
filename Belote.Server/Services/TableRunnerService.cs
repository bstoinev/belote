using Belote.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server.Services;

public sealed class TableRunnerService(TableManager table, IHubContext<TableHub> hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (table.IsPaused)
            {
                await Task.Delay(150, stoppingToken);
                continue;
            }

            if (table.TryMakeBotMove(out var snapshot) && snapshot is not null)
            {
                await hub.Clients.All.SendAsync("TableSnapshot", snapshot, stoppingToken);
                if (snapshot.Hand.Phase == Belote.Engine.Hand.BeloteHandPhase.Playing &&
                    snapshot.Hand.CurrentTrick.Count == 4)
                {
                    // After the 4th card of a trick is played, keep it visible for a moment before collection.
                    await Task.Delay(1000, stoppingToken);
                }
                else
                {
                    await Task.Delay(table.BotDelayMs, stoppingToken);
                }
                continue;
            }

            await Task.Delay(50, stoppingToken);
        }
    }
}
