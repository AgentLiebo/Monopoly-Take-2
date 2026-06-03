using System;
using System.Collections.Generic;

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
        var list = new List<PlayerState>(MonopolyRules.MaxPlayers);
        var tokens = new HashSet<TokenType>();
        foreach (var player in players)
        {
            if (!tokens.Add(player.Token))
            {
                throw new InvalidOperationException("Each player must choose a unique token.");
            }

            list.Add(new PlayerState { Name = player.Name, Token = player.Token, IsAi = player.IsAi });
        }

        if (list.Count is < MonopolyRules.MinPlayers or > MonopolyRules.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(players), "Monopoly supports 2–8 players.");
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
        var improved = new List<int>(player.OwnedPropertyIndexes.Count);
        for (var i = 0; i < player.OwnedPropertyIndexes.Count; i++)
        {
            var propertyIndex = player.OwnedPropertyIndexes[i];
            if (state.Properties[propertyIndex].IsImproved)
            {
                improved.Add(propertyIndex);
            }
        }

        improved.Sort((left, right) => ImprovementRank(state.Properties[right]).CompareTo(ImprovementRank(state.Properties[left])));
        for (var i = 0; i < improved.Count && player.Money < 0; i++)
        {
            while (player.Money < 0 && _properties.SellImprovement(state, player.Id, improved[i])) { }
        }

        var ownedSnapshot = new List<int>(player.OwnedPropertyIndexes);
        for (var i = 0; i < ownedSnapshot.Count && player.Money < 0; i++)
        {
            _properties.Mortgage(state, player.Id, ownedSnapshot[i]);
        }
    }

    public void DeclareBankruptcy(MonopolyGameState state, PlayerState debtor, PlayerState? creditor)
    {
        debtor.Bankrupt = true;
        var propertiesToTransfer = new List<int>(debtor.OwnedPropertyIndexes);
        for (var i = 0; i < propertiesToTransfer.Count; i++)
        {
            var propertyIndex = propertiesToTransfer[i];
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

    private static int ImprovementRank(PropertyState property)
    {
        return property.HasHotel ? MonopolyRules.HotelHouseEquivalent : property.Houses;
    }
}
