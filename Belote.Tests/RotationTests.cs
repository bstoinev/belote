using Belote.Engine;
using Belote.Engine.Hand;

namespace Belote.Tests;

public sealed class RotationTests
{
    [Fact]
    public void Rotation_Ccw_DealerCutterEldest_AreCorrect()
    {
        var dealer = Seat.North;
        var state = BeloteHandState.CreateNew(seed: 7, dealer);

        Assert.Equal(Seat.East, state.Cutter);
        Assert.Equal(Seat.West, state.Eldest);
        Assert.Equal(state.Cutter, state.Turn);

        Apply(ref state, state.Cutter, new CutCommand());

        Assert.Equal(BeloteHandPhase.Bidding, state.Phase);
        Assert.Equal(state.Eldest, state.Turn);

        // End bidding quickly: eldest bids, others pass.
        Apply(ref state, state.Turn, new BidCommand(new Contract(ContractKind.Clubs)));
        Apply(ref state, state.Turn, new PassCommand());
        Apply(ref state, state.Turn, new PassCommand());
        Apply(ref state, state.Turn, new PassCommand());

        Assert.Equal(BeloteHandPhase.Playing, state.Phase);
        Assert.Equal(state.Eldest, state.Turn);

        // First card played by eldest; turn advances CCW.
        var eldest = state.Turn;
        var card = state.Hands[eldest][0];
        Apply(ref state, eldest, new PlayCardCommand(card));
        Assert.Equal(eldest.NextCcw(), state.Turn);
    }

    private static void Apply(ref BeloteHandState state, Seat actor, BeloteCommand cmd)
    {
        var ok = BeloteHandEngine.TryApply(state, actor, cmd, out var res, out var rej);
        Assert.True(ok, rej?.Code);
        state = res.State;
    }
}
