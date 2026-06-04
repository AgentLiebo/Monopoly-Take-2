using System;
using System.Collections.Generic;

namespace MonopolyTake2;

public sealed class PropertyManager
{
    private readonly BoardManager _board;

    public PropertyManager(BoardManager board)
    {
        _board = board;
    }

    public Dictionary<int, PropertyState> CreateInitialPropertyStates()
    {
        var properties = new Dictionary<int, PropertyState>(_board.PurchasableSpaces.Count);
        foreach (var space in _board.PurchasableSpaces)
        {
            properties[space.Index] = new PropertyState { BoardIndex = space.Index };
        }

        return properties;
    }

    public bool IsOwned(MonopolyGameState state, int propertyIndex) => state.Properties[propertyIndex].OwnerId.HasValue;

    public bool OwnsFullSet(MonopolyGameState state, Guid playerId, ColorGroup group)
    {
        if (group is ColorGroup.None) return false;
        var spaces = _board.GetSpacesInGroup(group);
        if (spaces.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < spaces.Count; i++)
        {
            if (state.Properties[spaces[i].Index].OwnerId != playerId)
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<BoardSpaceData> GetColorSet(ColorGroup group) => _board.GetSpacesInGroup(group);

    public int CalculateRent(MonopolyGameState state, int propertyIndex, DiceRoll? utilityRoll = null, bool forceDouble = false)
    {
        var space = _board.GetSpace(propertyIndex);
        var property = state.Properties[propertyIndex];
        if (!property.OwnerId.HasValue || property.IsMortgaged)
        {
            return 0;
        }

        var ownerId = property.OwnerId.Value;
        if (space.Type == SpaceType.Railroad)
        {
            var count = CountOwnedInGroup(state, ownerId, ColorGroup.Railroad);
            var rent = space.RentValues![Math.Clamp(count - 1, 0, space.RentValues.Length - 1)];
            return forceDouble ? rent * 2 : rent;
        }

        if (space.Type == SpaceType.Utility)
        {
            var multiplier = CountOwnedInGroup(state, ownerId, ColorGroup.Utility) == 2 ? 10 : 4;
            return (utilityRoll?.Total ?? 7) * multiplier;
        }

        if (property.HasHotel)
        {
            return space.RentValues![5];
        }

        if (property.Houses > 0)
        {
            return space.RentValues![property.Houses];
        }

        var baseRent = space.BaseRent;
        return OwnsFullSet(state, ownerId, space.ColorGroup) ? baseRent * 2 : baseRent;
    }

    public bool CanBuildHouse(MonopolyGameState state, Guid playerId, int propertyIndex, out string reason)
    {
        var space = _board.GetSpace(propertyIndex);
        var property = state.Properties[propertyIndex];
        if (space.Type != SpaceType.Property)
        {
            reason = "Only color properties can be improved.";
            return false;
        }
        if (property.OwnerId != playerId)
        {
            reason = "Player does not own this property.";
            return false;
        }
        if (!OwnsFullSet(state, playerId, space.ColorGroup))
        {
            reason = "Player must own the complete color set.";
            return false;
        }
        if (property.IsMortgaged)
        {
            reason = "Mortgaged properties cannot be improved.";
            return false;
        }
        if (property.HasHotel)
        {
            reason = "Property already has a hotel.";
            return false;
        }
        var player = state.GetPlayer(playerId);
        var improvementCost = property.Houses == MonopolyRules.MaxHouses ? space.HotelCost : space.HouseCost;
        if (player.Money < improvementCost)
        {
            reason = "Insufficient money.";
            return false;
        }

        var minHouseCount = MonopolyRules.HotelHouseEquivalent;
        var colorSet = GetColorSet(space.ColorGroup);
        for (var i = 0; i < colorSet.Count; i++)
        {
            var setProperty = state.Properties[colorSet[i].Index];
            var setHouseCount = setProperty.HasHotel ? MonopolyRules.HotelHouseEquivalent : setProperty.Houses;
            minHouseCount = Math.Min(minHouseCount, setHouseCount);
        }

        var current = property.HasHotel ? MonopolyRules.HotelHouseEquivalent : property.Houses;
        if (current > minHouseCount)
        {
            reason = "Houses must be built evenly across the color set.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void BuildHouseOrHotel(MonopolyGameState state, Guid playerId, int propertyIndex)
    {
        if (!CanBuildHouse(state, playerId, propertyIndex, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        var space = _board.GetSpace(propertyIndex);
        var property = state.Properties[propertyIndex];
        var player = state.GetPlayer(playerId);
        player.Money -= property.Houses == MonopolyRules.MaxHouses ? space.HotelCost : space.HouseCost;
        if (property.Houses == MonopolyRules.MaxHouses)
        {
            property.Houses = 0;
            property.HasHotel = true;
        }
        else
        {
            property.Houses++;
        }
    }

    public bool SellImprovement(MonopolyGameState state, Guid playerId, int propertyIndex)
    {
        var property = state.Properties[propertyIndex];
        var space = _board.GetSpace(propertyIndex);
        if (property.OwnerId != playerId || !property.IsImproved)
        {
            return false;
        }

        var player = state.GetPlayer(playerId);
        if (property.HasHotel)
        {
            property.HasHotel = false;
            property.Houses = MonopolyRules.MaxHouses;
            player.Money += space.HotelCost / 2;
        }
        else
        {
            property.Houses--;
            player.Money += space.HouseCost / 2;
        }

        return true;
    }

    public bool Mortgage(MonopolyGameState state, Guid playerId, int propertyIndex)
    {
        var property = state.Properties[propertyIndex];
        var space = _board.GetSpace(propertyIndex);
        if (property.OwnerId != playerId || property.IsMortgaged || property.IsImproved)
        {
            return false;
        }

        property.IsMortgaged = true;
        state.GetPlayer(playerId).Money += space.MortgageValue;
        return true;
    }

    public bool Unmortgage(MonopolyGameState state, Guid playerId, int propertyIndex)
    {
        var property = state.Properties[propertyIndex];
        var space = _board.GetSpace(propertyIndex);
        var player = state.GetPlayer(playerId);
        var cost = MonopolyRules.MortgageRepayment(space.MortgageValue);
        if (property.OwnerId != playerId || !property.IsMortgaged || player.Money < cost)
        {
            return false;
        }

        player.Money -= cost;
        property.IsMortgaged = false;
        return true;
    }

    public void TransferProperty(MonopolyGameState state, int propertyIndex, Guid? newOwnerId)
    {
        var property = state.Properties[propertyIndex];
        if (property.OwnerId.HasValue)
        {
            state.GetPlayer(property.OwnerId.Value).OwnedPropertyIndexes.Remove(propertyIndex);
        }

        property.OwnerId = newOwnerId;
        if (newOwnerId.HasValue)
        {
            var owner = state.GetPlayer(newOwnerId.Value);
            if (!owner.OwnedPropertyIndexes.Contains(propertyIndex))
            {
                owner.OwnedPropertyIndexes.Add(propertyIndex);
            }
        }
    }

    public int CountOwnedInGroup(MonopolyGameState state, Guid ownerId, ColorGroup group)
    {
        var count = 0;
        var spaces = _board.GetSpacesInGroup(group);
        for (var i = 0; i < spaces.Count; i++)
        {
            if (state.Properties[spaces[i].Index].OwnerId == ownerId)
            {
                count++;
            }
        }

        return count;
    }
}
