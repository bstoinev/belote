using Belote.Engine;
using Belote.Engine.Hand;

namespace Belote.Tests;

public sealed class BiddingTests
{
    [Fact]
    public void Bidding_Ladder_AllowsPassThenLaterHigherBid()
    {
        var dealer = Seat.North;
        var state = BeloteHandState.CreateNew(seed: 123, dealer);

        Apply(ref state, state.Turn, new CutCommand());

        Assert.Equal(BeloteHandPhase.Bidding, state.Phase);
        Assert.Equal(state.Eldest, state.Turn);

        var eldest = state.Eldest; // West (with dealer North)
        var south = eldest.NextCcw();
        var east = south.NextCcw();
        var north = east.NextCcw();

        Apply(ref state, eldest, new PassCommand());
        Apply(ref state, south, new BidCommand(new Contract(ContractKind.Clubs)));
        Apply(ref state, east, new PassCommand());
        Apply(ref state, north, new PassCommand());

        Assert.Equal(eldest, state.Turn);
        Assert.Equal(new Contract(ContractKind.Clubs), state.HighestContract);

        Apply(ref state, eldest, new BidCommand(new Contract(ContractKind.Diamonds)));

        Assert.Equal(new Contract(ContractKind.Diamonds), state.HighestContract);
        Assert.Equal(eldest, state.HighestBidder);
    }

    [Fact]
    public void Bidding_EndsAfterThreePassesAfterLastBid()
    {
        var state = BeloteHandState.CreateNew(seed: 1, dealer: Seat.North);
        Apply(ref state, state.Turn, new CutCommand());

        var eldest = state.Eldest;
        Apply(ref state, eldest, new BidCommand(new Contract(ContractKind.Clubs)));
        Apply(ref state, eldest.NextCcw(), new PassCommand());
        Apply(ref state, eldest.NextCcw().NextCcw(), new PassCommand());
        Apply(ref state, eldest.NextCcw().NextCcw().NextCcw(), new PassCommand());

        Assert.Equal(BeloteHandPhase.Playing, state.Phase);
        Assert.Equal(eldest, state.Turn);
        Assert.Equal(new Contract(ContractKind.Clubs), state.HighestContract);
    }

    [Fact]
    public void AllPass_CancelsHand_AndRotatesDealer()
    {
        var dealer = Seat.North;
        var state = BeloteHandState.CreateNew(seed: 2, dealer);
        Apply(ref state, state.Turn, new CutCommand());

        var start = state.Eldest;
        Apply(ref state, start, new PassCommand());
        Apply(ref state, start.NextCcw(), new PassCommand());
        Apply(ref state, start.NextCcw().NextCcw(), new PassCommand());
        Apply(ref state, start.NextCcw().NextCcw().NextCcw(), new PassCommand());

        Assert.Equal(BeloteHandPhase.CanceledAllPass, state.Phase);
        Assert.NotNull(state.Outcome);
        Assert.True(state.Outcome!.WasCanceledAllPass);
        Assert.Equal(dealer.NextCcw(), state.Outcome.NextDealer);
    }

    private static void Apply(ref BeloteHandState state, Seat actor, BeloteCommand cmd)
    {
        var ok = BeloteHandEngine.TryApply(state, actor, cmd, out var res, out var rej);
        Assert.True(ok, rej?.Code);
        state = res.State;
    }
}

