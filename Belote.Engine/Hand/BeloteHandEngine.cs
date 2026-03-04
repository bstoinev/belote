namespace Belote.Engine.Hand;

public static class BeloteHandEngine
{
    public static bool TryApply(BeloteHandState state, Seat actor, BeloteCommand command, out BeloteHandEngineResult result, out EngineRejection? rejection)
    {
        result = new BeloteHandEngineResult(state, Array.Empty<EngineEvent>());
        rejection = null;

        if (state.Outcome is not null || state.Phase is BeloteHandPhase.CanceledAllPass or BeloteHandPhase.Completed)
        {
            rejection = new EngineRejection("HAND_ENDED", "The hand has already ended.");
            return false;
        }

        if (actor != state.Turn)
        {
            rejection = new EngineRejection("OUT_OF_TURN", "It is not your turn.");
            return false;
        }

        var events = new List<EngineEvent>();
        BeloteHandState next;

        if (state.Phase == BeloteHandPhase.AwaitingCut)
        {
            if (command is not CutCommand)
            {
                rejection = new EngineRejection("EXPECTED_CUT", "Expected Cut.");
                return false;
            }

            // Cut: shuffle, apply symbolic cut, deal, start bidding.
            var deck = BeloteHandRules.CreateShuffledDeck(state.Seed);
            deck = BeloteHandRules.ApplySymbolicCut(deck);
            var dealt = BeloteHandRules.Deal(deck, state.Dealer);
            next = state with
            {
                Phase = BeloteHandPhase.Bidding,
                Turn = state.Eldest,
                Hands = dealt,
                InitialHands = dealt,
                BiddingLog = [.. state.BiddingLog, new BidEntry(actor, "CUT", null, state.Doubling)],
            };
            events.Add(new EngineEvent("CUT", new Dictionary<string, string> { ["seat"] = actor.ToString() }));

            result = new BeloteHandEngineResult(next, events);
            return true;
        }

        if (state.Phase == BeloteHandPhase.Bidding)
        {
            var ok = TryApplyBidding(state, actor, command, events, out next, out rejection);
            if (!ok)
            {
                return false;
            }

            result = new BeloteHandEngineResult(next, events);
            return true;
        }

        if (state.Phase == BeloteHandPhase.Playing)
        {
            var ok = TryApplyPlaying(state, actor, command, events, out next, out rejection);
            if (!ok)
            {
                return false;
            }

            result = new BeloteHandEngineResult(next, events);
            return true;
        }

        rejection = new EngineRejection("INVALID_PHASE", "Command not valid in current phase.");
        return false;
    }

    private static bool TryApplyBidding(BeloteHandState state, Seat actor, BeloteCommand command, List<EngineEvent> events, out BeloteHandState next, out EngineRejection? rejection)
    {
        next = state;
        rejection = null;

        if (command is PassCommand)
        {
            var log = new List<BidEntry>(state.BiddingLog) { new(actor, "PASS", null, state.Doubling) };
            if (state.HighestContract is null)
            {
                var passes = state.PassesBeforeAnyBid + 1;
                if (passes >= 4)
                {
                    var outcome = new HandOutcome(
                        WasCanceledAllPass: true,
                        Contract: null,
                        Doubling: Doubling.None,
                        BiddingTeam: null,
                        InsideRuleApplied: false,
                        NorthSouthAwardedPoints: 0,
                        EastWestAwardedPoints: 0,
                        NextDealer: state.Dealer.NextCcw());

                    next = state with
                    {
                        Phase = BeloteHandPhase.CanceledAllPass,
                        Outcome = outcome,
                        BiddingLog = log,
                        PassesBeforeAnyBid = passes,
                    };
                    events.Add(new EngineEvent("BID_ALL_PASS", new Dictionary<string, string>()));
                    return true;
                }

                next = state with
                {
                    Turn = state.Turn.NextCcw(),
                    BiddingLog = log,
                    PassesBeforeAnyBid = passes,
                };
                events.Add(new EngineEvent("BID_PASS", new Dictionary<string, string> { ["seat"] = actor.ToString() }));
                return true;
            }

            var afterBid = state.PassesAfterLastBid + 1;
            if (afterBid >= 3)
            {
                // Bidding ends.
                next = state with
                {
                    Phase = BeloteHandPhase.Playing,
                    Turn = state.Eldest,
                    BiddingLog = log,
                    PassesAfterLastBid = afterBid,
                    TrickNumber = 0,
                    CurrentTrick = Array.Empty<PlayedCard>(),
                };
                events.Add(new EngineEvent("BID_END", new Dictionary<string, string>()));
                return true;
            }

            next = state with
            {
                Turn = state.Turn.NextCcw(),
                BiddingLog = log,
                PassesAfterLastBid = afterBid,
            };
            events.Add(new EngineEvent("BID_PASS", new Dictionary<string, string> { ["seat"] = actor.ToString() }));
            return true;
        }

        if (command is BidCommand bid)
        {
            var currentStrength = state.HighestContract is null ? -1 : BeloteHandRules.ContractStrength(state.HighestContract.Value);
            var newStrength = BeloteHandRules.ContractStrength(bid.Contract);
            if (newStrength <= currentStrength)
            {
                rejection = new EngineRejection("BID_NOT_HIGHER", "Bid must be strictly higher than the current highest bid.");
                return false;
            }

            var log = new List<BidEntry>(state.BiddingLog) { new(actor, "BID", bid.Contract, state.Doubling) };
            next = state with
            {
                HighestContract = bid.Contract,
                HighestBidder = actor,
                Turn = state.Turn.NextCcw(),
                BiddingLog = log,
                PassesAfterLastBid = 0,
            };
            events.Add(new EngineEvent("BID_MADE", new Dictionary<string, string> { ["seat"] = actor.ToString(), ["contract"] = bid.Contract.Kind.ToString() }));
            return true;
        }

        if (command is ContraCommand)
        {
            if (state.HighestContract is null || state.HighestBidder is null)
            {
                rejection = new EngineRejection("CONTRA_NO_BID", "Contra is only allowed after a bid exists.");
                return false;
            }

            if (state.Doubling != Doubling.None)
            {
                rejection = new EngineRejection("CONTRA_ALREADY", "Contra/Recontra has already been applied.");
                return false;
            }

            var biddingTeam = state.HighestBidder.Value.Team();
            if (actor.Team() == biddingTeam)
            {
                rejection = new EngineRejection("CONTRA_WRONG_TEAM", "Contra may only be called by the defending team.");
                return false;
            }

            var log = new List<BidEntry>(state.BiddingLog) { new(actor, "CONTRA", null, Doubling.Contra) };
            next = state with
            {
                Doubling = Doubling.Contra,
                Turn = state.Turn.NextCcw(),
                BiddingLog = log,
            };
            events.Add(new EngineEvent("BID_CONTRA", new Dictionary<string, string> { ["seat"] = actor.ToString() }));
            return true;
        }

        if (command is RecontraCommand)
        {
            if (state.HighestContract is null || state.HighestBidder is null)
            {
                rejection = new EngineRejection("RECONTRA_NO_BID", "Recontra is only allowed after a bid exists.");
                return false;
            }

            if (state.Doubling != Doubling.Contra)
            {
                rejection = new EngineRejection("RECONTRA_NOT_ALLOWED", "Recontra is only allowed after Contra.");
                return false;
            }

            var biddingTeam = state.HighestBidder.Value.Team();
            if (actor.Team() != biddingTeam)
            {
                rejection = new EngineRejection("RECONTRA_WRONG_TEAM", "Recontra may only be called by the bidding team.");
                return false;
            }

            var log = new List<BidEntry>(state.BiddingLog) { new(actor, "RECONTRA", null, Doubling.Recontra) };
            next = state with
            {
                Doubling = Doubling.Recontra,
                Turn = state.Turn.NextCcw(),
                BiddingLog = log,
            };
            events.Add(new EngineEvent("BID_RECONTRA", new Dictionary<string, string> { ["seat"] = actor.ToString() }));
            return true;
        }

        rejection = new EngineRejection("BID_UNKNOWN_CMD", "Command not valid during bidding.");
        return false;
    }

    private static bool TryApplyPlaying(BeloteHandState state, Seat actor, BeloteCommand command, List<EngineEvent> events, out BeloteHandState next, out EngineRejection? rejection)
    {
        next = state;
        rejection = null;

        if (state.HighestContract is null || state.HighestBidder is null)
        {
            rejection = new EngineRejection("NO_CONTRACT", "Playing cannot start without a contract.");
            return false;
        }

        if (command is DeclareAnnouncementsCommand declare)
        {
            if (actor != state.Eldest || state.TrickNumber != 0 || state.CurrentTrick.Count != 0)
            {
                rejection = new EngineRejection("DECLARATION_WINDOW", "Announcements may only be declared by eldest hand before the first card of the first trick.");
                return false;
            }

            if (state.AnnouncementsDeclared)
            {
                rejection = new EngineRejection("DECLARATION_ALREADY", "Announcements have already been declared.");
                return false;
            }

            var contract = state.HighestContract.Value;
            if (contract.Kind == ContractKind.NoTrump)
            {
                rejection = new EngineRejection("DECLARATION_NOT_ALLOWED", "Announcements are not allowed in No Trump.");
                return false;
            }

            var awards = ValidateAndAwardAnnouncements(state, actor, declare.Claims, out rejection);
            if (awards is null)
            {
                return false;
            }

            var newList = new List<AnnouncementAward>(state.Announcements);
            newList.AddRange(awards);
            next = state with
            {
                AnnouncementsDeclared = true,
                Announcements = newList,
            };
            foreach (var a in awards)
            {
                events.Add(new EngineEvent("ANNOUNCEMENT", new Dictionary<string, string>
                {
                    ["seat"] = a.Seat.ToString(),
                    ["kind"] = a.Kind.ToString(),
                    ["suit"] = a.Suit.ToString(),
                    ["highestRank"] = a.HighestRank.ToString(),
                    ["points"] = a.Points.ToString(),
                }));
            }

            return true;
        }

        if (command is CollectTrickCommand)
        {
            if (state.CurrentTrick.Count != 4)
            {
                rejection = new EngineRejection("TRICK_NOT_COMPLETE", "Cannot collect trick until 4 cards are played.");
                return false;
            }

            next = ResolveTrick(state, events);
            return true;
        }

        if (command is PlayCardCommand play)
        {
            if (state.CurrentTrick.Count == 4)
            {
                rejection = new EngineRejection("TRICK_NEEDS_COLLECT", "Collect the completed trick before playing further cards.");
                return false;
            }

            if (!state.Hands.TryGetValue(actor, out var hand))
            {
                rejection = new EngineRejection("HAND_MISSING", "Hand not found.");
                return false;
            }

            if (!hand.Contains(play.Card))
            {
                rejection = new EngineRejection("CARD_NOT_IN_HAND", "Card is not in hand.");
                return false;
            }

            var legal = BeloteHandRules.GetLegalPlays(state, actor);
            if (!legal.Contains(play.Card))
            {
                rejection = new EngineRejection("ILLEGAL_PLAY", "Card is not a legal play.");
                return false;
            }

            var newHand = hand.Where(c => c != play.Card).ToArray();
            var hands = new Dictionary<Seat, IReadOnlyList<Card>>(state.Hands) { [actor] = newHand };
            var currentTrick = new List<PlayedCard>(state.CurrentTrick) { new(actor, play.Card) };

            var belotes = new List<BeloteAward>(state.Belotes);
            var contract = state.HighestContract.Value;
            if (contract.IsSuitContract && contract.TrumpSuit is Suit trumpSuit)
            {
                var maybeAward = TryAutoBeloteAward(state, actor, play.Card, trumpSuit);
                if (maybeAward is not null)
                {
                    belotes.Add(maybeAward);
                    events.Add(new EngineEvent("BELOTE", new Dictionary<string, string>
                    {
                        ["seat"] = actor.ToString(),
                        ["suit"] = trumpSuit.ToString(),
                        ["points"] = maybeAward.Points.ToString(),
                    }));
                }
            }

            next = state with
            {
                Hands = hands,
                CurrentTrick = currentTrick,
                Turn = state.Turn.NextCcw(),
                Belotes = belotes,
            };
            events.Add(new EngineEvent("PLAY", new Dictionary<string, string> { ["seat"] = actor.ToString(), ["card"] = play.Card.ToString() }));

            if (currentTrick.Count == 4)
            {
                // Trick collection is a separate command to allow UIs to show the 4 cards before clearing.
                // Turn cycles back to the leader after the 4th play (CCW), which is already true with NextCcw().
                events.Add(new EngineEvent("TRICK_READY", new Dictionary<string, string> { ["trick"] = state.TrickNumber.ToString() }));
            }

            return true;
        }

        rejection = new EngineRejection("PLAY_UNKNOWN_CMD", "Command not valid during play.");
        return false;
    }

    private static IReadOnlyList<AnnouncementAward>? ValidateAndAwardAnnouncements(BeloteHandState state, Seat seat, IReadOnlyList<AnnouncementClaim> claims, out EngineRejection? rejection)
    {
        rejection = null;
        if (!state.Hands.TryGetValue(seat, out var hand))
        {
            rejection = new EngineRejection("HAND_MISSING", "Hand not found.");
            return null;
        }

        var awards = new List<AnnouncementAward>();
        foreach (var claim in claims)
        {
            if (claim.Kind is not (AnnouncementKind.Terca or AnnouncementKind.Quarta or AnnouncementKind.Quinta))
            {
                rejection = new EngineRejection("ANN_INVALID_KIND", "Invalid announcement kind.");
                return null;
            }

            var length = (int)claim.Kind;
            var highestIdx = (int)claim.HighestRank;
            var startIdx = highestIdx - (length - 1);
            if (startIdx < (int)Rank.Seven)
            {
                rejection = new EngineRejection("ANN_INVALID_RANGE", "Invalid sequence range.");
                return null;
            }

            var neededRanks = Enumerable.Range(startIdx, length).Select(i => (Rank)i).ToArray();
            var ok = neededRanks.All(r => hand.Contains(new Card(claim.Suit, r)));
            if (!ok)
            {
                rejection = new EngineRejection("ANN_NOT_IN_HAND", "Declared announcement does not exist in hand.");
                return null;
            }

            var points = BeloteHandRules.AnnouncementPoints(claim.Kind);
            awards.Add(new AnnouncementAward(seat, claim.Kind, claim.Suit, claim.HighestRank, points));
        }

        return awards;
    }

    private static BeloteAward? TryAutoBeloteAward(BeloteHandState state, Seat seat, Card playedCard, Suit trumpSuit)
    {
        if (playedCard.Suit != trumpSuit)
        {
            return null;
        }

        if (playedCard.Rank is not (Rank.Queen or Rank.King))
        {
            return null;
        }

        var already = state.Belotes.Any(b => b.Seat == seat);
        if (already)
        {
            return null;
        }

        if (!state.InitialHands.TryGetValue(seat, out var initial))
        {
            return null;
        }

        var otherRank = playedCard.Rank == Rank.Queen ? Rank.King : Rank.Queen;
        var hasBothOriginally = initial.Contains(new Card(trumpSuit, otherRank));
        if (!hasBothOriginally)
        {
            return null;
        }

        return new BeloteAward(seat, trumpSuit, 20);
    }

    private static BeloteHandState ResolveTrick(BeloteHandState state, List<EngineEvent> events)
    {
        var contract = state.HighestContract!.Value;
        var leadSuit = state.CurrentTrick[0].Card.Suit;
        var (winnerSeat, _) = BeloteHandRules.GetCurrentWinningCard(state.CurrentTrick, contract, leadSuit);

        var trickCards = state.CurrentTrick.Select(pc => pc.Card).ToArray();
        var completed = new List<TrickResult>(state.CompletedTricks)
        {
            new TrickResult(state.TrickNumber, winnerSeat, state.CurrentTrick.ToArray())
        };

        var nsWon = new List<Card>(state.NorthSouthWonCards);
        var ewWon = new List<Card>(state.EastWestWonCards);
        if (winnerSeat.Team() == Team.NorthSouth)
        {
            nsWon.AddRange(trickCards);
        }
        else
        {
            ewWon.AddRange(trickCards);
        }

        var nextTrickNumber = state.TrickNumber + 1;
        var next = state with
        {
            Turn = winnerSeat,
            TrickNumber = nextTrickNumber,
            CurrentTrick = Array.Empty<PlayedCard>(),
            CompletedTricks = completed,
            NorthSouthWonCards = nsWon,
            EastWestWonCards = ewWon,
        };

        events.Add(new EngineEvent("TRICK_WON", new Dictionary<string, string> { ["seat"] = winnerSeat.ToString(), ["trick"] = state.TrickNumber.ToString() }));

        if (nextTrickNumber >= 8)
        {
            next = CompleteHand(next, winnerSeat, events);
        }

        return next;
    }

    private static BeloteHandState CompleteHand(BeloteHandState state, Seat lastTrickWinner, List<EngineEvent> events)
    {
        var contract = state.HighestContract!.Value;
        var doubling = state.Doubling;
        var biddingTeam = state.HighestBidder!.Value.Team();

        var nsTrickPoints = state.NorthSouthWonCards.Sum(c => BeloteHandRules.Points(c, contract));
        var ewTrickPoints = state.EastWestWonCards.Sum(c => BeloteHandRules.Points(c, contract));

        if (lastTrickWinner.Team() == Team.NorthSouth)
        {
            nsTrickPoints += 10;
        }
        else
        {
            ewTrickPoints += 10;
        }

        var nsBonus = state.Announcements.Where(a => a.Seat.Team() == Team.NorthSouth).Sum(a => a.Points)
            + state.Belotes.Where(b => b.Seat.Team() == Team.NorthSouth).Sum(b => b.Points);
        var ewBonus = state.Announcements.Where(a => a.Seat.Team() == Team.EastWest).Sum(a => a.Points)
            + state.Belotes.Where(b => b.Seat.Team() == Team.EastWest).Sum(b => b.Points);

        var insideApplied = false;
        var nsAward = 0;
        var ewAward = 0;
        var multiplier = BeloteHandRules.DoublingMultiplier(doubling);

        var biddingTrick = biddingTeam == Team.NorthSouth ? nsTrickPoints : ewTrickPoints;
        var defendingTrick = biddingTeam == Team.NorthSouth ? ewTrickPoints : nsTrickPoints;

        if (biddingTrick < defendingTrick)
        {
            insideApplied = true;
            var total = nsTrickPoints + ewTrickPoints + nsBonus + ewBonus;
            if (biddingTeam == Team.NorthSouth)
            {
                ewAward = total * multiplier;
                nsAward = 0;
            }
            else
            {
                nsAward = total * multiplier;
                ewAward = 0;
            }
        }
        else
        {
            if (biddingTeam == Team.NorthSouth)
            {
                nsAward = (nsTrickPoints + nsBonus) * multiplier;
                ewAward = ewTrickPoints + ewBonus;
            }
            else
            {
                ewAward = (ewTrickPoints + ewBonus) * multiplier;
                nsAward = nsTrickPoints + nsBonus;
            }
        }

        var outcome = new HandOutcome(
            WasCanceledAllPass: false,
            Contract: contract,
            Doubling: doubling,
            BiddingTeam: biddingTeam,
            InsideRuleApplied: insideApplied,
            NorthSouthAwardedPoints: nsAward,
            EastWestAwardedPoints: ewAward,
            NextDealer: state.Dealer.NextCcw());

        events.Add(new EngineEvent("HAND_COMPLETE", new Dictionary<string, string>
        {
            ["nsAward"] = nsAward.ToString(),
            ["ewAward"] = ewAward.ToString(),
            ["inside"] = insideApplied ? "1" : "0",
            ["contract"] = contract.Kind.ToString(),
            ["doubling"] = doubling.ToString(),
        }));

        var next = state with
        {
            Phase = BeloteHandPhase.Completed,
            Outcome = outcome,
            Turn = outcome.NextDealer, // no longer used, but stable.
        };

        return next;
    }
}
