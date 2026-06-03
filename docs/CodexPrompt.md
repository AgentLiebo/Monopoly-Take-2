# Monopoly Game Clone (Educational Project)

This repository implements the provided Codex prompt as a Unity-ready C# gameplay layer. The scripts live in `Assets/Scripts/Monopoly` and are designed to be connected to Unity UI, animation, and scene prefabs.

## Implemented requirement mapping

| Requirement area | Implementation |
| --- | --- |
| Standard board | `BoardManager` builds all 40 spaces. |
| Players | `PlayerManager` creates 2–8 players with unique tokens and starting money. |
| Movement | `GameManager.TakeTurn`, `RollDice`, `MovePlayer`, doubles tracking, Speed Die toggle. |
| Property system | `PropertyManager` plus `GameManager.ResolvePropertyLanding`. |
| Color sets | `PropertyManager.OwnsFullSet` and rent calculation. |
| Houses/hotels | `CanBuildHouse`, `BuildHouseOrHotel`, even-building checks. |
| Auctions | `DeclinePendingPropertyAndStartAuction`, bid/pass/close methods. |
| Cards | 20 Chance and 20 Community Chest cards in `BoardManager`. |
| Jail | `PlayerManager` Jail helpers and `GameManager.ResolveJailRoll`. |
| Taxes | Income and Luxury tax settings and landing resolution. |
| Trading | `TradeManager` proposals with accept/reject. |
| Mortgage | Mortgage and unmortgage in `PropertyManager`. |
| Bankruptcy | Liquidation and asset transfer in `PlayerManager`. |
| AI | `AIManager` buy, bid, build, mortgage, simple trade heuristics. |
| Win condition | `GameManager.CheckWinCondition`. |
| UI | `UiModelFactory` and presentation records for Unity binding. |
| Save/load | `SaveManager` JSON persistence. |
| Extra features | Speed Die, local multiplayer-ready state, turn history, ownership overview data, settings. |
