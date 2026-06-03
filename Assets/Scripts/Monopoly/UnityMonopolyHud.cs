#if UNITY_5_3_OR_NEWER
using System.Text;
using UnityEngine;

namespace MonopolyTake2;

[RequireComponent(typeof(UnityGameController))]
public sealed class UnityMonopolyHud : MonoBehaviour
{
    [SerializeField] private int panelWidth = 360;
    [SerializeField] private int logHeight = 220;

    private UnityGameController _controller = null!;
    private Vector2 _playerScroll;
    private Vector2 _logScroll;
    private GUIStyle _headerStyle = null!;
    private GUIStyle _buttonStyle = null!;
    private GUIStyle _boxStyle = null!;

    private void Awake()
    {
        _controller = GetComponent<UnityGameController>();
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (_controller.CurrentUi == null)
        {
            return;
        }

        DrawLeftPanel();
        DrawRightPanel();
        DrawBottomLog();
    }

    private void DrawLeftPanel()
    {
        GUILayout.BeginArea(new Rect(12, 12, panelWidth, Screen.height - logHeight - 30), GUI.skin.box);
        GUILayout.Label("Monopoly Take 2", _headerStyle);
        GUILayout.Label($"Phase: {_controller.Game.State.Phase}");
        GUILayout.Label($"Current: {_controller.Game.State.CurrentPlayer.Name}");
        GUILayout.Label($"Message: {_controller.LastMessage}");
        if (_controller.LastRoll.HasValue)
        {
            var roll = _controller.LastRoll.Value;
            GUILayout.Label($"Last Roll: {roll.Die1} + {roll.Die2}" + (roll.SpeedDie > 0 ? $" + {roll.SpeedDie}" : string.Empty));
        }

        GUILayout.Space(8);
        DrawTurnButtons();
        GUILayout.Space(8);
        DrawAssetButtons();
        GUILayout.Space(8);
        DrawSaveButtons();
        GUILayout.Space(8);
        DrawPlayers();
        GUILayout.EndArea();
    }

    private void DrawTurnButtons()
    {
        var canAct = _controller.CanHumanAct();
        GUI.enabled = canAct && _controller.Game.State.Phase == TurnPhase.AwaitingRoll;
        if (GUILayout.Button("Roll Dice", _buttonStyle)) _controller.RollButton();

        GUI.enabled = canAct && _controller.Game.State.Phase == TurnPhase.AwaitingPurchaseDecision;
        if (GUILayout.Button("Buy Property", _buttonStyle)) _controller.BuyButton();
        if (GUILayout.Button("Decline / Start Auction", _buttonStyle)) _controller.AuctionButton();

        var auction = _controller.Game.State.CurrentAuction;
        GUI.enabled = canAct && auction != null;
        if (GUILayout.Button("Auction Bid +$10", _buttonStyle)) _controller.BidButton(10);
        if (GUILayout.Button("Pass Auction", _buttonStyle)) _controller.PassBidButton();

        GUI.enabled = canAct && _controller.Game.State.CurrentPlayer.InJail;
        if (GUILayout.Button("Pay $50 Jail Fine", _buttonStyle)) _controller.PayJailFineButton();
        if (GUILayout.Button("Use Jail Free Card", _buttonStyle)) _controller.UseJailCardButton();
        GUI.enabled = true;
    }

    private void DrawAssetButtons()
    {
        GUILayout.Label("Selected Property Actions", _headerStyle);
        GUI.enabled = _controller.CanHumanAct();
        if (GUILayout.Button("Build House / Hotel", _buttonStyle)) _controller.BuildSelectedImprovement();
        if (GUILayout.Button("Mortgage Selected", _buttonStyle)) _controller.MortgageSelectedProperty();
        if (GUILayout.Button("Repay Mortgage", _buttonStyle)) _controller.UnmortgageSelectedProperty();
        GUI.enabled = true;
    }

    private void DrawSaveButtons()
    {
        GUILayout.Label("Save / Load", _headerStyle);
        if (GUILayout.Button("Save Game", _buttonStyle)) _controller.SaveButton();
        if (GUILayout.Button("Load Game", _buttonStyle)) _controller.LoadButton();
    }

    private void DrawPlayers()
    {
        GUILayout.Label("Players", _headerStyle);
        _playerScroll = GUILayout.BeginScrollView(_playerScroll, GUILayout.MinHeight(120));
        for (var i = 0; i < _controller.Game.State.Players.Count; i++)
        {
            var player = _controller.Game.State.Players[i];
            var marker = player.IsAi ? "AI" : "YOU";
            GUILayout.Label($"{(i == _controller.Game.State.CurrentPlayerIndex ? "▶ " : "")}{player.Name} ({marker}) ${player.Money} | {player.Token} | Props {player.OwnedPropertyIndexes.Count}" + (player.InJail ? " | JAIL" : string.Empty));
        }
        GUILayout.EndScrollView();
    }

    private void DrawRightPanel()
    {
        var x = Screen.width - panelWidth - 12;
        GUILayout.BeginArea(new Rect(x, 12, panelWidth, Screen.height - logHeight - 30), GUI.skin.box);
        GUILayout.Label("Board / Property Info", _headerStyle);
        var selected = _controller.Game.Board.GetSpace(_controller.SelectedSpaceIndex);
        GUILayout.Label($"Selected #{selected.Index}: {selected.Name}");
        GUILayout.Label($"Type: {selected.Type}");
        GUILayout.Label($"Group: {selected.ColorGroup}");
        if (selected.IsPurchasable)
        {
            DrawPropertyDetails(selected);
        }

        GUILayout.Space(8);
        GUILayout.Label("Click any 3D tile to inspect it.");
        GUILayout.Label("Human interaction is controlled here: roll, buy, auction, build, mortgage, save/load.");
        GUILayout.EndArea();
    }

    private void DrawPropertyDetails(BoardSpaceData selected)
    {
        var state = _controller.Game.State;
        var property = state.Properties[selected.Index];
        var owner = property.OwnerId.HasValue ? state.GetPlayer(property.OwnerId.Value).Name : "Unowned";
        GUILayout.Label($"Owner: {owner}");
        GUILayout.Label($"Price: ${selected.Price} | Mortgage: ${selected.MortgageValue}");
        GUILayout.Label($"Houses: {property.Houses} | Hotel: {property.HasHotel} | Mortgaged: {property.IsMortgaged}");
        if (selected.RentValues != null)
        {
            var rents = new StringBuilder("Rent: ");
            for (var i = 0; i < selected.RentValues.Length; i++)
            {
                if (i > 0) rents.Append(" / ");
                rents.Append('$').Append(selected.RentValues[i]);
            }
            GUILayout.Label(rents.ToString());
        }
    }

    private void DrawBottomLog()
    {
        GUILayout.BeginArea(new Rect(12, Screen.height - logHeight - 12, Screen.width - 24, logHeight), _boxStyle);
        GUILayout.Label("Turn History", _headerStyle);
        _logScroll = GUILayout.BeginScrollView(_logScroll);
        var log = _controller.CurrentUi.TurnLog;
        for (var i = 0; i < log.Count; i++)
        {
            GUILayout.Label(log[i]);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_headerStyle != null)
        {
            return;
        }

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fixedHeight = 32
        };
        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10)
        };
    }
}
#endif
