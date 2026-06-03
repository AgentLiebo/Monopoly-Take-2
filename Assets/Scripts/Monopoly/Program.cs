using System;
using System.IO;
using System.Linq;

namespace MonopolyTake2;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        var seed = TryReadSeed(args);
        var turns = TryReadTurns(args) ?? 10;
        var game = new GameManager(seed);
        game.NewGame(new[]
        {
            ("Player 1", TokenType.Car, false),
            ("CPU 1", TokenType.Dog, true),
            ("CPU 2", TokenType.Hat, true),
            ("CPU 3", TokenType.Battleship, true)
        }, new GameSettings { SpeedDieEnabled = args.Contains("--speed-die", StringComparer.OrdinalIgnoreCase) });

        Console.WriteLine("Monopoly Take 2 executable smoke runner");
        Console.WriteLine("---------------------------------------");
        Console.WriteLine($"Players: {string.Join(", ", game.State.Players.Select(p => p.Name))}");
        Console.WriteLine($"Speed Die: {(game.State.Settings.SpeedDieEnabled ? "on" : "off")}");
        Console.WriteLine();

        for (var turn = 1; turn <= turns && game.State.Phase != TurnPhase.GameOver; turn++)
        {
            ResolvePendingDecisions(game);
            var player = game.State.CurrentPlayer;
            var roll = game.TakeTurn();
            ResolvePendingDecisions(game);
            Console.WriteLine($"Turn {turn:00}: {player.Name} rolled {roll.Total}; phase={game.State.Phase}; cash=${player.Money}; space={game.Board.GetSpace(player.Position).Name}");
        }

        Console.WriteLine();
        Console.WriteLine("Final standings:");
        foreach (var player in game.State.Players.OrderByDescending(p => p.Money))
        {
            Console.WriteLine($"- {player.Name}: ${player.Money}, properties={player.OwnedPropertyIndexes.Count}, bankrupt={player.Bankrupt}");
        }

        var savePath = TryReadSavePath(args);
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            game.Saves.SaveGame(game.State, savePath);
            Console.WriteLine($"Saved game state to {Path.GetFullPath(savePath)}");
        }

        return 0;
    }

    private static void ResolvePendingDecisions(GameManager game)
    {
        while (game.State.Phase is TurnPhase.AwaitingPurchaseDecision or TurnPhase.Auction)
        {
            if (game.State.Phase == TurnPhase.AwaitingPurchaseDecision)
            {
                var player = game.State.CurrentPlayer;
                var propertyIndex = game.State.PendingPurchasePropertyIndex!.Value;
                var space = game.Board.GetSpace(propertyIndex);
                if (player.Money >= space.Price + 100)
                {
                    game.BuyPendingProperty();
                }
                else
                {
                    game.DeclinePendingPropertyAndStartAuction();
                }
            }

            if (game.State.Phase == TurnPhase.Auction)
            {
                RunAutomatedAuction(game);
            }
        }
    }

    private static void RunAutomatedAuction(GameManager game)
    {
        var auction = game.State.CurrentAuction ?? throw new InvalidOperationException("No active auction.");
        foreach (var player in game.State.Players.Where(p => !p.Bankrupt && auction.ActiveBidderIds.Contains(p.Id)).ToArray())
        {
            var bid = player.IsAi
                ? game.Ai.ChooseAuctionBid(game.State, player, auction.PropertyIndex, auction.HighestBid)
                : Math.Min(player.Money - 100, auction.HighestBid + 10);
            if (bid > auction.HighestBid)
            {
                game.PlaceAuctionBid(player.Id, bid);
            }
            else
            {
                game.PassAuctionBid(player.Id);
            }
        }

        if (game.State.CurrentAuction != null)
        {
            game.CloseAuction();
        }
    }

    private static int? TryReadTurns(string[] args)
    {
        return TryReadIntOption(args, "--turns");
    }

    private static int? TryReadSeed(string[] args)
    {
        return TryReadIntOption(args, "--seed");
    }

    private static int? TryReadIntOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryReadSavePath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--save", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Monopoly Take 2");
        Console.WriteLine("Usage: MonopolyTake2.Core [--turns N] [--seed N] [--speed-die] [--save path]");
        Console.WriteLine("Publishes to a Windows .exe with: dotnet publish MonopolyTake2.Core.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true");
    }
}
