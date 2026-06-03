using System;
using System.Collections.Generic;
using System.Linq;

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
    private readonly BoardManager _board;

    public UiModelFactory(BoardManager board)
    {
        _board = board;
    }

    public GameUiModel Create(MonopolyGameState state, int? selectedPropertyIndex = null)
    {
        return new GameUiModel(
            state.Phase,
            state.Players.Select((p, i) => new PlayerHudModel(p.Id, p.Name, p.Token, p.Money, p.Position, i == state.CurrentPlayerIndex, p.InJail, p.Bankrupt, p.GetOutOfJailFreeCards)).ToList(),
            selectedPropertyIndex.HasValue ? CreatePropertyPanel(state, selectedPropertyIndex.Value) : null,
            state.CurrentAuction,
            state.CurrentTradeProposal,
            state.TurnHistory.TakeLast(25).Select(h => h.Message).ToList(),
            state.WinnerId);
    }

    public PropertyPanelModel CreatePropertyPanel(MonopolyGameState state, int propertyIndex)
    {
        var space = _board.GetSpace(propertyIndex);
        state.Properties.TryGetValue(propertyIndex, out var property);
        var ownerName = property?.OwnerId.HasValue == true
            ? state.Players.Single(p => p.Id == property.OwnerId.Value).Name
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
}
