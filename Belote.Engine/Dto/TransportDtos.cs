using Belote.Engine.Hand;

namespace Belote.Engine.Dto;

public sealed record SeatInfoDto(
    string Nickname,
    string AvatarUrl,
    int Rating,
    bool IsRobot,
    bool IsConnected);

public sealed record MatchScoreDto(
    int NorthSouthTotal,
    int EastWestTotal,
    int Target);

public sealed record TableLogEntryDto(
    long Sequence,
    string Code,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record TableSnapshotDto(
    string TableId,
    MatchScoreDto Match,
    IReadOnlyDictionary<Seat, SeatInfoDto> Seats,
    BeloteHandState Hand,
    IReadOnlyList<TableLogEntryDto> Log,
    bool IsPaused,
    int BotDelayMs);

public sealed record ClientCommandDto(
    string Command,
    ContractKind? ContractKind,
    string? Card,
    IReadOnlyList<AnnouncementClaim>? Announcements);

public sealed record ReplayResultDto(
    bool Identical,
    string LiveHash,
    string ReplayHash,
    BeloteHandState ReplayHand);

// Chat (placeholder, no persistence/moderation yet).
public sealed record ChatMessageDto(
    string Sender,
    string Message,
    long ClientTimestampMs);
