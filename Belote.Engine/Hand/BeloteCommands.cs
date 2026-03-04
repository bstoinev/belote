namespace Belote.Engine.Hand;

public abstract record BeloteCommand;

public sealed record CutCommand : BeloteCommand;

public sealed record PassCommand : BeloteCommand;

public sealed record BidCommand(Contract Contract) : BeloteCommand;

public sealed record ContraCommand : BeloteCommand;

public sealed record RecontraCommand : BeloteCommand;

public enum AnnouncementKind
{
    Terca = 3,
    Quarta = 4,
    Quinta = 5,
}

public sealed record AnnouncementClaim(AnnouncementKind Kind, Suit Suit, Rank HighestRank);

public sealed record DeclareAnnouncementsCommand(IReadOnlyList<AnnouncementClaim> Claims) : BeloteCommand;

public sealed record PlayCardCommand(Card Card) : BeloteCommand;

/// <summary>
/// Collects the completed trick (4 cards), determines the winner, and advances the hand to the next trick (or completes the hand).
/// </summary>
public sealed record CollectTrickCommand : BeloteCommand;
