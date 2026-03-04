using System.Security.Cryptography;
using System.Text;

namespace Belote.Engine.Hand;

public static class BeloteHandCanonical
{
    public static string ComputeHash(BeloteHandState state)
    {
        var canonical = ToCanonicalString(state);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash);
        return hex;
    }

    public static string ToCanonicalString(BeloteHandState state)
    {
        var sb = new StringBuilder(4096);
        sb.Append("handId=").Append(state.HandId).Append('\n');
        sb.Append("seed=").Append(state.Seed).Append('\n');
        sb.Append("dealer=").Append(state.Dealer).Append('\n');
        sb.Append("phase=").Append(state.Phase).Append('\n');
        sb.Append("turn=").Append(state.Turn).Append('\n');
        sb.Append("contract=").Append(state.HighestContract?.Kind.ToString() ?? "null").Append('\n');
        sb.Append("highestBidder=").Append(state.HighestBidder?.ToString() ?? "null").Append('\n');
        sb.Append("doubling=").Append(state.Doubling).Append('\n');
        sb.Append("trickNumber=").Append(state.TrickNumber).Append('\n');

        foreach (var seat in Enum.GetValues<Seat>())
        {
            sb.Append("hand[").Append(seat).Append("]=");
            if (state.Hands.TryGetValue(seat, out var hand))
            {
                foreach (var card in hand.OrderBy(c => (int)c.Suit).ThenBy(c => (int)c.Rank))
                {
                    sb.Append(card.ToString()).Append(',');
                }
            }
            sb.Append('\n');
        }

        sb.Append("currentTrick=");
        foreach (var pc in state.CurrentTrick)
        {
            sb.Append(pc.Seat).Append(':').Append(pc.Card.ToString()).Append(';');
        }
        sb.Append('\n');

        sb.Append("nsWon=");
        foreach (var card in state.NorthSouthWonCards.OrderBy(c => (int)c.Suit).ThenBy(c => (int)c.Rank))
        {
            sb.Append(card.ToString()).Append(',');
        }
        sb.Append('\n');

        sb.Append("ewWon=");
        foreach (var card in state.EastWestWonCards.OrderBy(c => (int)c.Suit).ThenBy(c => (int)c.Rank))
        {
            sb.Append(card.ToString()).Append(',');
        }
        sb.Append('\n');

        sb.Append("ann=");
        foreach (var a in state.Announcements.OrderBy(a => a.Seat).ThenBy(a => a.Suit).ThenBy(a => a.HighestRank).ThenBy(a => a.Kind))
        {
            sb.Append(a.Seat).Append(':').Append(a.Kind).Append(':').Append(a.Suit).Append(':').Append(a.HighestRank).Append(';');
        }
        sb.Append('\n');

        sb.Append("belote=");
        foreach (var b in state.Belotes.OrderBy(b => b.Seat))
        {
            sb.Append(b.Seat).Append(':').Append(b.TrumpSuit).Append(';');
        }
        sb.Append('\n');

        sb.Append("outcome=");
        if (state.Outcome is null)
        {
            sb.Append("null\n");
        }
        else
        {
            sb.Append(state.Outcome.WasCanceledAllPass ? "allpass" : "played").Append('|')
                .Append(state.Outcome.Contract?.Kind.ToString() ?? "null").Append('|')
                .Append(state.Outcome.Doubling).Append('|')
                .Append(state.Outcome.BiddingTeam?.ToString() ?? "null").Append('|')
                .Append(state.Outcome.InsideRuleApplied ? "inside" : "normal").Append('|')
                .Append(state.Outcome.NorthSouthAwardedPoints).Append('|')
                .Append(state.Outcome.EastWestAwardedPoints).Append('|')
                .Append(state.Outcome.NextDealer).Append('\n');
        }

        return sb.ToString();
    }
}

