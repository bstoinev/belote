namespace Belote.Engine;

public enum Suit
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3,
}

public enum Rank
{
    Seven = 0,
    Eight = 1,
    Nine = 2,
    Ten = 3,
    Jack = 4,
    Queen = 5,
    King = 6,
    Ace = 7,
}

public readonly record struct Card(Suit Suit, Rank Rank)
{
    public override string ToString()
    {
        // Culture-invariant compact representation.
        var suit = Suit switch
        {
            Suit.Clubs => "C",
            Suit.Diamonds => "D",
            Suit.Hearts => "H",
            Suit.Spades => "S",
            _ => "?",
        };

        var rank = Rank switch
        {
            Rank.Seven => "7",
            Rank.Eight => "8",
            Rank.Nine => "9",
            Rank.Ten => "10",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => "?",
        };

        return $"{suit}{rank}";
    }

    public static bool TryParse(string text, out Card card)
    {
        card = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().ToUpperInvariant();
        var suitChar = trimmed[0];
        var suit = suitChar switch
        {
            'C' => Suit.Clubs,
            'D' => Suit.Diamonds,
            'H' => Suit.Hearts,
            'S' => Suit.Spades,
            _ => (Suit?)null,
        };

        if (suit is null)
        {
            return false;
        }

        var rankText = trimmed[1..];
        var rank = rankText switch
        {
            "7" => Rank.Seven,
            "8" => Rank.Eight,
            "9" => Rank.Nine,
            "10" => Rank.Ten,
            "J" => Rank.Jack,
            "Q" => Rank.Queen,
            "K" => Rank.King,
            "A" => Rank.Ace,
            _ => (Rank?)null,
        };

        if (rank is null)
        {
            return false;
        }

        card = new Card(suit.Value, rank.Value);
        return true;
    }
}

public static class Deck32
{
    public static Card[] CreateOrdered()
    {
        var cards = new List<Card>(32);
        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                cards.Add(new Card(suit, rank));
            }
        }

        return cards.ToArray();
    }
}

