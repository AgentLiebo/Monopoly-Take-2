using System;
using System.Collections.Generic;

namespace MonopolyTake2;

public sealed record PropertyPanelModel(
    string Name,
    SpaceType Type,
    ColorGroup ColorGroup,
    int Price,
    int MortgageValue,
    IReadOnlyList<int> RentValues,
    string OwnerName,
    bool IsMortgaged,
    int Houses,
    bool HasHotel);

public sealed record PlayerHudModel(
    Guid PlayerId,
    string Name,
    TokenType Token,
    int Money,
    int Position,
    bool IsCurrentPlayer,
    bool InJail,
    bool Bankrupt,
    int GetOutOfJailFreeCards);

public sealed record GameUiModel(
    TurnPhase Phase,
    IReadOnlyList<PlayerHudModel> Players,
    PropertyPanelModel? SelectedProperty,
    AuctionState? Auction,
    TradeProposal? TradeProposal,
    IReadOnlyList<string> TurnLog,
    Guid? WinnerId);

public sealed class UiModelFactory
{
    private const int TurnLogLimit = 25;
    private readonly BoardManager _board;

    public UiModelFactory(BoardManager board)
    {
        _board = board;
    }

    public GameUiModel Create(MonopolyGameState state, int? selectedPropertyIndex = null)
    {
        return new GameUiModel(
            state.Phase,
            CreatePlayerHudModels(state),
            selectedPropertyIndex.HasValue ? CreatePropertyPanel(state, selectedPropertyIndex.Value) : null,
            state.CurrentAuction,
            state.CurrentTradeProposal,
            CreateTurnLog(state),
            state.WinnerId);
    }

    public PropertyPanelModel CreatePropertyPanel(MonopolyGameState state, int propertyIndex)
    {
        var space = _board.GetSpace(propertyIndex);
        state.Properties.TryGetValue(propertyIndex, out var property);
        var ownerName = property?.OwnerId.HasValue == true
            ? state.GetPlayer(property.OwnerId.Value).Name
            : "Unowned";
        return new PropertyPanelModel(
            space.Name,
            space.Type,
            space.ColorGroup,
            space.Price,
            space.MortgageValue,
            space.RentValues ?? Array.Empty<int>(),
            ownerName,
            property?.IsMortgaged ?? false,
            property?.Houses ?? 0,
            property?.HasHotel ?? false);
    }

    private static IReadOnlyList<PlayerHudModel> CreatePlayerHudModels(MonopolyGameState state)
    {
        var models = new List<PlayerHudModel>(state.Players.Count);
        for (var i = 0; i < state.Players.Count; i++)
        {
            var player = state.Players[i];
            models.Add(new PlayerHudModel(
                player.Id,
                player.Name,
                player.Token,
                player.Money,
                player.Position,
                i == state.CurrentPlayerIndex,
                player.InJail,
                player.Bankrupt,
                player.GetOutOfJailFreeCards));
        }

        return models;
    }

    private static IReadOnlyList<string> CreateTurnLog(MonopolyGameState state)
    {
        var start = Math.Max(0, state.TurnHistory.Count - TurnLogLimit);
        var entries = new List<string>(state.TurnHistory.Count - start);
        for (var i = start; i < state.TurnHistory.Count; i++)
        {
            entries.Add(state.TurnHistory[i].Message);
        }

        return entries;
    }
}
