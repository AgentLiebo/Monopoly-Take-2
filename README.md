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

The core rules code avoids hard dependencies on `UnityEngine` so it can be unit-tested with .NET, while the Unity-specific files under `Assets/Scripts/Monopoly` provide a ready-to-run adapter. `UnityGameController` owns a `GameManager`, wires player decisions into the gameplay API, and can auto-create both the 3D board view and the in-game HUD at runtime.

## Core architecture

- `GameManager` orchestrates setup, turns, dice, movement, landing resolution, card actions, auctions, and win condition.
- `BoardManager` owns the 40-space board and creates shuffled Chance and Community Chest decks.
- `PlayerManager` handles payments, Jail release, liquidation, and bankruptcy.
- `PropertyManager` handles ownership, rent, color sets, houses, hotels, mortgages, and property transfer.
- `TradeManager` validates and executes money/property/Get Out of Jail Free card trades.
- `AIManager` implements simple opponent decisions for purchasing, bidding, building, mortgaging, and trade proposals.
- `SaveManager` persists and restores game state as JSON.
- `UiModelFactory` emits presentation models for property panels, player HUDs, trade/auction windows, turn logs, and statistics screens.


## Unity 3D scene and player interaction

The Unity adapter now creates a playable runtime scene from code. Add `UnityGameController` to an empty GameObject and press Play; it automatically adds:

- `UnityMonopolyBoardView`, which builds a 3D Monopoly-style board with clickable spaces, player tokens, ownership markers, mortgage markers, and house/hotel blocks.
- `UnityMonopolyHud`, which renders an in-game GUI for rolling dice, buying or auctioning properties, bidding/passing auctions, paying Jail fines, using Jail cards, building improvements, mortgaging/unmortgaging, save/load, property inspection, player money, and turn history.

The human player controls decisions from the GUI. AI players can auto-advance their turns while human purchase, auction, building, mortgage, and save/load decisions remain interactive.


## Unity player build

This repository is now a Unity project as well as a .NET gameplay-core project. It includes:

- `ProjectSettings/ProjectVersion.txt` pinned to Unity `6000.4.10f1`.
- `Packages/manifest.json` with the built-in Unity modules used by the runtime-generated 3D board and IMGUI HUD.
- `Assets/Scripts/Monopoly/Editor/UnityBuild.cs`, which creates a playable scene containing `UnityGameController` and builds a Windows x64 Unity player.
- `scripts/build-unity-win64.sh`, a one-command Linux build wrapper.

Build the graphical Windows Unity player with:

```bash
./scripts/build-unity-win64.sh
```

By default, the requested player executable path is:

```text
Builds/Windows/MonopolyTake2.exe
```

Unity desktop builds are not truly single-file: the `.exe` must stay next to the generated Unity data folder and Unity runtime files. Zip the entire `Builds/Windows/` folder when you share the game.

If Unity is installed somewhere else, set `UNITY_EDITOR` before running the script:

```bash
UNITY_EDITOR=/path/to/Unity ./scripts/build-unity-win64.sh
```

A valid Unity license/sign-in is required for batchmode player builds.

## Building an executable

The gameplay core includes a small console smoke runner so it can be compiled outside Unity. Build and run locally with:

```bash
dotnet run --project MonopolyTake2.Core.csproj -- --turns 10 --seed 42
```

Publish a Windows executable with:

```bash
dotnet publish MonopolyTake2.Core.csproj -c Release -r win-x64
# or use the checked-in publish profile:
dotnet publish MonopolyTake2.Core.csproj /p:PublishProfile=win-x64
```

The generated executable is `publish/win-x64/MonopolyTake2.Core.exe`. It accepts `--turns`, `--seed`, `--speed-die`, and `--save` options. The project explicitly sets `OutputType=Exe`, `StartupObject=MonopolyTake2.Program`, `UseAppHost=true`, and Release runtime publishes default to `PublishSingleFile=true` so the Windows publish is treated as an executable application.
