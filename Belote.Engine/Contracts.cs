namespace Belote.Engine;

public enum ContractKind
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3,
    NoTrump = 4,
    AllTrump = 5,
}

public enum Doubling
{
    None = 1,
    Contra = 2,
    Recontra = 4,
}

public readonly record struct Contract(ContractKind Kind)
{
    public Suit? TrumpSuit
    {
        get
        {
            Suit? suit = Kind switch
            {
                ContractKind.Clubs => Suit.Clubs,
                ContractKind.Diamonds => Suit.Diamonds,
                ContractKind.Hearts => Suit.Hearts,
                ContractKind.Spades => Suit.Spades,
                _ => null,
            };

            return suit;
        }
    }

    public bool IsSuitContract
    {
        get
        {
            var isSuit = TrumpSuit is not null;
            return isSuit;
        }
    }
}

