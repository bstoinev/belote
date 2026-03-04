using Belote.Engine;
using Belote.Engine.Hand;

namespace Belote.Tests;

public sealed class ScoringTests
{
    [Fact]
    public void InsideRule_AwardsAllToDefenders_WithMultiplier()
    {
        var contract = new Contract(ContractKind.Hearts);

        var nsWon = new List<Card>
        {
            new(Suit.Hearts, Rank.Seven),
            new(Suit.Hearts, Rank.Eight),
            new(Suit.Clubs, Rank.Seven),
            new(Suit.Clubs, Rank.Eight),
            new(Suit.Clubs, Rank.Nine),
            new(Suit.Diamonds, Rank.Seven),
            new(Suit.Diamonds, Rank.Eight),
            new(Suit.Diamonds, Rank.Nine),
            new(Suit.Spades, Rank.Seven),
            new(Suit.Spades, Rank.Eight),
            new(Suit.Spades, Rank.Nine),
            new(Suit.Clubs, Rank.Jack),
            new(Suit.Diamonds, Rank.Jack),
            new(Suit.Spades, Rank.Jack),
            new(Suit.Clubs, Rank.Queen),
            new(Suit.Diamonds, Rank.Queen),
        };

        var ewWon = new List<Card>
        {
            new(Suit.Diamonds, Rank.Ace),
            new(Suit.Spades, Rank.Ace),
            new(Suit.Clubs, Rank.Ten),
            new(Suit.Diamonds, Rank.Ten),
            new(Suit.Spades, Rank.Ten),
            new(Suit.Hearts, Rank.Ace),
            new(Suit.Clubs, Rank.King),
            new(Suit.Diamonds, Rank.King),
            new(Suit.Spades, Rank.King),
            new(Suit.Hearts, Rank.King),
            new(Suit.Hearts, Rank.Queen),
            new(Suit.Spades, Rank.Queen),
        };

        var announcements = new List<AnnouncementAward>
        {
            new(Seat.North, AnnouncementKind.Terca, Suit.Spades, Rank.Nine, Points: 20),
        };

        var belotes = new List<BeloteAward>
        {
            new(Seat.North, Suit.Hearts, Points: 20),
        };

        var state = BeloteHandState.CreateNew(seed: 5, dealer: Seat.North) with
        {
            Phase = BeloteHandPhase.Playing,
            HighestContract = contract,
            HighestBidder = Seat.North, // bidding team NS
            Doubling = Doubling.Contra,
            TrickNumber = 7,
            NorthSouthWonCards = nsWon,
            EastWestWonCards = ewWon,
            AnnouncementsDeclared = true,
            Announcements = announcements,
            Belotes = belotes,
            Hands = new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.North] = new[] { new Card(Suit.Clubs, Rank.Nine) },
                [Seat.East] = Array.Empty<Card>(),
                [Seat.South] = Array.Empty<Card>(),
                [Seat.West] = Array.Empty<Card>(),
            },
            InitialHands = new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.North] = new[] { new Card(Suit.Clubs, Rank.Nine) },
                [Seat.East] = Array.Empty<Card>(),
                [Seat.South] = Array.Empty<Card>(),
                [Seat.West] = Array.Empty<Card>(),
            },
            CurrentTrick = new List<PlayedCard>
            {
                new(Seat.West, new Card(Suit.Clubs, Rank.Ace)),
                new(Seat.South, new Card(Suit.Clubs, Rank.Seven)),
                new(Seat.East, new Card(Suit.Clubs, Rank.Eight)),
            },
            Turn = Seat.North,
        };

        Apply(ref state, Seat.North, new PlayCardCommand(new Card(Suit.Clubs, Rank.Nine)));
        Apply(ref state, state.Turn, new CollectTrickCommand());

        Assert.Equal(BeloteHandPhase.Completed, state.Phase);
        Assert.NotNull(state.Outcome);
        Assert.True(state.Outcome!.InsideRuleApplied);
        Assert.Equal(0, state.Outcome.NorthSouthAwardedPoints);
        Assert.True(state.Outcome.EastWestAwardedPoints > 0);
        Assert.Equal(Doubling.Contra, state.Outcome.Doubling);
    }

    [Fact]
    public void ContraMultiplier_AppliesToBiddingTeam_WhenNotInside()
    {
        var contract = new Contract(ContractKind.NoTrump);
        var state = BeloteHandState.CreateNew(seed: 6, dealer: Seat.North) with
        {
            Phase = BeloteHandPhase.Playing,
            HighestContract = contract,
            HighestBidder = Seat.North, // bidding team NS
            Doubling = Doubling.Contra,
            TrickNumber = 7,
            NorthSouthWonCards = new List<Card> { new(Suit.Clubs, Rank.Ace) }, // 11
            EastWestWonCards = new List<Card>(), // 0
            Hands = new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.North] = new[] { new Card(Suit.Clubs, Rank.Ten) },
                [Seat.East] = Array.Empty<Card>(),
                [Seat.South] = Array.Empty<Card>(),
                [Seat.West] = Array.Empty<Card>(),
            },
            InitialHands = new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.North] = new[] { new Card(Suit.Clubs, Rank.Ten) },
                [Seat.East] = Array.Empty<Card>(),
                [Seat.South] = Array.Empty<Card>(),
                [Seat.West] = Array.Empty<Card>(),
            },
            CurrentTrick = new List<PlayedCard>
            {
                new(Seat.West, new Card(Suit.Clubs, Rank.Seven)),
                new(Seat.South, new Card(Suit.Clubs, Rank.Eight)),
                new(Seat.East, new Card(Suit.Clubs, Rank.Nine)),
            },
            Turn = Seat.North,
        };

        Apply(ref state, Seat.North, new PlayCardCommand(new Card(Suit.Clubs, Rank.Ten)));
        Apply(ref state, state.Turn, new CollectTrickCommand());

        Assert.NotNull(state.Outcome);
        Assert.False(state.Outcome!.InsideRuleApplied);
        Assert.True(state.Outcome.NorthSouthAwardedPoints % 2 == 0);
        Assert.True(state.Outcome.NorthSouthAwardedPoints > state.Outcome.EastWestAwardedPoints);
    }

    private static void Apply(ref BeloteHandState state, Seat actor, BeloteCommand cmd)
    {
        var ok = BeloteHandEngine.TryApply(state, actor, cmd, out var res, out var rej);
        Assert.True(ok, rej?.Code);
        state = res.State;
    }
}
