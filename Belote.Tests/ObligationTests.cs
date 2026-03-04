using Belote.Engine;
using Belote.Engine.Hand;

namespace Belote.Tests;

public sealed class ObligationTests
{
    [Fact]
    public void MustFollowSuit_WhenPossible()
    {
        var state = MakePlayingState(
            contract: new Contract(ContractKind.Hearts),
            currentTrick: new List<PlayedCard> { new(Seat.West, new Card(Suit.Clubs, Rank.Ace)) },
            hands: new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.South] = new[] { new Card(Suit.Clubs, Rank.Seven), new Card(Suit.Hearts, Rank.Seven) },
            });

        var legal = BeloteHandRules.GetLegalPlays(state, Seat.South);
        Assert.Equal([new Card(Suit.Clubs, Rank.Seven)], legal);
    }

    [Fact]
    public void MustTrump_WhenVoidInLedSuit_AndHasTrump()
    {
        var state = MakePlayingState(
            contract: new Contract(ContractKind.Hearts),
            currentTrick: new List<PlayedCard> { new(Seat.West, new Card(Suit.Clubs, Rank.Ace)) },
            hands: new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.South] = new[] { new Card(Suit.Hearts, Rank.Seven), new Card(Suit.Spades, Rank.Seven) },
            });

        var legal = BeloteHandRules.GetLegalPlays(state, Seat.South);
        Assert.Equal([new Card(Suit.Hearts, Rank.Seven)], legal);
    }

    [Fact]
    public void MayDiscard_WhenVoidInLedSuit_AndNoTrump()
    {
        var state = MakePlayingState(
            contract: new Contract(ContractKind.Hearts),
            currentTrick: new List<PlayedCard> { new(Seat.West, new Card(Suit.Clubs, Rank.Ace)) },
            hands: new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.South] = new[] { new Card(Suit.Spades, Rank.Seven), new Card(Suit.Diamonds, Rank.Seven) },
            });

        var legal = BeloteHandRules.GetLegalPlays(state, Seat.South);
        Assert.Equal(2, legal.Count);
    }

    [Fact]
    public void Raising_AT_Required_OnAllSuits()
    {
        var trick = new List<PlayedCard>
        {
            new(Seat.West, new Card(Suit.Clubs, Rank.Nine)),
            new(Seat.South, new Card(Suit.Clubs, Rank.Seven)),
        };

        var state = MakePlayingState(
            contract: new Contract(ContractKind.AllTrump),
            currentTrick: trick,
            hands: new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.East] = new[] { new Card(Suit.Clubs, Rank.Ace), new Card(Suit.Clubs, Rank.Jack) },
            });

        var legal = BeloteHandRules.GetLegalPlays(state, Seat.East);
        Assert.Equal([new Card(Suit.Clubs, Rank.Jack)], legal);
    }

    [Fact]
    public void Raising_NT_IsNeverRequired()
    {
        var trick = new List<PlayedCard>
        {
            new(Seat.West, new Card(Suit.Clubs, Rank.Nine)),
        };

        var state = MakePlayingState(
            contract: new Contract(ContractKind.NoTrump),
            currentTrick: trick,
            hands: new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.South] = new[] { new Card(Suit.Clubs, Rank.Ace), new Card(Suit.Clubs, Rank.Seven) },
            });

        var legal = BeloteHandRules.GetLegalPlays(state, Seat.South);
        Assert.Equal(2, legal.Count);
    }

    [Fact]
    public void Raising_SuitContract_Required_ForTrumpOvertrump()
    {
        var trick = new List<PlayedCard>
        {
            new(Seat.West, new Card(Suit.Hearts, Rank.Nine)),
        };

        var state = MakePlayingState(
            contract: new Contract(ContractKind.Hearts),
            currentTrick: trick,
            hands: new Dictionary<Seat, IReadOnlyList<Card>>
            {
                [Seat.South] = new[] { new Card(Suit.Hearts, Rank.Jack), new Card(Suit.Hearts, Rank.Seven) },
            });

        var legal = BeloteHandRules.GetLegalPlays(state, Seat.South);
        Assert.Equal([new Card(Suit.Hearts, Rank.Jack)], legal);
    }

    private static BeloteHandState MakePlayingState(Contract contract, List<PlayedCard> currentTrick, Dictionary<Seat, IReadOnlyList<Card>> hands)
    {
        var baseHands = Enum.GetValues<Seat>().ToDictionary(s => s, _ => (IReadOnlyList<Card>)Array.Empty<Card>());
        foreach (var kvp in hands)
        {
            baseHands[kvp.Key] = kvp.Value;
        }

        var state = BeloteHandState.CreateNew(seed: 99, dealer: Seat.North) with
        {
            Phase = BeloteHandPhase.Playing,
            Turn = hands.Keys.First(),
            HighestContract = contract,
            HighestBidder = Seat.North,
            Hands = baseHands,
            InitialHands = baseHands,
            CurrentTrick = currentTrick,
        };

        return state;
    }
}

