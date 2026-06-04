using System;
using System.Collections.Generic;
using System.Linq;

namespace MonopolyTake2;

public enum SpaceType
{
    Go,
    Property,
    Railroad,
    Utility,
    Chance,
    CommunityChest,
    IncomeTax,
    LuxuryTax,
    Jail,
    FreeParking,
    GoToJail
}

public enum ColorGroup
{
    None,
    Brown,
    LightBlue,
    Pink,
    Orange,
    Red,
    Yellow,
    Green,
    DarkBlue,
    Railroad,
    Utility
}

public enum CardDeckType
{
    Chance,
    CommunityChest
}

public enum CardActionType
{
    Collect,
    Pay,
    AdvanceToSpace,
    AdvanceToNearestRailroad,
    AdvanceToNearestUtility,
    MoveRelative,
    GoToJail,
    GetOutOfJailFree,
    Repairs,
    PayEachPlayer,
    CollectFromEachPlayer
}

public enum TurnPhase
{
    Setup,
    AwaitingRoll,
    Moving,
    ResolvingSpace,
    AwaitingPurchaseDecision,
    Auction,
    Trading,
    ManagingAssets,
    GameOver
}

public enum TokenType
{
    Battleship,
    Boot,
    Car,
    Cat,
    Dog,
    Hat,
    Iron,
    Thimble
}

public sealed record BoardSpaceData(
    int Index,
    string Name,
    SpaceType Type,
    int Price = 0,
    int MortgageValue = 0,
    int[]? RentValues = null,
    int HouseCost = 0,
    int HotelCost = 0,
    ColorGroup ColorGroup = ColorGroup.None,
    int TaxAmount = 0)
{
    public bool IsPurchasable => Type is SpaceType.Property or SpaceType.Railroad or SpaceType.Utility;
    public int BaseRent => RentValues is { Length: > 0 } ? RentValues[0] : 0;
}

public sealed record CardData(
    string Id,
    CardDeckType Deck,
    string Title,
    string Description,
    CardActionType Action,
    int Amount = 0,
    int TargetSpace = -1,
    bool KeepUntilUsed = false);

public sealed record DiceRoll(int Die1, int Die2, int SpeedDie = 0)
{
    public int Total => Die1 + Die2 + SpeedDie;
    public bool IsDouble => Die1 == Die2;
}

public sealed class PlayerState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public TokenType Token { get; set; }
    public bool IsAi { get; set; }
    public int Money { get; set; } = MonopolyRules.StartingMoney;
    public int Position { get; set; }
    public bool InJail { get; set; }
    public int JailTurnsAttempted { get; set; }
    public int ConsecutiveDoubles { get; set; }
    public bool Bankrupt { get; set; }
    public int GetOutOfJailFreeCards { get; set; }
    public List<int> OwnedPropertyIndexes { get; set; } = new();

    public override string ToString() => $"{Name} ({Token}) ${Money}";
}

public sealed class PropertyState
{
    public int BoardIndex { get; set; }
    public Guid? OwnerId { get; set; }
    public int Houses { get; set; }
    public bool HasHotel { get; set; }
    public bool IsMortgaged { get; set; }

    public bool IsImproved => Houses > 0 || HasHotel;
}

public sealed class AuctionState
{
    public int PropertyIndex { get; set; }
    public Guid? HighestBidderId { get; set; }
    public int HighestBid { get; set; }
    public HashSet<Guid> ActiveBidderIds { get; set; } = new();
    public bool IsClosed { get; set; }
}

public sealed class TradeOffer
{
    public int Money { get; set; }
    public List<int> PropertyIndexes { get; set; } = new();
    public int GetOutOfJailFreeCards { get; set; }
}

public sealed class TradeProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromPlayerId { get; set; }
    public Guid ToPlayerId { get; set; }
    public TradeOffer OfferedByFromPlayer { get; set; } = new();
    public TradeOffer OfferedByToPlayer { get; set; } = new();
    public bool? Accepted { get; set; }
}

public sealed class TurnHistoryEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Guid? PlayerId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class GameSettings
{
    public bool SpeedDieEnabled { get; set; }
    public bool FreeParkingJackpot { get; set; }
    public int IncomeTaxAmount { get; set; } = MonopolyRules.IncomeTax;
    public int LuxuryTaxAmount { get; set; } = MonopolyRules.LuxuryTax;
}

public sealed class MonopolyGameState
{
    public GameSettings Settings { get; set; } = new();
    public List<PlayerState> Players { get; set; } = new();
    public Dictionary<int, PropertyState> Properties { get; set; } = new();
    public int CurrentPlayerIndex { get; set; }
    public TurnPhase Phase { get; set; } = TurnPhase.Setup;
    public int FreeParkingPool { get; set; }
    public AuctionState? CurrentAuction { get; set; }
    public TradeProposal? CurrentTradeProposal { get; set; }
    public int? PendingPurchasePropertyIndex { get; set; }
    public List<TurnHistoryEntry> TurnHistory { get; set; } = new();
    public Queue<CardData> ChanceDeck { get; set; } = new();
    public Queue<CardData> CommunityChestDeck { get; set; } = new();
    public Guid? WinnerId { get; set; }

    public PlayerState CurrentPlayer => Players[CurrentPlayerIndex];
}

public static class MonopolyRules
{
    public const int StartingMoney = 1500;
    public const int GoSalary = 200;
    public const int JailFine = 50;
    public const int MaxJailTurns = 3;
    public const int IncomeTax = 200;
    public const int LuxuryTax = 100;
    public const int BoardSpaceCount = 40;
    public const int MaxPlayers = 8;
    public const int MinPlayers = 2;
    public const int MaxHouses = 4;
    public const int HotelHouseEquivalent = 5;

    public static int MortgageRepayment(int mortgageValue) => mortgageValue + (int)Math.Ceiling(mortgageValue * 0.10m);
}

public static class EnumerableExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
    {
        return source.OrderBy(_ => rng.Next());
    }
}
