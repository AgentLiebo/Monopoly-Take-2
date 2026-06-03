using System;

namespace MonopolyTake2;

public sealed class TradeManager
{
    private readonly PropertyManager _properties;

    public TradeManager(PropertyManager properties)
    {
        _properties = properties;
    }

    public TradeProposal ProposeTrade(MonopolyGameState state, Guid fromPlayerId, Guid toPlayerId, TradeOffer fromOffer, TradeOffer toOffer)
    {
        ValidateOfferOwnership(state, fromPlayerId, fromOffer);
        ValidateOfferOwnership(state, toPlayerId, toOffer);
        var proposal = new TradeProposal
        {
            FromPlayerId = fromPlayerId,
            ToPlayerId = toPlayerId,
            OfferedByFromPlayer = fromOffer,
            OfferedByToPlayer = toOffer
        };
        state.CurrentTradeProposal = proposal;
        state.Phase = TurnPhase.Trading;
        return proposal;
    }

    public void RejectTrade(MonopolyGameState state)
    {
        if (state.CurrentTradeProposal != null)
        {
            state.CurrentTradeProposal.Accepted = false;
        }
        state.CurrentTradeProposal = null;
        state.Phase = TurnPhase.AwaitingRoll;
    }

    public void AcceptTrade(MonopolyGameState state)
    {
        var proposal = state.CurrentTradeProposal ?? throw new InvalidOperationException("No trade proposal is active.");
        ValidateOfferOwnership(state, proposal.FromPlayerId, proposal.OfferedByFromPlayer);
        ValidateOfferOwnership(state, proposal.ToPlayerId, proposal.OfferedByToPlayer);

        var from = state.GetPlayer(proposal.FromPlayerId);
        var to = state.GetPlayer(proposal.ToPlayerId);
        TransferOffer(state, from, to, proposal.OfferedByFromPlayer);
        TransferOffer(state, to, from, proposal.OfferedByToPlayer);
        proposal.Accepted = true;
        state.CurrentTradeProposal = null;
        state.Phase = TurnPhase.AwaitingRoll;
    }

    private void TransferOffer(MonopolyGameState state, PlayerState giver, PlayerState receiver, TradeOffer offer)
    {
        giver.Money -= offer.Money;
        receiver.Money += offer.Money;
        giver.GetOutOfJailFreeCards -= offer.GetOutOfJailFreeCards;
        receiver.GetOutOfJailFreeCards += offer.GetOutOfJailFreeCards;
        foreach (var propertyIndex in offer.PropertyIndexes)
        {
            _properties.TransferProperty(state, propertyIndex, receiver.Id);
        }
    }

    private static void ValidateOfferOwnership(MonopolyGameState state, Guid ownerId, TradeOffer offer)
    {
        var player = state.GetPlayer(ownerId);
        if (player.Money < offer.Money)
        {
            throw new InvalidOperationException("Player cannot offer more money than they have.");
        }
        if (player.GetOutOfJailFreeCards < offer.GetOutOfJailFreeCards)
        {
            throw new InvalidOperationException("Player cannot offer unavailable Get Out of Jail Free cards.");
        }
        foreach (var propertyIndex in offer.PropertyIndexes)
        {
            if (!player.OwnedPropertyIndexes.Contains(propertyIndex))
            {
                throw new InvalidOperationException("Player cannot offer a property they do not own.");
            }
            var property = state.Properties[propertyIndex];
            if (property.IsImproved)
            {
                throw new InvalidOperationException("Improved properties cannot be traded until houses/hotels are sold.");
            }
        }
    }
}
