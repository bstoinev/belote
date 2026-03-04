# Belote (Bulgarian rules) – MVP-1

Production-quality, extensible .NET solution for an online Belote game with a strict separation between:

- `Belote.Engine`: pure deterministic rules/state/scoring (no SignalR/UI)
- `Belote.Server`: ASP.NET Core + SignalR authoritative server (single continuous table)
- `Belote.Client`: Blazor WebAssembly spectator UI with themes + localization
- `Belote.Tests`: xUnit tests for rules/scoring/rotation/obligations

## Stack

- .NET SDK: `net10.0` (installed in the current environment). The code is written to be portable to newer/older LTS SDKs if you retarget.
- Server: ASP.NET Core + SignalR
- Client: Blazor WebAssembly (served by `Belote.Server`)
- Tests: xUnit

## Run

From the repo root:

1. `dotnet run --project .\\Belote.Server\\Belote.Server.csproj`
2. Open the **printed** URL (`Now listening on: ...`). (Ports are ephemeral by default to avoid conflicts.)

Optional:

- Theme override: add `?theme=default` (the active theme is read from query string or `Belote.Client/wwwroot/appsettings.json`).
- Spectator nickname: add `?nick=YourName` (stored in-memory on the server; seating is not implemented in MVP-1).
- Language: use the in-app `EN/BG` selector (stored in `localStorage` and applied on reload).

## What MVP-1 does

- A single continuous table (`Table-1`) runs forever.
- 4 deterministic robot players:
  - Perform the required *cut* step.
  - Bid (including multi-round bidding; passing doesn’t lock you out).
  - Declare announcements (eldest hand, only before the first card).
  - Play legal cards to finish full hands end-to-end.
- Spectator UI:
  - Top-down table layout with 4 seats (N/E/S/W).
  - Shows all 4 hands face-up (debug spectator mode).
  - Shows current trick in the center.
  - Shows phase, dealer/cutter/eldest markers, whose turn it is.
  - Shows bidding log and action log.
  - Shows match score panel (`N/S` vs `E/W`) to 151.

## Determinism & replay

- Each hand has a deterministic `Seed` (`Belote.Engine.Hand.BeloteHandState.Seed`).
- Shuffle uses a deterministic PRNG (`Belote.Engine.Prng.XorShift128Plus`), not `System.Random`.
- Server keeps an in-memory action log of accepted engine commands for each hand.
- UI button **Replay last hand** asks the server to:
  1) rebuild the hand from its seed + dealer and
  2) replay the recorded commands
  3) compare hashes (`Belote.Engine.Hand.BeloteHandCanonical.ComputeHash`)

## Scoring note (Contra/Recontra + Inside)

This MVP applies doubling as follows (documented choice per spec):

- Normal hand (bidding team not “inside”):
  - Bidding team’s final hand total is multiplied by `Contra/Recontra`.
  - Defenders’ final hand total is **not** multiplied.
- “Inside” hand (“Vutre” – bidding team has fewer trick points than defenders):
  - Bidding team gets `0`.
  - Defenders receive **all points from the hand** (both teams’ trick points + all bonuses) multiplied by `Contra/Recontra`.

## Themes / assets

- No card art is embedded in the engine.
- Default theme lives in `Belote.Client/wwwroot/themes/default/`:
  - `table.svg`
  - `card-back.svg`
  - `cards/*.svg` (32 faces)

Card rendering goes through `Belote.Client.Services.ThemeService.CardFacePath(card)`.

## Chat (stub)

Chat UI is present but intentionally disabled (no persistence/moderation yet).

- Server stub: `Belote.Server/Hubs/TableHub.cs` (`SendChat`)
- Shared DTO: `Belote.Engine/Dto/TransportDtos.cs` (`ChatMessageDto`)

## Tests

Run:

- `dotnet test .\\Belote.Tests\\Belote.Tests.csproj -c Release`

Includes coverage for:

- bidding ladder + pass-then-bid-higher
- bidding end condition + all-pass cancel
- CCW rotation for dealer/cutter/eldest and play turn order
- follow suit / must trump / discard
- raising rules (AT vs NT vs suit contract)
- scoring (last trick +10, doubling, inside rule)
