using System;
using System.Collections.Generic;

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

        var evaluatedGroups = new HashSet<ColorGroup>();
        for (var i = 0; i < ai.OwnedPropertyIndexes.Count; i++)
        {
            var group = _board.GetSpace(ai.OwnedPropertyIndexes[i]).ColorGroup;
            if (group is ColorGroup.None or ColorGroup.Railroad or ColorGroup.Utility || !evaluatedGroups.Add(group))
            {
                continue;
            }

            TryBuildInGroup(state, ai, group);
        }

        if (ai.Money < 100)
        {
            var ownedSnapshot = new List<int>(ai.OwnedPropertyIndexes);
            for (var i = 0; i < ownedSnapshot.Count && ai.Money < 100; i++)
            {
                var property = state.Properties[ownedSnapshot[i]];
                if (!property.IsMortgaged && !property.IsImproved)
                {
                    _properties.Mortgage(state, ai.Id, ownedSnapshot[i]);
                }
            }
        }
    }

    public TradeProposal? ProposeSimpleTrade(MonopolyGameState state, PlayerState ai)
    {
        if (!ai.IsAi || ai.Money < 200) return null;

        foreach (var missing in state.Properties.Values)
        {
            if (!missing.OwnerId.HasValue || missing.OwnerId == ai.Id || missing.IsImproved)
            {
                continue;
            }

            var space = _board.GetSpace(missing.BoardIndex);
            if (space.ColorGroup is ColorGroup.None or ColorGroup.Railroad or ColorGroup.Utility) continue;
            var ownsSome = _properties.CountOwnedInGroup(state, ai.Id, space.ColorGroup) > 0;
            if (!ownsSome) continue;

            var owner = state.GetPlayer(missing.OwnerId.Value);
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

    private void TryBuildInGroup(MonopolyGameState state, PlayerState ai, ColorGroup group)
    {
        if (!_properties.OwnsFullSet(state, ai.Id, group))
        {
            return;
        }

        PropertyState? buildTarget = null;
        var buildTargetRank = MonopolyRules.HotelHouseEquivalent + 1;
        var colorSet = _properties.GetColorSet(group);
        for (var i = 0; i < colorSet.Count; i++)
        {
            var property = state.Properties[colorSet[i].Index];
            var rank = property.HasHotel ? MonopolyRules.HotelHouseEquivalent : property.Houses;
            if (rank < buildTargetRank)
            {
                buildTarget = property;
                buildTargetRank = rank;
            }
        }

        if (buildTarget == null)
        {
            return;
        }

        var space = _board.GetSpace(buildTarget.BoardIndex);
        if (ai.Money - space.HouseCost >= 300 && _properties.CanBuildHouse(state, ai.Id, buildTarget.BoardIndex, out _))
        {
            _properties.BuildHouseOrHotel(state, ai.Id, buildTarget.BoardIndex);
        }
    }
}
