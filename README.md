# Monopoly Take 2

Educational Monopoly-style board game foundation for Unity, implemented in modular C#.

## What is included

The project provides a complete rules-first gameplay layer that Unity scenes and UI prefabs can call into:

- Standard 40-space Monopoly board with GO, streets, railroads, utilities, taxes, Chance, Community Chest, Jail, Free Parking, and Go To Jail.
- Two to eight players, unique tokens, $1,500 starting money, and randomized dice-roll turn order.
- Two-dice movement, GO salary, doubles extra turns, three-doubles Jail penalty, and optional Speed Die mode.
- Property purchase, auction, rent, monopoly color-set rent doubling, houses, hotels, even-building enforcement, mortgages, and unmortgage interest.
- Chance and Community Chest decks with 20 cards each, including movement, payments, collections, repairs, Jail, and Get Out of Jail Free cards.
- Jail, taxes, trading, bankruptcy, AI buying/building/mortgaging/trading heuristics, save/load, turn history, statistics-ready UI models, and game-over detection.

## Unity integration notes

The code intentionally avoids hard dependencies on `UnityEngine` so it can be unit-tested with .NET and dropped into Unity under `Assets/Scripts/Monopoly`. In a Unity scene, create a MonoBehaviour adapter that owns a `GameManager`, binds button events to methods such as `TakeTurn`, `BuyPendingProperty`, `DeclinePendingPropertyAndStartAuction`, `PlaceAuctionBid`, and uses `UiModelFactory.Create` to populate panels, ownership markers, house/hotel visuals, trade windows, auction windows, dice animation, and history logs.

## Core architecture

- `GameManager` orchestrates setup, turns, dice, movement, landing resolution, card actions, auctions, and win condition.
- `BoardManager` owns the 40-space board and creates shuffled Chance and Community Chest decks.
- `PlayerManager` handles payments, Jail release, liquidation, and bankruptcy.
- `PropertyManager` handles ownership, rent, color sets, houses, hotels, mortgages, and property transfer.
- `TradeManager` validates and executes money/property/Get Out of Jail Free card trades.
- `AIManager` implements simple opponent decisions for purchasing, bidding, building, mortgaging, and trade proposals.
- `SaveManager` persists and restores game state as JSON.
- `UiModelFactory` emits presentation models for property panels, player HUDs, trade/auction windows, turn logs, and statistics screens.

## Building an executable

The gameplay core includes a small console smoke runner so it can be compiled outside Unity. Build and run locally with:

```bash
dotnet run --project MonopolyTake2.Core.csproj -- --turns 10 --seed 42
```

Publish a Windows executable with:

```bash
dotnet publish MonopolyTake2.Core.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/win-x64
```

The generated executable is `publish/win-x64/MonopolyTake2.Core.exe`. It accepts `--turns`, `--seed`, `--speed-die`, and `--save` options.
