using System;
using System.Collections.Generic;
using System.Linq;

namespace MonopolyTake2;

public sealed class BoardManager
{
    private readonly Random _rng;

    public BoardManager(Random? rng = null)
    {
        _rng = rng ?? new Random();
        Spaces = BuildStandardBoard();
        SpaceByIndex = Spaces.ToDictionary(s => s.Index);
    }

    public IReadOnlyList<BoardSpaceData> Spaces { get; }
    public IReadOnlyDictionary<int, BoardSpaceData> SpaceByIndex { get; }

    public BoardSpaceData GetSpace(int index) => SpaceByIndex[Normalize(index)];

    public int Normalize(int index)
    {
        var result = index % MonopolyRules.BoardSpaceCount;
        return result < 0 ? result + MonopolyRules.BoardSpaceCount : result;
    }

    public bool PassesGo(int start, int destination) => Normalize(destination) < Normalize(start) || destination >= MonopolyRules.BoardSpaceCount;

    public int Move(int start, int spaces) => Normalize(start + spaces);

    public int FindNearest(int currentIndex, SpaceType type)
    {
        for (var offset = 1; offset <= MonopolyRules.BoardSpaceCount; offset++)
        {
            var candidate = GetSpace(currentIndex + offset);
            if (candidate.Type == type)
            {
                return candidate.Index;
            }
        }

        throw new InvalidOperationException($"No board space of type {type} exists.");
    }

    public Queue<CardData> CreateChanceDeck() => new(BuildChanceCards().Shuffle(_rng));
    public Queue<CardData> CreateCommunityChestDeck() => new(BuildCommunityChestCards().Shuffle(_rng));

    private static IReadOnlyList<BoardSpaceData> BuildStandardBoard()
    {
        static BoardSpaceData Prop(int i, string n, int price, int mortgage, int[] rents, int houseCost, ColorGroup group) =>
            new(i, n, SpaceType.Property, price, mortgage, rents, houseCost, houseCost, group);
        static BoardSpaceData Rail(int i, string n) => new(i, n, SpaceType.Railroad, 200, 100, new[] { 25, 50, 100, 200 }, 0, 0, ColorGroup.Railroad);
        static BoardSpaceData Utility(int i, string n) => new(i, n, SpaceType.Utility, 150, 75, new[] { 4, 10 }, 0, 0, ColorGroup.Utility);

        return new[]
        {
            new BoardSpaceData(0, "GO", SpaceType.Go),
            Prop(1, "Mediterranean Avenue", 60, 30, new[] { 2, 10, 30, 90, 160, 250 }, 50, ColorGroup.Brown),
            new BoardSpaceData(2, "Community Chest", SpaceType.CommunityChest),
            Prop(3, "Baltic Avenue", 60, 30, new[] { 4, 20, 60, 180, 320, 450 }, 50, ColorGroup.Brown),
            new BoardSpaceData(4, "Income Tax", SpaceType.IncomeTax, TaxAmount: MonopolyRules.IncomeTax),
            Rail(5, "Reading Railroad"),
            Prop(6, "Oriental Avenue", 100, 50, new[] { 6, 30, 90, 270, 400, 550 }, 50, ColorGroup.LightBlue),
            new BoardSpaceData(7, "Chance", SpaceType.Chance),
            Prop(8, "Vermont Avenue", 100, 50, new[] { 6, 30, 90, 270, 400, 550 }, 50, ColorGroup.LightBlue),
            Prop(9, "Connecticut Avenue", 120, 60, new[] { 8, 40, 100, 300, 450, 600 }, 50, ColorGroup.LightBlue),
            new BoardSpaceData(10, "Jail / Just Visiting", SpaceType.Jail),
            Prop(11, "St. Charles Place", 140, 70, new[] { 10, 50, 150, 450, 625, 750 }, 100, ColorGroup.Pink),
            Utility(12, "Electric Company"),
            Prop(13, "States Avenue", 140, 70, new[] { 10, 50, 150, 450, 625, 750 }, 100, ColorGroup.Pink),
            Prop(14, "Virginia Avenue", 160, 80, new[] { 12, 60, 180, 500, 700, 900 }, 100, ColorGroup.Pink),
            Rail(15, "Pennsylvania Railroad"),
            Prop(16, "St. James Place", 180, 90, new[] { 14, 70, 200, 550, 750, 950 }, 100, ColorGroup.Orange),
            new BoardSpaceData(17, "Community Chest", SpaceType.CommunityChest),
            Prop(18, "Tennessee Avenue", 180, 90, new[] { 14, 70, 200, 550, 750, 950 }, 100, ColorGroup.Orange),
            Prop(19, "New York Avenue", 200, 100, new[] { 16, 80, 220, 600, 800, 1000 }, 100, ColorGroup.Orange),
            new BoardSpaceData(20, "Free Parking", SpaceType.FreeParking),
            Prop(21, "Kentucky Avenue", 220, 110, new[] { 18, 90, 250, 700, 875, 1050 }, 150, ColorGroup.Red),
            new BoardSpaceData(22, "Chance", SpaceType.Chance),
            Prop(23, "Indiana Avenue", 220, 110, new[] { 18, 90, 250, 700, 875, 1050 }, 150, ColorGroup.Red),
            Prop(24, "Illinois Avenue", 240, 120, new[] { 20, 100, 300, 750, 925, 1100 }, 150, ColorGroup.Red),
            Rail(25, "B. & O. Railroad"),
            Prop(26, "Atlantic Avenue", 260, 130, new[] { 22, 110, 330, 800, 975, 1150 }, 150, ColorGroup.Yellow),
            Prop(27, "Ventnor Avenue", 260, 130, new[] { 22, 110, 330, 800, 975, 1150 }, 150, ColorGroup.Yellow),
            Utility(28, "Water Works"),
            Prop(29, "Marvin Gardens", 280, 140, new[] { 24, 120, 360, 850, 1025, 1200 }, 150, ColorGroup.Yellow),
            new BoardSpaceData(30, "Go To Jail", SpaceType.GoToJail),
            Prop(31, "Pacific Avenue", 300, 150, new[] { 26, 130, 390, 900, 1100, 1275 }, 200, ColorGroup.Green),
            Prop(32, "North Carolina Avenue", 300, 150, new[] { 26, 130, 390, 900, 1100, 1275 }, 200, ColorGroup.Green),
            new BoardSpaceData(33, "Community Chest", SpaceType.CommunityChest),
            Prop(34, "Pennsylvania Avenue", 320, 160, new[] { 28, 150, 450, 1000, 1200, 1400 }, 200, ColorGroup.Green),
            Rail(35, "Short Line"),
            new BoardSpaceData(36, "Chance", SpaceType.Chance),
            Prop(37, "Park Place", 350, 175, new[] { 35, 175, 500, 1100, 1300, 1500 }, 200, ColorGroup.DarkBlue),
            new BoardSpaceData(38, "Luxury Tax", SpaceType.LuxuryTax, TaxAmount: MonopolyRules.LuxuryTax),
            Prop(39, "Boardwalk", 400, 200, new[] { 50, 200, 600, 1400, 1700, 2000 }, 200, ColorGroup.DarkBlue)
        };
    }

    private static IEnumerable<CardData> BuildChanceCards()
    {
        return new[]
        {
            new CardData("chance_go", CardDeckType.Chance, "Advance to GO", "Collect $200.", CardActionType.AdvanceToSpace, TargetSpace: 0),
            new CardData("chance_illinois", CardDeckType.Chance, "Advance to Illinois Avenue", "If you pass GO, collect $200.", CardActionType.AdvanceToSpace, TargetSpace: 24),
            new CardData("chance_st_charles", CardDeckType.Chance, "Advance to St. Charles Place", "If you pass GO, collect $200.", CardActionType.AdvanceToSpace, TargetSpace: 11),
            new CardData("chance_boardwalk", CardDeckType.Chance, "Advance to Boardwalk", "Move directly to Boardwalk.", CardActionType.AdvanceToSpace, TargetSpace: 39),
            new CardData("chance_nearest_rr_1", CardDeckType.Chance, "Advance to nearest Railroad", "Owner may collect doubled railroad rent.", CardActionType.AdvanceToNearestRailroad),
            new CardData("chance_nearest_rr_2", CardDeckType.Chance, "Advance to nearest Railroad", "Owner may collect doubled railroad rent.", CardActionType.AdvanceToNearestRailroad),
            new CardData("chance_nearest_util", CardDeckType.Chance, "Advance to nearest Utility", "Pay utility rent after moving.", CardActionType.AdvanceToNearestUtility),
            new CardData("chance_bank_dividend", CardDeckType.Chance, "Bank pays you dividend", "Collect $50.", CardActionType.Collect, Amount: 50),
            new CardData("chance_jail_free", CardDeckType.Chance, "Get Out of Jail Free", "Keep this card until needed.", CardActionType.GetOutOfJailFree, KeepUntilUsed: true),
            new CardData("chance_back_three", CardDeckType.Chance, "Go Back 3 Spaces", "Move backward three spaces.", CardActionType.MoveRelative, Amount: -3),
            new CardData("chance_go_jail", CardDeckType.Chance, "Go to Jail", "Go directly to Jail. Do not pass GO.", CardActionType.GoToJail),
            new CardData("chance_repairs", CardDeckType.Chance, "General Repairs", "Pay $25 per house and $100 per hotel.", CardActionType.Repairs, Amount: 25, TargetSpace: 100),
            new CardData("chance_poor_tax", CardDeckType.Chance, "Pay Poor Tax", "Pay $15.", CardActionType.Pay, Amount: 15),
            new CardData("chance_reading_rr", CardDeckType.Chance, "Take a ride on Reading Railroad", "If you pass GO, collect $200.", CardActionType.AdvanceToSpace, TargetSpace: 5),
            new CardData("chance_elected_chair", CardDeckType.Chance, "Elected Chairman", "Pay each player $50.", CardActionType.PayEachPlayer, Amount: 50),
            new CardData("chance_building_loan", CardDeckType.Chance, "Building loan matures", "Collect $150.", CardActionType.Collect, Amount: 150),
            new CardData("chance_crossword", CardDeckType.Chance, "Crossword Competition", "Collect $100.", CardActionType.Collect, Amount: 100),
            new CardData("chance_speeding", CardDeckType.Chance, "Speeding Fine", "Pay $25.", CardActionType.Pay, Amount: 25),
            new CardData("chance_holiday", CardDeckType.Chance, "Holiday Fund", "Collect $75.", CardActionType.Collect, Amount: 75),
            new CardData("chance_market", CardDeckType.Chance, "Market Rally", "Collect $30 from each player.", CardActionType.CollectFromEachPlayer, Amount: 30)
        };
    }

    private static IEnumerable<CardData> BuildCommunityChestCards()
    {
        return new[]
        {
            new CardData("chest_go", CardDeckType.CommunityChest, "Advance to GO", "Collect $200.", CardActionType.AdvanceToSpace, TargetSpace: 0),
            new CardData("chest_bank_error", CardDeckType.CommunityChest, "Bank Error", "Collect $200.", CardActionType.Collect, Amount: 200),
            new CardData("chest_doctor", CardDeckType.CommunityChest, "Doctor's Fees", "Pay $50.", CardActionType.Pay, Amount: 50),
            new CardData("chest_stock", CardDeckType.CommunityChest, "Stock Sale", "Collect $50.", CardActionType.Collect, Amount: 50),
            new CardData("chest_jail_free", CardDeckType.CommunityChest, "Get Out of Jail Free", "Keep this card until needed.", CardActionType.GetOutOfJailFree, KeepUntilUsed: true),
            new CardData("chest_go_jail", CardDeckType.CommunityChest, "Go to Jail", "Go directly to Jail. Do not pass GO.", CardActionType.GoToJail),
            new CardData("chest_holiday", CardDeckType.CommunityChest, "Holiday Fund", "Collect $100.", CardActionType.Collect, Amount: 100),
            new CardData("chest_tax_refund", CardDeckType.CommunityChest, "Income Tax Refund", "Collect $20.", CardActionType.Collect, Amount: 20),
            new CardData("chest_birthday", CardDeckType.CommunityChest, "Birthday", "Collect $10 from each player.", CardActionType.CollectFromEachPlayer, Amount: 10),
            new CardData("chest_life_insurance", CardDeckType.CommunityChest, "Life Insurance", "Collect $100.", CardActionType.Collect, Amount: 100),
            new CardData("chest_hospital", CardDeckType.CommunityChest, "Hospital Fees", "Pay $100.", CardActionType.Pay, Amount: 100),
            new CardData("chest_school", CardDeckType.CommunityChest, "School Fees", "Pay $50.", CardActionType.Pay, Amount: 50),
            new CardData("chest_consulting", CardDeckType.CommunityChest, "Consulting Fee", "Collect $25.", CardActionType.Collect, Amount: 25),
            new CardData("chest_repairs", CardDeckType.CommunityChest, "Street Repairs", "Pay $40 per house and $115 per hotel.", CardActionType.Repairs, Amount: 40, TargetSpace: 115),
            new CardData("chest_beauty", CardDeckType.CommunityChest, "Beauty Contest", "Collect $10.", CardActionType.Collect, Amount: 10),
            new CardData("chest_inheritance", CardDeckType.CommunityChest, "Inheritance", "Collect $100.", CardActionType.Collect, Amount: 100),
            new CardData("chest_charity", CardDeckType.CommunityChest, "Charity Donation", "Pay $45.", CardActionType.Pay, Amount: 45),
            new CardData("chest_bonus", CardDeckType.CommunityChest, "Annual Bonus", "Collect $75.", CardActionType.Collect, Amount: 75),
            new CardData("chest_rebate", CardDeckType.CommunityChest, "Utility Rebate", "Collect $30.", CardActionType.Collect, Amount: 30),
            new CardData("chest_assessment", CardDeckType.CommunityChest, "City Assessment", "Pay $25 to each player.", CardActionType.PayEachPlayer, Amount: 25)
        };
    }
}
