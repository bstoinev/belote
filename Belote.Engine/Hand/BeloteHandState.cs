using Belote.Engine.Prng;

namespace Belote.Engine.Hand;

public sealed record PlayedCard(Seat Seat, Card Card);

public sealed record TrickResult(int TrickNumber, Seat Winner, IReadOnlyList<PlayedCard> Cards);

public sealed record AnnouncementAward(Seat Seat, AnnouncementKind Kind, Suit Suit, Rank HighestRank, int Points);

public sealed record BeloteAward(Seat Seat, Suit TrumpSuit, int Points);

public sealed record BidEntry(Seat Seat, string Action, Contract? Contract, Doubling Doubling);

public sealed record HandOutcome(
    bool WasCanceledAllPass,
    Contract? Contract,
    Doubling Doubling,
    Team? BiddingTeam,
    bool InsideRuleApplied,
    int NorthSouthAwardedPoints,
    int EastWestAwardedPoints,
    Seat NextDealer);

public sealed record BeloteHandState
{
    public required Guid HandId { get; init; }
    public required int Seed { get; init; }

    public required Seat Dealer { get; init; }
    public Seat Cutter => Dealer.PrevCcw();
    public Seat Eldest => Dealer.NextCcw();

    public required BeloteHandPhase Phase { get; init; }
    public required Seat Turn { get; init; }

    public required IReadOnlyDictionary<Seat, IReadOnlyList<Card>> Hands { get; init; }
    public required IReadOnlyDictionary<Seat, IReadOnlyList<Card>> InitialHands { get; init; }

    public required IReadOnlyList<BidEntry> BiddingLog { get; init; }
    public required Contract? HighestContract { get; init; }
    public required Seat? HighestBidder { get; init; }
    public required Doubling Doubling { get; init; }
    public required int PassesBeforeAnyBid { get; init; }
    public required int PassesAfterLastBid { get; init; }

    public required bool AnnouncementsDeclared { get; init; }
    public required IReadOnlyList<AnnouncementAward> Announcements { get; init; }
    public required IReadOnlyList<BeloteAward> Belotes { get; init; }

    public required int TrickNumber { get; init; } // 0..7
    public required IReadOnlyList<PlayedCard> CurrentTrick { get; init; } // 0..3
    public required IReadOnlyList<TrickResult> CompletedTricks { get; init; }

    public required IReadOnlyList<Card> NorthSouthWonCards { get; init; }
    public required IReadOnlyList<Card> EastWestWonCards { get; init; }

    public required HandOutcome? Outcome { get; init; }

    public static BeloteHandState CreateNew(int seed, Seat dealer)
    {
        var emptyHands = Enum.GetValues<Seat>().ToDictionary(s => s, _ => (IReadOnlyList<Card>)Array.Empty<Card>());
        var state = new BeloteHandState
        {
            HandId = XorShift128Plus.DeterministicGuidFromSeed(seed),
            Seed = seed,
            Dealer = dealer,
            Phase = BeloteHandPhase.AwaitingCut,
            Turn = dealer.PrevCcw(), // Cutter must act (cut) before dealing.
            Hands = emptyHands,
            InitialHands = emptyHands,
            BiddingLog = Array.Empty<BidEntry>(),
            HighestContract = null,
            HighestBidder = null,
            Doubling = Doubling.None,
            PassesBeforeAnyBid = 0,
            PassesAfterLastBid = 0,
            AnnouncementsDeclared = false,
            Announcements = Array.Empty<AnnouncementAward>(),
            Belotes = Array.Empty<BeloteAward>(),
            TrickNumber = 0,
            CurrentTrick = Array.Empty<PlayedCard>(),
            CompletedTricks = Array.Empty<TrickResult>(),
            NorthSouthWonCards = Array.Empty<Card>(),
            EastWestWonCards = Array.Empty<Card>(),
            Outcome = null,
        };

        return state;
    }
}

