using System;
using System.Collections.Generic;
using System.Linq;

namespace MonopolyTake2;

public sealed class PlayerManager
{
    private readonly PropertyManager _properties;

    public PlayerManager(PropertyManager properties)
    {
        _properties = properties;
    }

    public List<PlayerState> CreatePlayers(IEnumerable<(string Name, TokenType Token, bool IsAi)> players)
    {
        var list = players.Select(p => new PlayerState { Name = p.Name, Token = p.Token, IsAi = p.IsAi }).ToList();
        if (list.Count is < MonopolyRules.MinPlayers or > MonopolyRules.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(players), "Monopoly supports 2–8 players.");
        }
        if (list.Select(p => p.Token).Distinct().Count() != list.Count)
        {
            throw new InvalidOperationException("Each player must choose a unique token.");
        }
        return list;
    }

    public void PayBank(MonopolyGameState state, PlayerState player, int amount)
    {
        player.Money -= amount;
        if (state.Settings.FreeParkingJackpot)
        {
            state.FreeParkingPool += amount;
        }
        ResolveDebtIfNeeded(state, player, null);
    }

    public void CollectFromBank(PlayerState player, int amount) => player.Money += amount;

    public void PayPlayer(MonopolyGameState state, PlayerState payer, PlayerState creditor, int amount)
    {
        payer.Money -= amount;
        creditor.Money += amount;
        ResolveDebtIfNeeded(state, payer, creditor);
    }

    public void SendToJail(PlayerState player)
    {
        player.Position = 10;
        player.InJail = true;
        player.JailTurnsAttempted = 0;
        player.ConsecutiveDoubles = 0;
    }

    public bool TryPayJailFine(PlayerState player)
    {
        if (!player.InJail || player.Money < MonopolyRules.JailFine)
        {
            return false;
        }

        player.Money -= MonopolyRules.JailFine;
        player.InJail = false;
        player.JailTurnsAttempted = 0;
        return true;
    }

    public bool TryUseJailFreeCard(PlayerState player)
    {
        if (!player.InJail || player.GetOutOfJailFreeCards <= 0)
        {
            return false;
        }

        player.GetOutOfJailFreeCards--;
        player.InJail = false;
        player.JailTurnsAttempted = 0;
        return true;
    }

    public void ResolveDebtIfNeeded(MonopolyGameState state, PlayerState debtor, PlayerState? creditor)
    {
        if (debtor.Money >= 0 || debtor.Bankrupt)
        {
            return;
        }

        LiquidateAssets(state, debtor);
        if (debtor.Money >= 0)
        {
            return;
        }

        DeclareBankruptcy(state, debtor, creditor);
    }

    public void LiquidateAssets(MonopolyGameState state, PlayerState player)
    {
        var improved = player.OwnedPropertyIndexes
            .Where(i => state.Properties[i].IsImproved)
            .OrderByDescending(i => state.Properties[i].HasHotel ? MonopolyRules.HotelHouseEquivalent : state.Properties[i].Houses)
            .ToList();
        foreach (var index in improved)
        {
            while (player.Money < 0 && _properties.SellImprovement(state, player.Id, index)) { }
        }

        foreach (var index in player.OwnedPropertyIndexes.ToList())
        {
            if (player.Money >= 0) break;
            _properties.Mortgage(state, player.Id, index);
        }
    }

    public void DeclareBankruptcy(MonopolyGameState state, PlayerState debtor, PlayerState? creditor)
    {
        debtor.Bankrupt = true;
        foreach (var propertyIndex in debtor.OwnedPropertyIndexes.ToList())
        {
            var property = state.Properties[propertyIndex];
            property.Houses = 0;
            property.HasHotel = false;
            _properties.TransferProperty(state, propertyIndex, creditor?.Id);
        }

        if (creditor != null)
        {
            creditor.Money += Math.Max(0, debtor.Money);
            creditor.GetOutOfJailFreeCards += debtor.GetOutOfJailFreeCards;
        }

        debtor.OwnedPropertyIndexes.Clear();
        debtor.GetOutOfJailFreeCards = 0;
    }
}
