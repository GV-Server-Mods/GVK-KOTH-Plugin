# GVK KOTH

King of the Hill for the GV: Deserts of Kharak (GVK) Space Engineers server, as a Torch plugin. It replaces the King of the Hill - The Complete Edition mod (Workshop 2388326362) and the Space-Chemist KOTH plugin.

**Status:** plugin code complete for the #741 feature list and in testing; the player HUD is on the GVK_Settings branch `feat/koth-client`. 70 rule tests pass (`dotnet test GVK_KOTH.Tests`). In game, only the first round of fixed-zone tests (Khar Toba, 2026-10-28) has run; NPC zones, rules, the API, Flying KOTH and the player HUD are untested in game ([TEST_PLAN.md](TEST_PLAN.md)). The design and the full feature list are in [GVK-Settings#741](https://github.com/GV-Server-Mods/GVK-Settings/issues/741).

## How KOTH works on GVK
- Every faction in a zone fills one shared capture bar; more players fill it faster (levelling off below 2x).
- When the bar fills, every faction in the zone scores points and a small credit payout, and the bar starts again.
- Zone points from all factions together drop loot into a public container that players fight over.
- Zones run on weekly slots (a random zone per slot), all the time, or for a set time on an NPC grid.
- Rules apply around each enabled fixed zone all the time, whether or not it is open, and around an NPC zone while its grid is here and the zone hasn't ended; never to NPC grids, and not while the plugin's master switch is off. Each rule has its own range (0 = off): player thrusters, large-grid power, safe zones, projectors and drills switch off. A block switched back on inside its range (terminal, timer, script) is switched off again at once.
- The drill rule covers vanilla ship drills, the ToolCore drills in `ToolCoreDrillSubtypes`, and hand drills. Two server-side patches on game code back it up: the server refuses to let a player take out a hand drill inside the range (the check Keen uses for the block placer in safe zones), and a hand drill or vanilla ship drill there digs nothing, even fired from a cockpit. A hand drill carried into the range is put away at the next check.

## Zones in the sample config
`docs/GVK_KOTH.sample.cfg` ships these profiles; the comments there explain every setting.

| Zone | Where | Runs | Who counts |
|---|---|---|---|
| `KharToba`, `Kalash`, `CrashedStarship` | Fixed coordinates | Weekly slots, one picked at random from the pool | Capture; Kalash and Crashed Starship small grids only |
| `Scraptown` | Fixed coordinates | Always | Capture, large grids |
| `KOTHTown` | Fixed coordinates of the NST KOTH Town | Always, but switched off in the sample until the town's spawn group is re-enabled in GVK_Derelicts | Encampment, 3,000 m |

Every zone except `KOTHTown` sets rules: drills 3,000 m, safe zones and projectors 4,000 m, large-grid power 3,000 m (not Khar Toba), thrusters 3,000 m (Crashed Starship and Scraptown).

## Projects
| Project | What it is |
|---|---|
| `GVK_KOTH` | The Torch plugin |
| `GVK_KOTH.Core` | Capture, scoring, schedule, config rules and the data-file reader/writer, with no Torch or game references |
| `GVK_KOTH.Tests` | Rule tests: `dotnet test GVK_KOTH.Tests` |

## Build
`dotnet build -c Release` builds the plugin and copies `GVK-KOTH.zip` into `$(TorchDir)\Plugins` (set in `GVK_KOTH/GVK_KOTH.csproj`, default `C:\SE_GVK_S10`).

## Config and data
- Settings: `Instance\GVK_KOTH.cfg`. Edit it and run `!koth reload`; problems are listed in chat and the log.
- Scores, the score log (CSV) and saved state: `Instance\GVK_KOTH\`. To start a new season, stop the server and delete that folder.
- `Scores.xml` and `State.xml` are UTF-8. If one can't be read, the plugin copies it to `<name>.bad-<time>`, alerts the admin and starts empty; fix or rename the copy back (server stopped) to recover it. Builds before this fix saved the files in a way they couldn't read back, so their `.bad-*` copies are the real totals.

## For other mods
Mods can change season points and read which zones are open, and MES can read `GVK_KOTH_<Zone>_Active`: see [docs/API.md](docs/API.md).

## License
AGPL-3.0, see `LICENSE.txt`.
