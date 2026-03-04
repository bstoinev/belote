namespace Belote.Engine;

public enum Seat
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public enum Team
{
    NorthSouth = 0,
    EastWest = 1,
}

public static class SeatExtensions
{
    public static Team Team(this Seat seat)
    {
        var team = seat is Seat.North or Seat.South ? Belote.Engine.Team.NorthSouth : Belote.Engine.Team.EastWest;
        return team;
    }

    /// <summary>Next seat in counter-clockwise (CCW) order.</summary>
    public static Seat NextCcw(this Seat seat)
    {
        var next = seat switch
        {
            Seat.North => Seat.West,
            Seat.West => Seat.South,
            Seat.South => Seat.East,
            Seat.East => Seat.North,
            _ => throw new ArgumentOutOfRangeException(nameof(seat), seat, "Unknown seat."),
        };

        return next;
    }

    /// <summary>Previous seat in counter-clockwise (CCW) order.</summary>
    public static Seat PrevCcw(this Seat seat)
    {
        var prev = seat switch
        {
            Seat.North => Seat.East,
            Seat.East => Seat.South,
            Seat.South => Seat.West,
            Seat.West => Seat.North,
            _ => throw new ArgumentOutOfRangeException(nameof(seat), seat, "Unknown seat."),
        };

        return prev;
    }
}

