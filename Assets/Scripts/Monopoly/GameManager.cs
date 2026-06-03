using System;
using System.Collections.Generic;

namespace MonopolyTake2;

public sealed class GameManager
{
    private readonly Random _rng;

    public GameManager(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Board = new BoardManager(_rng);
        Properties = new PropertyManager(Board);
        Players = new PlayerManager(Properties);
        Trades = new TradeManager(Properties);
        Ai = new AIManager(Board, Properties);
        Saves = new SaveManager();
        Ui = new UiModelFactory(Board);
    }

    public BoardManager Board { get; }
    public PropertyManager Properties { get; }
    public PlayerManager Players { get; }
    public TradeManager Trades { get; }
    public AIManager Ai { get; }
    public SaveManager Saves { get; }
    public UiModelFactory Ui { get; }
    public MonopolyGameState State { get; private set; } = new();

    public MonopolyGameState NewGame(IEnumerable<(string Name, TokenType Token, bool IsAi)> players, GameSettings? settings = null)
    {
        State = new MonopolyGameState
        {
            Settings = settings ?? new GameSettings(),
            Players = Players.CreatePlayers(players),
            Properties = Properties.CreateInitialPropertyStates(),
            ChanceDeck = Board.CreateChanceDeck(),
            CommunityChestDeck = Board.CreateCommunityChestDeck(),
            Phase = TurnPhase.AwaitingRoll
        };
        DetermineTurnOrder();
        Log("New game started. Turn order: " + BuildTurnOrderSummary() + ".");
        return State;
    }

    public void Load(MonopolyGameState state)
    {
        State = state;
        State.ChanceDeck = State.ChanceDeck.Count == 0 ? Board.CreateChanceDeck() : State.ChanceDeck;
        State.CommunityChestDeck = State.CommunityChestDeck.Count == 0 ? Board.CreateCommunityChestDeck() : State.CommunityChestDeck;
    }

    public DiceRoll RollDice() => new(_rng.Next(1, 7), _rng.Next(1, 7), State.Settings.SpeedDieEnabled ? _rng.Next(1, 7) : 0);

    public DiceRoll TakeTurn(DiceRoll? scriptedRoll = null)
    {
        EnsurePhase(TurnPhase.AwaitingRoll, TurnPhase.ManagingAssets);
        var player = State.CurrentPlayer;
        if (player.Bankrupt)
        {
            EndTurn(false);
            return scriptedRoll ?? new DiceRoll(0, 0);
        }

        State.Phase = TurnPhase.Moving;
        var roll = scriptedRoll ?? RollDice();
        Log($"{player.Name} rolled {roll.Die1} and {roll.Die2}" + (roll.SpeedDie > 0 ? $" plus speed die {roll.SpeedDie}" : string.Empty) + ".", player.Id);

        if (player.InJail)
        {
            ResolveJailRoll(player, roll);
        }
        else
        {
            if (roll.IsDouble)
            {
                player.ConsecutiveDoubles++;
                if (player.ConsecutiveDoubles >= 3)
                {
                    Players.SendToJail(player);
                    Log($"{player.Name} rolled doubles three times and went to Jail.", player.Id);
                    EndTurn(false);
                    return roll;
                }
            }
            else
            {
                player.ConsecutiveDoubles = 0;
            }

            MovePlayer(player, roll.Total);
            ResolveLanding(player, roll);
        }

        var extraTurn = roll.IsDouble && !player.InJail && !player.Bankrupt && State.Phase != TurnPhase.AwaitingPurchaseDecision;
        if (State.Phase is not TurnPhase.AwaitingPurchaseDecision and not TurnPhase.Auction and not TurnPhase.Trading and not TurnPhase.GameOver)
        {
            EndTurn(extraTurn);
        }
        return roll;
    }

    public void BuyPendingProperty()
    {
        var propertyIndex = State.PendingPurchasePropertyIndex ?? throw new InvalidOperationException("No property is pending purchase.");
        var player = State.CurrentPlayer;
        var space = Board.GetSpace(propertyIndex);
        if (player.Money < space.Price)
        {
            throw new InvalidOperationException("Player cannot afford this property.");
        }

        player.Money -= space.Price;
        Properties.TransferProperty(State, propertyIndex, player.Id);
        State.PendingPurchasePropertyIndex = null;
        Log($"{player.Name} bought {space.Name} for ${space.Price}.", player.Id);
        EndTurn(player.ConsecutiveDoubles > 0);
    }

    public AuctionState DeclinePendingPropertyAndStartAuction()
    {
        var propertyIndex = State.PendingPurchasePropertyIndex ?? throw new InvalidOperationException("No property is pending purchase.");
        State.PendingPurchasePropertyIndex = null;
        var activeBidderIds = new HashSet<Guid>();
        for (var i = 0; i < State.Players.Count; i++)
        {
            if (!State.Players[i].Bankrupt)
            {
                activeBidderIds.Add(State.Players[i].Id);
            }
        }

        State.CurrentAuction = new AuctionState
        {
            PropertyIndex = propertyIndex,
            ActiveBidderIds = activeBidderIds
        };
        State.Phase = TurnPhase.Auction;
        Log($"Auction started for {Board.GetSpace(propertyIndex).Name}.");
        return State.CurrentAuction;
    }

    public void PlaceAuctionBid(Guid bidderId, int amount)
    {
        var auction = State.CurrentAuction ?? throw new InvalidOperationException("No auction is active.");
        var bidder = State.GetPlayer(bidderId);
        if (!auction.ActiveBidderIds.Contains(bidderId) || bidder.Money < amount || amount <= auction.HighestBid)
        {
            throw new InvalidOperationException("Invalid auction bid.");
        }

        auction.HighestBidderId = bidderId;
        auction.HighestBid = amount;
        Log($"{bidder.Name} bid ${amount} for {Board.GetSpace(auction.PropertyIndex).Name}.", bidder.Id);
    }

    public void PassAuctionBid(Guid bidderId)
    {
        var auction = State.CurrentAuction ?? throw new InvalidOperationException("No auction is active.");
        auction.ActiveBidderIds.Remove(bidderId);
        if (auction.ActiveBidderIds.Count <= 1)
        {
            CloseAuction();
        }
    }

    public void CloseAuction()
    {
        var auction = State.CurrentAuction ?? throw new InvalidOperationException("No auction is active.");
        auction.IsClosed = true;
        if (auction.HighestBidderId.HasValue)
        {
            var winner = State.GetPlayer(auction.HighestBidderId.Value);
            winner.Money -= auction.HighestBid;
            Properties.TransferProperty(State, auction.PropertyIndex, winner.Id);
            Log($"{winner.Name} won {Board.GetSpace(auction.PropertyIndex).Name} for ${auction.HighestBid}.", winner.Id);
        }
        else
        {
            Log($"Auction closed with no buyer for {Board.GetSpace(auction.PropertyIndex).Name}.");
        }

        State.CurrentAuction = null;
        EndTurn(State.CurrentPlayer.ConsecutiveDoubles > 0);
    }

    public bool PayJailFine() => Players.TryPayJailFine(State.CurrentPlayer);
    public bool UseGetOutOfJailFreeCard() => Players.TryUseJailFreeCard(State.CurrentPlayer);

    private void DetermineTurnOrder()
    {
        var turnOrder = new List<(PlayerState Player, int Roll)>(State.Players.Count);
        for (var i = 0; i < State.Players.Count; i++)
        {
            turnOrder.Add((State.Players[i], _rng.Next(1, 7) + _rng.Next(1, 7)));
        }

        turnOrder.Sort((left, right) => right.Roll.CompareTo(left.Roll));
        State.Players = new List<PlayerState>(turnOrder.Count);
        for (var i = 0; i < turnOrder.Count; i++)
        {
            State.Players.Add(turnOrder[i].Player);
        }

        State.CurrentPlayerIndex = 0;
    }

    private void ResolveJailRoll(PlayerState player, DiceRoll roll)
    {
        player.JailTurnsAttempted++;
        if (roll.IsDouble)
        {
            player.InJail = false;
            player.JailTurnsAttempted = 0;
            Log($"{player.Name} rolled doubles and left Jail.", player.Id);
            MovePlayer(player, roll.Total);
            ResolveLanding(player, roll);
            return;
        }

        if (player.JailTurnsAttempted >= MonopolyRules.MaxJailTurns)
        {
            Players.PayBank(State, player, MonopolyRules.JailFine);
            player.InJail = false;
            player.JailTurnsAttempted = 0;
            Log($"{player.Name} paid $50 after three failed Jail rolls.", player.Id);
            MovePlayer(player, roll.Total);
            ResolveLanding(player, roll);
            return;
        }

        Log($"{player.Name} remains in Jail ({player.JailTurnsAttempted}/3 attempts).", player.Id);
    }

    private void MovePlayer(PlayerState player, int spaces)
    {
        var old = player.Position;
        var destination = Board.Move(old, spaces);
        if (Board.PassesGo(old, old + spaces))
        {
            Players.CollectFromBank(player, MonopolyRules.GoSalary);
            Log($"{player.Name} passed GO and collected $200.", player.Id);
        }
        player.Position = destination;
        Log($"{player.Name} moved to {Board.GetSpace(destination).Name}.", player.Id);
    }

    private void ResolveLanding(PlayerState player, DiceRoll roll)
    {
        State.Phase = TurnPhase.ResolvingSpace;
        var space = Board.GetSpace(player.Position);
        switch (space.Type)
        {
            case SpaceType.Go:
            case SpaceType.Jail:
                break;
            case SpaceType.FreeParking:
                if (State.Settings.FreeParkingJackpot && State.FreeParkingPool > 0)
                {
                    player.Money += State.FreeParkingPool;
                    Log($"{player.Name} collected ${State.FreeParkingPool} from Free Parking.", player.Id);
                    State.FreeParkingPool = 0;
                }
                break;
            case SpaceType.IncomeTax:
                Players.PayBank(State, player, State.Settings.IncomeTaxAmount);
                Log($"{player.Name} paid ${State.Settings.IncomeTaxAmount} Income Tax.", player.Id);
                break;
            case SpaceType.LuxuryTax:
                Players.PayBank(State, player, State.Settings.LuxuryTaxAmount);
                Log($"{player.Name} paid ${State.Settings.LuxuryTaxAmount} Luxury Tax.", player.Id);
                break;
            case SpaceType.GoToJail:
                Players.SendToJail(player);
                Log($"{player.Name} went directly to Jail.", player.Id);
                break;
            case SpaceType.Chance:
                DrawAndResolveCard(CardDeckType.Chance, player, roll);
                break;
            case SpaceType.CommunityChest:
                DrawAndResolveCard(CardDeckType.CommunityChest, player, roll);
                break;
            case SpaceType.Property:
            case SpaceType.Railroad:
            case SpaceType.Utility:
                ResolvePropertyLanding(player, space, roll);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ResolvePropertyLanding(PlayerState player, BoardSpaceData space, DiceRoll roll, bool forceDoubleRent = false)
    {
        var property = State.Properties[space.Index];
        if (!property.OwnerId.HasValue)
        {
            if (player.IsAi && Ai.ShouldBuyProperty(State, player, space.Index))
            {
                player.Money -= space.Price;
                Properties.TransferProperty(State, space.Index, player.Id);
                Log($"{player.Name} bought {space.Name} for ${space.Price}.", player.Id);
            }
            else
            {
                State.PendingPurchasePropertyIndex = space.Index;
                State.Phase = TurnPhase.AwaitingPurchaseDecision;
                Log($"{space.Name} is unowned. {player.Name} may buy it for ${space.Price} or start an auction.", player.Id);
            }
            return;
        }

        if (property.OwnerId.Value == player.Id || property.IsMortgaged)
        {
            return;
        }

        var owner = State.GetPlayer(property.OwnerId.Value);
        var rent = Properties.CalculateRent(State, space.Index, roll, forceDoubleRent);
        Players.PayPlayer(State, player, owner, rent);
        Log($"{player.Name} paid ${rent} rent to {owner.Name} for {space.Name}.", player.Id);
    }

    private void DrawAndResolveCard(CardDeckType deckType, PlayerState player, DiceRoll roll)
    {
        var deck = deckType == CardDeckType.Chance ? State.ChanceDeck : State.CommunityChestDeck;
        if (deck.Count == 0)
        {
            if (deckType == CardDeckType.Chance) State.ChanceDeck = Board.CreateChanceDeck(); else State.CommunityChestDeck = Board.CreateCommunityChestDeck();
            deck = deckType == CardDeckType.Chance ? State.ChanceDeck : State.CommunityChestDeck;
        }

        var card = deck.Dequeue();
        Log($"{player.Name} drew {card.Title}: {card.Description}", player.Id);
        if (!card.KeepUntilUsed)
        {
            deck.Enqueue(card);
        }

        switch (card.Action)
        {
            case CardActionType.Collect:
                Players.CollectFromBank(player, card.Amount);
                break;
            case CardActionType.Pay:
                Players.PayBank(State, player, card.Amount);
                break;
            case CardActionType.AdvanceToSpace:
                AdvanceToCardSpace(player, card.TargetSpace, roll);
                break;
            case CardActionType.AdvanceToNearestRailroad:
                AdvanceToNearestAndResolve(player, SpaceType.Railroad, roll, true);
                break;
            case CardActionType.AdvanceToNearestUtility:
                AdvanceToNearestAndResolve(player, SpaceType.Utility, roll, false);
                break;
            case CardActionType.MoveRelative:
                MovePlayer(player, card.Amount);
                ResolveLanding(player, roll);
                break;
            case CardActionType.GoToJail:
                Players.SendToJail(player);
                break;
            case CardActionType.GetOutOfJailFree:
                player.GetOutOfJailFreeCards++;
                break;
            case CardActionType.Repairs:
                var owed = 0;
                for (var i = 0; i < player.OwnedPropertyIndexes.Count; i++)
                {
                    var ownedProperty = State.Properties[player.OwnedPropertyIndexes[i]];
                    owed += ownedProperty.Houses * card.Amount + (ownedProperty.HasHotel ? card.TargetSpace : 0);
                }

                Players.PayBank(State, player, owed);
                break;
            case CardActionType.PayEachPlayer:
                for (var i = 0; i < State.Players.Count; i++)
                {
                    var other = State.Players[i];
                    if (other.Id != player.Id && !other.Bankrupt)
                    {
                        Players.PayPlayer(State, player, other, card.Amount);
                    }
                }
                break;
            case CardActionType.CollectFromEachPlayer:
                for (var i = 0; i < State.Players.Count; i++)
                {
                    var other = State.Players[i];
                    if (other.Id != player.Id && !other.Bankrupt)
                    {
                        Players.PayPlayer(State, other, player, card.Amount);
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void AdvanceToCardSpace(PlayerState player, int target, DiceRoll roll)
    {
        if (target < player.Position)
        {
            Players.CollectFromBank(player, MonopolyRules.GoSalary);
            Log($"{player.Name} passed GO and collected $200.", player.Id);
        }
        player.Position = target;
        ResolveLanding(player, roll);
    }

    private void AdvanceToNearestAndResolve(PlayerState player, SpaceType type, DiceRoll roll, bool doubleRent)
    {
        var target = Board.FindNearest(player.Position, type);
        if (target < player.Position)
        {
            Players.CollectFromBank(player, MonopolyRules.GoSalary);
        }
        player.Position = target;
        ResolvePropertyLanding(player, Board.GetSpace(target), roll, doubleRent);
    }

    private void EndTurn(bool extraTurn)
    {
        CheckWinCondition();
        if (State.Phase == TurnPhase.GameOver) return;

        State.Phase = TurnPhase.ManagingAssets;
        for (var i = 0; i < State.Players.Count; i++)
        {
            var ai = State.Players[i];
            if (ai.IsAi && !ai.Bankrupt)
            {
                Ai.ManageAssets(State, ai);
            }
        }

        if (!extraTurn)
        {
            State.CurrentPlayer.ConsecutiveDoubles = 0;
            do
            {
                State.CurrentPlayerIndex = (State.CurrentPlayerIndex + 1) % State.Players.Count;
            } while (State.CurrentPlayer.Bankrupt);
        }
        State.Phase = TurnPhase.AwaitingRoll;
    }

    private void CheckWinCondition()
    {
        PlayerState? winner = null;
        var activeCount = 0;
        for (var i = 0; i < State.Players.Count; i++)
        {
            if (State.Players[i].Bankrupt)
            {
                continue;
            }

            winner = State.Players[i];
            activeCount++;
            if (activeCount > 1)
            {
                return;
            }
        }

        if (activeCount == 1 && winner != null)
        {
            State.WinnerId = winner.Id;
            State.Phase = TurnPhase.GameOver;
            Log($"{winner.Name} wins the game!", winner.Id);
        }
    }

    private void EnsurePhase(params TurnPhase[] phases)
    {
        for (var i = 0; i < phases.Length; i++)
        {
            if (phases[i] == State.Phase)
            {
                return;
            }
        }

        throw new InvalidOperationException($"Expected phase {string.Join(" or ", phases)}, got {State.Phase}.");
    }

    private string BuildTurnOrderSummary()
    {
        var names = new string[State.Players.Count];
        for (var i = 0; i < State.Players.Count; i++)
        {
            names[i] = State.Players[i].Name;
        }

        return string.Join(", ", names);
    }

    private void Log(string message, Guid? playerId = null)
    {
        State.TurnHistory.Add(new TurnHistoryEntry { PlayerId = playerId, Message = message });
    }
}
