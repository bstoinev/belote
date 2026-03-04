using Belote.Engine;
using Belote.Engine.Dto;
using Belote.Engine.Hand;
using Belote.Engine.Prng;
using Belote.Server.Hubs;
using Belote.Server.Services.Bots;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server.Services;

public sealed class TableManager
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _spectators = new();
    private readonly XorShift128Plus _tableRng;
    private readonly IReadOnlyDictionary<Seat, RobotPlayer> _robots;
    private readonly Dictionary<Seat, SeatInfoDto> _seats;
    private readonly List<TableLogEntryDto> _log = new();

    private long _logSeq;

    private int _nsTotal;
    private int _ewTotal;
    private const int Target = 151;
    private const int DefaultBotDelayMs = 1500;
    private bool _paused;

    private Seat _dealer;
    private BeloteHandState _hand;
    private readonly List<(Seat Actor, BeloteCommand Command)> _handCommands = new();

    private (int Seed, Seat Dealer, IReadOnlyList<(Seat Actor, BeloteCommand Command)> Commands, BeloteHandState FinalState)? _lastHand;

    public TableManager()
    {
        var initialSeed = Random.Shared.Next(int.MinValue, int.MaxValue);
        _tableRng = new XorShift128Plus(initialSeed);

        _dealer = (Seat)_tableRng.NextInt(4);
        _hand = BeloteHandState.CreateNew(NextHandSeed(), _dealer);

        _seats = new Dictionary<Seat, SeatInfoDto>
        {
            [Seat.North] = new SeatInfoDto("North", "/avatars/bot-north.svg", 1200, IsRobot: true, IsConnected: true),
            [Seat.East] = new SeatInfoDto("East", "/avatars/bot-east.svg", 1200, IsRobot: true, IsConnected: true),
            [Seat.South] = new SeatInfoDto("South", "/avatars/bot-south.svg", 1200, IsRobot: true, IsConnected: true),
            [Seat.West] = new SeatInfoDto("West", "/avatars/bot-west.svg", 1200, IsRobot: true, IsConnected: true),
        };

        _robots = new Dictionary<Seat, RobotPlayer>
        {
            [Seat.North] = new RobotPlayer(Seat.North),
            [Seat.East] = new RobotPlayer(Seat.East),
            [Seat.South] = new RobotPlayer(Seat.South),
            [Seat.West] = new RobotPlayer(Seat.West),
        };

        AppendLog("TABLE_START", new Dictionary<string, string> { ["dealer"] = _dealer.ToString(), ["seed"] = _hand.Seed.ToString() });
    }

    public TableSnapshotDto GetSnapshot()
    {
        lock (_gate)
        {
            return SnapshotUnsafe();
        }
    }

    public int BotDelayMs
    {
        get
        {
            lock (_gate)
            {
                return DefaultBotDelayMs;
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _paused;
            }
        }
    }

    public TableSnapshotDto SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_paused != paused)
            {
                _paused = paused;
                AppendLog(paused ? "PLAY_PAUSED" : "PLAY_RESUMED", new Dictionary<string, string>());
            }

            return SnapshotUnsafe();
        }
    }

    public void RegisterSpectator(string connectionId, string? requestedNickname)
    {
        var nick = SanitizeNickname(requestedNickname);
        lock (_gate)
        {
            _spectators[connectionId] = nick;
        }
    }

    public void UnregisterSpectator(string connectionId)
    {
        lock (_gate)
        {
            _spectators.Remove(connectionId);
        }
    }

    public ReplayResultDto ReplayLastHand()
    {
        lock (_gate)
        {
            if (_lastHand is null)
            {
                var liveHash = BeloteHandCanonical.ComputeHash(_hand);
                return new ReplayResultDto(Identical: true, liveHash, liveHash, _hand);
            }

            var last = _lastHand.Value;
            var replay = BeloteHandState.CreateNew(last.Seed, last.Dealer);
            foreach (var (actor, cmd) in last.Commands)
            {
                var ok = BeloteHandEngine.TryApply(replay, actor, cmd, out var r, out _);
                if (!ok)
                {
                    break;
                }

                replay = r.State;
            }

            var liveFinalHash = BeloteHandCanonical.ComputeHash(last.FinalState);
            var replayHash = BeloteHandCanonical.ComputeHash(replay);
            var identical = string.Equals(liveFinalHash, replayHash, StringComparison.Ordinal);
            return new ReplayResultDto(identical, liveFinalHash, replayHash, replay);
        }
    }

    internal bool TryMakeBotMove(out TableSnapshotDto? updatedSnapshot)
    {
        updatedSnapshot = null;
        lock (_gate)
        {
            if (_paused)
            {
                return false;
            }

            // Hand ended: settle and start next.
            if (_hand.Phase is BeloteHandPhase.CanceledAllPass or BeloteHandPhase.Completed)
            {
                if (_hand.Outcome is not null)
                {
                    _lastHand = (_hand.Seed, _hand.Dealer, _handCommands.ToArray(), _hand);
                }

                if (_hand.Phase == BeloteHandPhase.Completed && _hand.Outcome is not null)
                {
                    _nsTotal += _hand.Outcome.NorthSouthAwardedPoints;
                    _ewTotal += _hand.Outcome.EastWestAwardedPoints;

                    if (_nsTotal >= Target || _ewTotal >= Target)
                    {
                        var winner = _nsTotal >= Target ? "NS" : "EW";
                        AppendLog("MATCH_WON", new Dictionary<string, string> { ["winner"] = winner, ["ns"] = _nsTotal.ToString(), ["ew"] = _ewTotal.ToString() });
                        _nsTotal = 0;
                        _ewTotal = 0;
                    }
                }

                var nextDealer = _hand.Outcome?.NextDealer ?? _hand.Dealer.NextCcw();
                _dealer = nextDealer;
                _handCommands.Clear();
                _hand = BeloteHandState.CreateNew(NextHandSeed(), _dealer);
                AppendLog("HAND_START", new Dictionary<string, string> { ["dealer"] = _dealer.ToString(), ["seed"] = _hand.Seed.ToString() });
                updatedSnapshot = SnapshotUnsafe();
                return true;
            }

            if (!_robots.TryGetValue(_hand.Turn, out var robot))
            {
                return false;
            }

            var cmd = robot.Decide(_hand);
            if (cmd is null)
            {
                return false;
            }

            var okApply = BeloteHandEngine.TryApply(_hand, robot.Seat, cmd, out var res, out var rej);
            if (!okApply)
            {
                AppendLog("ROBOT_REJECTED", new Dictionary<string, string> { ["seat"] = robot.Seat.ToString(), ["code"] = rej?.Code ?? "UNKNOWN" });
                return false;
            }

            _hand = res.State;
            _handCommands.Add((robot.Seat, cmd));

            AppendLog("CMD", new Dictionary<string, string> { ["seat"] = robot.Seat.ToString(), ["cmd"] = cmd.GetType().Name });
            foreach (var e in res.Events)
            {
                AppendLog($"EVT_{e.Code}", e.Parameters);
            }

            updatedSnapshot = SnapshotUnsafe();
            return true;
        }
    }

    private int NextHandSeed() => unchecked((int)_tableRng.NextUInt64());

    private TableSnapshotDto SnapshotUnsafe()
    {
        var match = new MatchScoreDto(_nsTotal, _ewTotal, Target);
        var snapshot = new TableSnapshotDto("Table-1", match, _seats, _hand, _log.ToArray(), _paused, DefaultBotDelayMs);
        return snapshot;
    }

    private void AppendLog(string code, IReadOnlyDictionary<string, string> parameters)
    {
        _logSeq++;
        _log.Add(new TableLogEntryDto(_logSeq, code, parameters));
        if (_log.Count > 200)
        {
            _log.RemoveAt(0);
        }
    }

    private static string SanitizeNickname(string? requestedNickname)
    {
        var nick = (requestedNickname ?? string.Empty).Trim();
        if (nick.Length == 0)
        {
            nick = $"Guest-{Random.Shared.Next(1000, 9999)}";
        }

        if (nick.Length > 16)
        {
            nick = nick[..16];
        }

        return nick;
    }
}
