namespace Belote.Engine.Hand;

public static class BeloteHandRules
{
    private static readonly Rank[] TrumpOrder =
    [
        Rank.Jack, Rank.Nine, Rank.Ace, Rank.Ten, Rank.King, Rank.Queen, Rank.Eight, Rank.Seven
    ];

    private static readonly Rank[] NonTrumpOrder =
    [
        Rank.Ace, Rank.Ten, Rank.King, Rank.Queen, Rank.Jack, Rank.Nine, Rank.Eight, Rank.Seven
    ];

    public static int ContractStrength(Contract contract) => (int)contract.Kind;

    public static int DoublingMultiplier(Doubling doubling) => (int)doubling;

    public static Card[] CreateShuffledDeck(int seed)
    {
        var rng = new Prng.XorShift128Plus(seed);
        var deck = Deck32.CreateOrdered();
        rng.Shuffle(deck);
        return deck;
    }

    public static Card[] ApplySymbolicCut(Card[] deck)
    {
        // Symbolic deterministic cut required by Bulgarian rules ("cut" exists as a step/command).
        // Fixed cut keeps replay trivial and deterministic.
        const int cutIndex = 16;
        var cut = new Card[deck.Length];
        Array.Copy(deck, cutIndex, cut, 0, deck.Length - cutIndex);
        Array.Copy(deck, 0, cut, deck.Length - cutIndex, cutIndex);
        return cut;
    }

    public static IReadOnlyDictionary<Seat, IReadOnlyList<Card>> Deal(Card[] deck, Seat dealer)
    {
        var result = Enum.GetValues<Seat>().ToDictionary(s => s, _ => new List<Card>(8));
        var seat = dealer.NextCcw(); // eldest starts receiving cards
        for (var i = 0; i < deck.Length; i++)
        {
            result[seat].Add(deck[i]);
            seat = seat.NextCcw();
        }

        var readonlyResult = result.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<Card>)kvp.Value.OrderBy(c => (int)c.Suit).ThenBy(c => (int)c.Rank).ToArray());
        return readonlyResult;
    }

    public static IReadOnlyList<Card> GetLegalPlays(BeloteHandState state, Seat seat)
    {
        if (!state.Hands.TryGetValue(seat, out var hand))
        {
            return Array.Empty<Card>();
        }

        if (state.Phase != BeloteHandPhase.Playing || state.HighestContract is null)
        {
            return Array.Empty<Card>();
        }

        if (state.CurrentTrick.Count == 4)
        {
            // Trick is complete; a CollectTrick command is required before further plays.
            return Array.Empty<Card>();
        }

        if (state.CurrentTrick.Count == 0)
        {
            return hand;
        }

        var leadSuit = state.CurrentTrick[0].Card.Suit;
        var hasLeadSuit = hand.Any(c => c.Suit == leadSuit);
        var contract = state.HighestContract.Value;

        if (hasLeadSuit)
        {
            var candidates = hand.Where(c => c.Suit == leadSuit).ToArray();
            var raiseRequired = contract.Kind == ContractKind.AllTrump || (contract.IsSuitContract && contract.TrumpSuit == leadSuit);
            if (raiseRequired)
            {
                var winning = GetCurrentWinningCard(state.CurrentTrick, contract, leadSuit);
                var beating = candidates.Where(c => BeatsInTrick(c, winning.Card, contract, leadSuit)).ToArray();
                if (beating.Length > 0)
                {
                    candidates = beating;
                }
            }

            return candidates;
        }

        // No lead suit.
        if (contract.IsSuitContract)
        {
            var trumpSuit = contract.TrumpSuit!.Value;
            var trumpCards = hand.Where(c => c.Suit == trumpSuit).ToArray();
            if (trumpCards.Length > 0)
            {
                var anyTrumpInTrick = state.CurrentTrick.Any(pc => pc.Card.Suit == trumpSuit);
                if (anyTrumpInTrick)
                {
                    var highestTrumpInTrick = state.CurrentTrick
                        .Select(pc => pc.Card)
                        .Where(c => c.Suit == trumpSuit)
                        .MinBy(c => PowerIndex(c.Rank, isTrumpOrder: true));

                    var beating = trumpCards.Where(c => BeatsTrump(c, highestTrumpInTrick)).ToArray();
                    if (beating.Length > 0)
                    {
                        trumpCards = beating;
                    }
                }

                return trumpCards;
            }
        }

        // Discard any.
        return hand;
    }

    public static (Seat Winner, Card Card) GetCurrentWinningCard(IReadOnlyList<PlayedCard> trick, Contract contract, Suit leadSuit)
    {
        var winner = trick[0];
        for (var i = 1; i < trick.Count; i++)
        {
            var challenger = trick[i];
            if (BeatsInTrick(challenger.Card, winner.Card, contract, leadSuit))
            {
                winner = challenger;
            }
        }

        return (winner.Seat, winner.Card);
    }

    public static bool BeatsInTrick(Card challenger, Card currentWinner, Contract contract, Suit leadSuit)
    {
        var kind = contract.Kind;
        if (kind is ContractKind.NoTrump or ContractKind.AllTrump)
        {
            // No trump suit; led suit always determines.
            if (challenger.Suit != leadSuit)
            {
                return false;
            }

            if (currentWinner.Suit != leadSuit)
            {
                return true;
            }

            var trumpOrder = kind == ContractKind.AllTrump;
            return PowerIndex(challenger.Rank, trumpOrder) < PowerIndex(currentWinner.Rank, trumpOrder);
        }

        // Suit contract.
        var trumpSuit = contract.TrumpSuit!.Value;
        var challengerIsTrump = challenger.Suit == trumpSuit;
        var winnerIsTrump = currentWinner.Suit == trumpSuit;
        if (challengerIsTrump && !winnerIsTrump)
        {
            return true;
        }

        if (!challengerIsTrump && winnerIsTrump)
        {
            return false;
        }

        if (challengerIsTrump && winnerIsTrump)
        {
            return BeatsTrump(challenger, currentWinner);
        }

        // Neither is trump.
        if (challenger.Suit != leadSuit)
        {
            return false;
        }

        if (currentWinner.Suit != leadSuit)
        {
            return true;
        }

        return PowerIndex(challenger.Rank, isTrumpOrder: false) < PowerIndex(currentWinner.Rank, isTrumpOrder: false);
    }

    public static bool BeatsTrump(Card challenger, Card currentTrumpWinner)
        => PowerIndex(challenger.Rank, isTrumpOrder: true) < PowerIndex(currentTrumpWinner.Rank, isTrumpOrder: true);

    public static int Points(Card card, Contract contract)
    {
        var kind = contract.Kind;
        var isTrumpOrder = kind == ContractKind.AllTrump || (contract.IsSuitContract && card.Suit == contract.TrumpSuit);
        var points = isTrumpOrder
            ? card.Rank switch
            {
                Rank.Jack => 20,
                Rank.Nine => 14,
                Rank.Ace => 11,
                Rank.Ten => 10,
                Rank.King => 4,
                Rank.Queen => 3,
                _ => 0,
            }
            : card.Rank switch
            {
                Rank.Ace => 11,
                Rank.Ten => 10,
                Rank.King => 4,
                Rank.Queen => 3,
                Rank.Jack => 2,
                _ => 0,
            };

        return points;
    }

    public static int AnnouncementPoints(AnnouncementKind kind)
    {
        var points = kind switch
        {
            AnnouncementKind.Terca => 20,
            AnnouncementKind.Quarta => 50,
            AnnouncementKind.Quinta => 100,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown announcement kind."),
        };

        return points;
    }

    private static int PowerIndex(Rank rank, bool isTrumpOrder)
    {
        var order = isTrumpOrder ? TrumpOrder : NonTrumpOrder;
        var idx = Array.IndexOf(order, rank);
        return idx;
    }
}
