using Belote.Engine.Dto;
using Belote.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server.Hubs;

public sealed class TableHub(TableManager table) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var nick = http?.Request.Query["nick"].ToString();
        table.RegisterSpectator(Context.ConnectionId, nick);

        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("TableSnapshot", table.GetSnapshot());
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        table.UnregisterSpectator(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task<TableSnapshotDto> GetSnapshot()
    {
        return Task.FromResult(table.GetSnapshot());
    }

    public async Task<TableSnapshotDto> SetPaused(bool paused)
    {
        var snapshot = table.SetPaused(paused);
        await Clients.All.SendAsync("TableSnapshot", snapshot);
        return snapshot;
    }

    public Task<TableSnapshotDto> TogglePaused()
    {
        var snapshot = table.SetPaused(!table.IsPaused);
        _ = Clients.All.SendAsync("TableSnapshot", snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<ReplayResultDto> ReplayLastHand()
    {
        return Task.FromResult(table.ReplayLastHand());
    }

    public Task SendCommand(ClientCommandDto command)
    {
        // MVP spectator-first: command handling is stubbed for future human seating.
        // Commands are executed by robots on the server.
        return Task.CompletedTask;
    }

    public Task SendChat(ChatMessageDto message)
    {
        // Placeholder: no persistence/moderation yet.
        return Task.CompletedTask;
    }
}
