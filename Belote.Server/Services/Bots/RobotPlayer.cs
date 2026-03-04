using Belote.Engine;
using Belote.Engine.Hand;

namespace Belote.Server.Services.Bots;

public sealed class RobotPlayer(Seat seat)
{
    public Seat Seat { get; } = seat;

    public BeloteCommand? Decide(BeloteHandState state)
    {
        BeloteCommand? cmd = state.Phase switch
        {
            BeloteHandPhase.AwaitingCut => new CutCommand(),
            BeloteHandPhase.Bidding => DecideBid(state),
            BeloteHandPhase.Playing => DecidePlay(state),
            _ => null,
        };

        return cmd;
    }

    private BeloteCommand DecideBid(BeloteHandState state)
    {
        if (!state.Hands.TryGetValue(Seat, out var hand) || hand.Count == 0)
        {
            return new PassCommand();
        }

        var currentStrength = state.HighestContract is null ? -1 : BeloteHandRules.ContractStrength(state.HighestContract.Value);

        // Simple deterministic heuristic: compute estimated score for each contract, choose the best above threshold.
        var candidates = Enum.GetValues<ContractKind>()
            .Select(k => new Contract(k))
            .Where(c => BeloteHandRules.ContractStrength(c) > currentStrength)
            .Select(c => (Contract: c, Score: EstimateContractStrength(hand, c)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => BeloteHandRules.ContractStrength(x.Contract))
            .ToArray();

        if (candidates.Length == 0)
        {
            return new PassCommand();
        }

        var best = candidates[0];
        var threshold = best.Contract.Kind switch
        {
            ContractKind.NoTrump => 68,
            ContractKind.AllTrump => 75,
            _ => 62,
        };

        if (best.Score < threshold)
        {
            return new PassCommand();
        }

        return new BidCommand(best.Contract);
    }

    private BeloteCommand DecidePlay(BeloteHandState state)
    {
        if (state.HighestContract is null)
        {
            return new PassCommand();
        }

        if (state.CurrentTrick.Count == 4)
        {
            return new CollectTrickCommand();
        }

        // Announcements: eldest hand, before first card.
        if (Seat == state.Eldest &&
            state.TrickNumber == 0 &&
            state.CurrentTrick.Count == 0 &&
            !state.AnnouncementsDeclared &&
            state.HighestContract.Value.Kind != ContractKind.NoTrump)
        {
            var claims = FindAnnouncementClaims(state, Seat);
            if (claims.Count > 0)
            {
                return new DeclareAnnouncementsCommand(claims);
            }
        }

        var legal = BeloteHandRules.GetLegalPlays(state, Seat);
        if (legal.Count == 0)
        {
            return new PassCommand();
        }

        var contract = state.HighestContract.Value;
        if (state.CurrentTrick.Count == 0)
        {
            // Lead: preserve high-value cards; play lowest-points card.
            var chosen = legal
                .OrderBy(c => BeloteHandRules.Points(c, contract))
                .ThenBy(c => (int)c.Suit)
                .ThenBy(c => (int)c.Rank)
                .First();
            return new PlayCardCommand(chosen);
        }
        else
        {
            var leadSuit = state.CurrentTrick[0].Card.Suit;
            var winning = BeloteHandRules.GetCurrentWinningCard(state.CurrentTrick, contract, leadSuit);

            var winningCards = legal.Where(c => BeloteHandRules.BeatsInTrick(c, winning.Card, contract, leadSuit)).ToArray();
            if (winningCards.Length > 0)
            {
                var chosen = winningCards
                    .OrderBy(c => BeloteHandRules.Points(c, contract))
                    .ThenBy(c => (int)c.Suit)
                    .ThenBy(c => (int)c.Rank)
                    .First();
                return new PlayCardCommand(chosen);
            }

            var discard = legal
                .OrderBy(c => BeloteHandRules.Points(c, contract))
                .ThenBy(c => (int)c.Suit)
                .ThenBy(c => (int)c.Rank)
                .First();
            return new PlayCardCommand(discard);
        }
    }

    private static int EstimateContractStrength(IReadOnlyList<Card> hand, Contract contract)
    {
        var sum = hand.Sum(c => BeloteHandRules.Points(c, contract));
        if (contract.IsSuitContract && contract.TrumpSuit is Suit trumpSuit)
        {
            var trumpCount = hand.Count(c => c.Suit == trumpSuit);
            sum += trumpCount * 3;
            if (hand.Contains(new Card(trumpSuit, Rank.Jack)))
            {
                sum += 6;
            }

            if (hand.Contains(new Card(trumpSuit, Rank.Nine)))
            {
                sum += 4;
            }
        }

        return sum;
    }

    private static List<AnnouncementClaim> FindAnnouncementClaims(BeloteHandState state, Seat seat)
    {
        var claims = new List<AnnouncementClaim>();
        if (!state.Hands.TryGetValue(seat, out var hand))
        {
            return claims;
        }

        foreach (var suit in Enum.GetValues<Suit>())
        {
            var ranks = hand.Where(c => c.Suit == suit).Select(c => c.Rank).Distinct().OrderBy(r => (int)r).ToArray();
            var bestRun = FindBestRun(ranks);
            if (bestRun.Length >= 5)
            {
                claims.Add(new AnnouncementClaim(AnnouncementKind.Quinta, suit, bestRun[^1]));
            }
            else if (bestRun.Length == 4)
            {
                claims.Add(new AnnouncementClaim(AnnouncementKind.Quarta, suit, bestRun[^1]));
            }
            else if (bestRun.Length == 3)
            {
                claims.Add(new AnnouncementClaim(AnnouncementKind.Terca, suit, bestRun[^1]));
            }
        }

        return claims;
    }

    private static Rank[] FindBestRun(Rank[] sortedDistinct)
    {
        if (sortedDistinct.Length == 0)
        {
            return Array.Empty<Rank>();
        }

        var bestStart = 0;
        var bestLen = 1;

        var start = 0;
        for (var i = 1; i < sortedDistinct.Length; i++)
        {
            if ((int)sortedDistinct[i] == (int)sortedDistinct[i - 1] + 1)
            {
                var len = i - start + 1;
                if (len > bestLen)
                {
                    bestLen = len;
                    bestStart = start;
                }
            }
            else
            {
                start = i;
            }
        }

        var run = sortedDistinct.Skip(bestStart).Take(bestLen).ToArray();
        return run;
    }
}
