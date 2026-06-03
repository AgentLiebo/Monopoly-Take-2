#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MonopolyTake2;

public sealed class UnityGameController : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private bool speedDieEnabled;
    [SerializeField] private bool freeParkingJackpot;
    [SerializeField] private bool autoCreateView = true;
    [SerializeField] private bool autoCreateHud = true;
    [SerializeField] private bool autoAdvanceAiTurns = true;
    [SerializeField] private string saveFileName = "monopoly-save.json";

    private readonly GameManager _game = new();
    private bool _aiTurnInProgress;

    public event Action? GameRefreshed;

    public GameManager Game => _game;
    public GameUiModel CurrentUi { get; private set; } = null!;
    public DiceRoll? LastRoll { get; private set; }
    public int SelectedSpaceIndex { get; private set; }
    public string LastMessage { get; private set; } = "Welcome to Monopoly Take 2.";
    public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void Awake()
    {
        EnsureSceneHelpers();
    }

    private void Start()
    {
        StartLocalGame(new[]
        {
            ("Player 1", TokenType.Car, false),
            ("CPU 1", TokenType.Dog, true),
            ("CPU 2", TokenType.Hat, true),
            ("CPU 3", TokenType.Battleship, true)
        });
    }

    public void StartLocalGame(IEnumerable<(string Name, TokenType Token, bool IsAi)> players)
    {
        _game.NewGame(players, new GameSettings
        {
            SpeedDieEnabled = speedDieEnabled,
            FreeParkingJackpot = freeParkingJackpot
        });
        SelectedSpaceIndex = _game.State.CurrentPlayer.Position;
        LastRoll = null;
        LastMessage = "Game started. Human players decide rolls, buys, auctions, building, mortgages, saves, and loads from the GUI.";
        RefreshUi();
        TryStartAiTurns();
    }

    public void SelectSpace(int boardIndex)
    {
        SelectedSpaceIndex = _game.Board.Normalize(boardIndex);
        RefreshUi(false);
    }

    public void RollButton()
    {
        if (!CanHumanAct() || _game.State.Phase != TurnPhase.AwaitingRoll)
        {
            LastMessage = "Roll is only available on your turn while waiting to roll.";
            RefreshUi(false);
            return;
        }

        LastRoll = _game.TakeTurn();
        SelectedSpaceIndex = _game.State.CurrentPlayer.Position;
        LastMessage = $"Rolled {LastRoll.Die1} + {LastRoll.Die2}" + (LastRoll.SpeedDie > 0 ? $" + speed {LastRoll.SpeedDie}" : string.Empty) + ".";
        RefreshUi();
        TryStartAiTurns();
    }

    public void BuyButton()
    {
        if (!CanHumanAct() || _game.State.Phase != TurnPhase.AwaitingPurchaseDecision)
        {
            LastMessage = "There is no property purchase decision waiting for you.";
            RefreshUi(false);
            return;
        }

        _game.BuyPendingProperty();
        LastMessage = "Purchased property.";
        RefreshUi();
        TryStartAiTurns();
    }

    public void AuctionButton()
    {
        if (!CanHumanAct() || _game.State.Phase != TurnPhase.AwaitingPurchaseDecision)
        {
            LastMessage = "There is no property to auction right now.";
            RefreshUi(false);
            return;
        }

        _game.DeclinePendingPropertyAndStartAuction();
        LastMessage = "Auction started. Use Bid +$10 or Pass.";
        RunAiAuctionBids();
        RefreshUi();
    }

    public void BidButton(int increment = 10)
    {
        var auction = _game.State.CurrentAuction;
        if (!CanHumanAct() || auction == null || !auction.ActiveBidderIds.Contains(_game.State.CurrentPlayer.Id))
        {
            LastMessage = "You are not an active bidder.";
            RefreshUi(false);
            return;
        }

        var bid = auction.HighestBid + increment;
        if (_game.State.CurrentPlayer.Money < bid)
        {
            LastMessage = "You cannot afford that bid.";
            RefreshUi(false);
            return;
        }

        _game.PlaceAuctionBid(_game.State.CurrentPlayer.Id, bid);
        LastMessage = $"Bid ${bid}.";
        RunAiAuctionBids();
        RefreshUi();
        TryStartAiTurns();
    }

    public void PassBidButton()
    {
        var auction = _game.State.CurrentAuction;
        if (!CanHumanAct() || auction == null)
        {
            LastMessage = "No auction is active.";
            RefreshUi(false);
            return;
        }

        if (auction.ActiveBidderIds.Contains(_game.State.CurrentPlayer.Id))
        {
            _game.PassAuctionBid(_game.State.CurrentPlayer.Id);
            LastMessage = "Passed auction bid.";
        }

        RunAiAuctionBids();
        RefreshUi();
        TryStartAiTurns();
    }

    public void BuildSelectedImprovement()
    {
        var player = _game.State.CurrentPlayer;
        if (!CanHumanAct())
        {
            LastMessage = "You can only build during your own turn.";
        }
        else if (_game.Properties.CanBuildHouse(_game.State, player.Id, SelectedSpaceIndex, out var reason))
        {
            _game.Properties.BuildHouseOrHotel(_game.State, player.Id, SelectedSpaceIndex);
            LastMessage = "Built an improvement.";
        }
        else
        {
            LastMessage = reason;
        }

        RefreshUi();
    }

    public void MortgageSelectedProperty()
    {
        var player = _game.State.CurrentPlayer;
        if (CanHumanAct() && _game.State.Properties.ContainsKey(SelectedSpaceIndex) && _game.Properties.Mortgage(_game.State, player.Id, SelectedSpaceIndex))
        {
            LastMessage = "Property mortgaged.";
        }
        else
        {
            LastMessage = "Cannot mortgage the selected property.";
        }

        RefreshUi();
    }

    public void UnmortgageSelectedProperty()
    {
        var player = _game.State.CurrentPlayer;
        if (CanHumanAct() && _game.State.Properties.ContainsKey(SelectedSpaceIndex) && _game.Properties.Unmortgage(_game.State, player.Id, SelectedSpaceIndex))
        {
            LastMessage = "Mortgage repaid.";
        }
        else
        {
            LastMessage = "Cannot repay mortgage on the selected property.";
        }

        RefreshUi();
    }

    public void PayJailFineButton()
    {
        LastMessage = _game.PayJailFine() ? "Paid $50 to leave Jail." : "Cannot pay Jail fine right now.";
        RefreshUi();
    }

    public void UseJailCardButton()
    {
        LastMessage = _game.UseGetOutOfJailFreeCard() ? "Used Get Out of Jail Free." : "No Jail card is available.";
        RefreshUi();
    }

    public void SaveButton()
    {
        _game.Saves.SaveGame(_game.State, SavePath);
        LastMessage = $"Saved to {SavePath}.";
        RefreshUi(false);
    }

    public void LoadButton()
    {
        if (!_game.Saves.HasSave(SavePath))
        {
            LastMessage = "No saved game exists yet.";
            RefreshUi(false);
            return;
        }

        _game.Load(_game.Saves.LoadGame(SavePath));
        SelectedSpaceIndex = _game.State.CurrentPlayer.Position;
        LastMessage = $"Loaded from {SavePath}.";
        RefreshUi();
        TryStartAiTurns();
    }

    public bool CanHumanAct()
    {
        return _game.State.Phase != TurnPhase.GameOver && !_game.State.CurrentPlayer.IsAi && !_aiTurnInProgress;
    }

    private void TryStartAiTurns()
    {
        if (autoAdvanceAiTurns && !_aiTurnInProgress && _game.State.Phase != TurnPhase.GameOver && _game.State.CurrentPlayer.IsAi)
        {
            StartCoroutine(AutoAdvanceAiTurns());
        }
    }

    private IEnumerator AutoAdvanceAiTurns()
    {
        _aiTurnInProgress = true;
        while (_game.State.Phase != TurnPhase.GameOver && _game.State.CurrentPlayer.IsAi)
        {
            yield return new WaitForSeconds(0.65f);
            ResolveAiDecisionIfNeeded();
            if (_game.State.Phase is TurnPhase.AwaitingRoll or TurnPhase.ManagingAssets)
            {
                LastRoll = _game.TakeTurn();
                LastMessage = $"{_game.State.CurrentPlayer.Name} auto-played.";
            }

            ResolveAiDecisionIfNeeded();
            SelectedSpaceIndex = _game.State.CurrentPlayer.Position;
            RefreshUi();
        }

        _aiTurnInProgress = false;
        RefreshUi();
    }

    private void ResolveAiDecisionIfNeeded()
    {
        while (_game.State.CurrentPlayer.IsAi && _game.State.Phase is TurnPhase.AwaitingPurchaseDecision or TurnPhase.Auction)
        {
            if (_game.State.Phase == TurnPhase.AwaitingPurchaseDecision)
            {
                _game.DeclinePendingPropertyAndStartAuction();
            }

            if (_game.State.Phase == TurnPhase.Auction)
            {
                RunAiAuctionBids();
                if (_game.State.CurrentAuction != null)
                {
                    _game.CloseAuction();
                }
            }
        }
    }

    private void RunAiAuctionBids()
    {
        var auction = _game.State.CurrentAuction;
        if (auction == null)
        {
            return;
        }

        for (var i = 0; i < _game.State.Players.Count && _game.State.CurrentAuction != null; i++)
        {
            var bidder = _game.State.Players[i];
            if (!bidder.IsAi || bidder.Bankrupt || !auction.ActiveBidderIds.Contains(bidder.Id))
            {
                continue;
            }

            var bid = _game.Ai.ChooseAuctionBid(_game.State, bidder, auction.PropertyIndex, auction.HighestBid);
            if (bid > auction.HighestBid)
            {
                _game.PlaceAuctionBid(bidder.Id, bid);
            }
            else
            {
                _game.PassAuctionBid(bidder.Id);
            }
        }
    }

    private void RefreshUi(bool syncSelectionToCurrentPlayer = true)
    {
        if (syncSelectionToCurrentPlayer && _game.State.Players.Count > 0)
        {
            SelectedSpaceIndex = _game.State.CurrentPlayer.Position;
        }

        CurrentUi = _game.Ui.Create(_game.State, SelectedSpaceIndex);
        GameRefreshed?.Invoke();
    }

    private void EnsureSceneHelpers()
    {
        if (autoCreateView && FindObjectOfType<UnityMonopolyBoardView>() == null)
        {
            gameObject.AddComponent<UnityMonopolyBoardView>();
        }

        if (autoCreateHud && FindObjectOfType<UnityMonopolyHud>() == null)
        {
            gameObject.AddComponent<UnityMonopolyHud>();
        }
    }
}
#endif
