using Belote.Engine.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;

namespace Belote.Client.Services;

public sealed class TableHubClient
{
    private readonly HubConnection _hub;
    private readonly object _gate = new();
    private bool _started;

    public TableSnapshotDto? Snapshot { get; private set; }

    public event Action? SnapshotChanged;

    public TableHubClient(NavigationManager nav)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri("/hubs/table"))
            .WithAutomaticReconnect()
            .Build();

        _hub.On<TableSnapshotDto>("TableSnapshot", snapshot =>
        {
            lock (_gate)
            {
                Snapshot = snapshot;
            }

            SnapshotChanged?.Invoke();
        });
    }

    public async Task EnsureStartedAsync()
    {
        var start = false;
        lock (_gate)
        {
            if (!_started)
            {
                _started = true;
                start = true;
            }
        }

        if (!start)
        {
            return;
        }

        await _hub.StartAsync();
        Snapshot = await _hub.InvokeAsync<TableSnapshotDto>("GetSnapshot");
        SnapshotChanged?.Invoke();
    }

    public Task<ReplayResultDto> ReplayLastHandAsync()
    {
        return _hub.InvokeAsync<ReplayResultDto>("ReplayLastHand");
    }

    public Task<TableSnapshotDto> SetPausedAsync(bool paused)
    {
        return _hub.InvokeAsync<TableSnapshotDto>("SetPaused", paused);
    }

    public Task<TableSnapshotDto> TogglePausedAsync()
    {
        return _hub.InvokeAsync<TableSnapshotDto>("TogglePaused");
    }

    public ValueTask DisposeAsync() => _hub.DisposeAsync();
}
