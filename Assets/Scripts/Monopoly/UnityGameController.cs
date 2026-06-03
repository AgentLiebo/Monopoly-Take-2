#if UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyTake2;

public sealed class UnityGameController : MonoBehaviour
{
    [SerializeField] private bool speedDieEnabled;
    [SerializeField] private bool freeParkingJackpot;

    private readonly GameManager _game = new();

    public GameUiModel CurrentUi { get; private set; } = null!;

    private void Start()
    {
        StartLocalGame(new[]
        {
            ("Player 1", TokenType.Car, false),
            ("CPU 1", TokenType.Dog, true)
        });
    }

    public void StartLocalGame(IEnumerable<(string Name, TokenType Token, bool IsAi)> players)
    {
        _game.NewGame(players, new GameSettings
        {
            SpeedDieEnabled = speedDieEnabled,
            FreeParkingJackpot = freeParkingJackpot
        });
        RefreshUi();
    }

    public void RollButton()
    {
        _game.TakeTurn();
        RefreshUi();
    }

    public void BuyButton()
    {
        _game.BuyPendingProperty();
        RefreshUi();
    }

    public void AuctionButton()
    {
        _game.DeclinePendingPropertyAndStartAuction();
        RefreshUi();
    }

    public void PayJailFineButton()
    {
        _game.PayJailFine();
        RefreshUi();
    }

    public void UseJailCardButton()
    {
        _game.UseGetOutOfJailFreeCard();
        RefreshUi();
    }

    public void SaveButton(string path)
    {
        _game.Saves.SaveGame(_game.State, path);
    }

    public void LoadButton(string path)
    {
        _game.Load(_game.Saves.LoadGame(path));
        RefreshUi();
    }

    private void RefreshUi()
    {
        CurrentUi = _game.Ui.Create(_game.State, _game.State.CurrentPlayer.Position);
        // Bind CurrentUi to Unity UI Toolkit/uGUI views for player HUDs, dice animation,
        // property details, ownership indicators, houses/hotels, auction and trade windows.
    }
}
#endif
