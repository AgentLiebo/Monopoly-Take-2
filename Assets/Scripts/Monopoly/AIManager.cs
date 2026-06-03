using System;
using System.Linq;

namespace MonopolyTake2;

public sealed class AIManager
{
    private readonly BoardManager _board;
    private readonly PropertyManager _properties;

    public AIManager(BoardManager board, PropertyManager properties)
    {
        _board = board;
        _properties = properties;
    }

    public bool ShouldBuyProperty(MonopolyGameState state, PlayerState ai, int propertyIndex)
    {
        var space = _board.GetSpace(propertyIndex);
        if (!ai.IsAi || !space.IsPurchasable || ai.Money < space.Price)
        {
            return false;
        }

        var reserve = space.ColorGroup is ColorGroup.DarkBlue or ColorGroup.Green ? 250 : 150;
        var setOwned = _properties.CountOwnedInGroup(state, ai.Id, space.ColorGroup);
        return ai.Money - space.Price >= reserve || setOwned > 0;
    }

    public int ChooseAuctionBid(MonopolyGameState state, PlayerState ai, int propertyIndex, int currentBid)
    {
        if (!ai.IsAi || ai.Bankrupt)
        {
            return 0;
        }

        var space = _board.GetSpace(propertyIndex);
        var desiredMax = Math.Min(ai.Money - 100, space.Price + (_properties.CountOwnedInGroup(state, ai.Id, space.ColorGroup) * 50));
        var nextBid = currentBid + 10;
        return nextBid <= desiredMax ? nextBid : 0;
    }

    public void ManageAssets(MonopolyGameState state, PlayerState ai)
    {
        if (!ai.IsAi || ai.Bankrupt)
        {
            return;
        }

        foreach (var group in ai.OwnedPropertyIndexes.Select(i => _board.GetSpace(i).ColorGroup).Distinct())
        {
            if (group is ColorGroup.None or ColorGroup.Railroad or ColorGroup.Utility) continue;
            if (!_properties.OwnsFullSet(state, ai.Id, group)) continue;

            var buildTarget = _properties.GetColorSet(group)
                .Select(s => state.Properties[s.Index])
                .OrderBy(p => p.HasHotel ? MonopolyRules.HotelHouseEquivalent : p.Houses)
                .FirstOrDefault();
            if (buildTarget == null) continue;
            var space = _board.GetSpace(buildTarget.BoardIndex);
            if (ai.Money - space.HouseCost >= 300 && _properties.CanBuildHouse(state, ai.Id, buildTarget.BoardIndex, out _))
            {
                _properties.BuildHouseOrHotel(state, ai.Id, buildTarget.BoardIndex);
            }
        }

        if (ai.Money < 100)
        {
            foreach (var index in ai.OwnedPropertyIndexes.Where(i => !state.Properties[i].IsMortgaged && !state.Properties[i].IsImproved).ToList())
            {
                if (ai.Money >= 100) break;
                _properties.Mortgage(state, ai.Id, index);
            }
        }
    }

    public TradeProposal? ProposeSimpleTrade(MonopolyGameState state, PlayerState ai)
    {
        if (!ai.IsAi || ai.Money < 200) return null;

        foreach (var missing in state.Properties.Values.Where(p => p.OwnerId.HasValue && p.OwnerId != ai.Id && !p.IsImproved))
        {
            var space = _board.GetSpace(missing.BoardIndex);
            if (space.ColorGroup is ColorGroup.None or ColorGroup.Railroad or ColorGroup.Utility) continue;
            var ownsSome = _properties.CountOwnedInGroup(state, ai.Id, space.ColorGroup) > 0;
            if (!ownsSome) continue;

            var owner = state.Players.Single(p => p.Id == missing.OwnerId!.Value);
            var cashOffer = Math.Min(ai.Money - 100, space.Price + 50);
            return new TradeProposal
            {
                FromPlayerId = ai.Id,
                ToPlayerId = owner.Id,
                OfferedByFromPlayer = new TradeOffer { Money = cashOffer },
                OfferedByToPlayer = new TradeOffer { PropertyIndexes = { missing.BoardIndex } }
            };
        }

        return null;
    }
}
