using System;
using System.Collections.Generic;
using System.Linq;

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
        return _board.Spaces
            .Where(s => s.IsPurchasable)
            .ToDictionary(s => s.Index, s => new PropertyState { BoardIndex = s.Index });
    }

    public bool IsOwned(MonopolyGameState state, int propertyIndex) => state.Properties[propertyIndex].OwnerId.HasValue;

    public bool OwnsFullSet(MonopolyGameState state, Guid playerId, ColorGroup group)
    {
        if (group is ColorGroup.None) return false;
        return _board.Spaces
            .Where(s => s.ColorGroup == group && s.IsPurchasable)
            .All(s => state.Properties[s.Index].OwnerId == playerId);
    }

    public IEnumerable<BoardSpaceData> GetColorSet(ColorGroup group) => _board.Spaces.Where(s => s.ColorGroup == group && s.IsPurchasable);

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
        if (state.Players.Single(p => p.Id == playerId).Money < space.HouseCost)
        {
            reason = "Insufficient money.";
            return false;
        }

        var set = GetColorSet(space.ColorGroup).Select(s => state.Properties[s.Index]).ToList();
        var minHouseCount = set.Min(p => p.HasHotel ? MonopolyRules.HotelHouseEquivalent : p.Houses);
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
        var player = state.Players.Single(p => p.Id == playerId);
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

        var player = state.Players.Single(p => p.Id == playerId);
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
        state.Players.Single(p => p.Id == playerId).Money += space.MortgageValue;
        return true;
    }

    public bool Unmortgage(MonopolyGameState state, Guid playerId, int propertyIndex)
    {
        var property = state.Properties[propertyIndex];
        var space = _board.GetSpace(propertyIndex);
        var player = state.Players.Single(p => p.Id == playerId);
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
            state.Players.Single(p => p.Id == property.OwnerId.Value).OwnedPropertyIndexes.Remove(propertyIndex);
        }

        property.OwnerId = newOwnerId;
        if (newOwnerId.HasValue)
        {
            var owner = state.Players.Single(p => p.Id == newOwnerId.Value);
            if (!owner.OwnedPropertyIndexes.Contains(propertyIndex))
            {
                owner.OwnedPropertyIndexes.Add(propertyIndex);
            }
        }
    }

    public int CountOwnedInGroup(MonopolyGameState state, Guid ownerId, ColorGroup group)
    {
        return _board.Spaces.Count(s => s.ColorGroup == group && s.IsPurchasable && state.Properties[s.Index].OwnerId == ownerId);
    }
}
