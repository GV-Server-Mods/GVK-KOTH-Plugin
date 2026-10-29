# GVK KOTH

King of the Hill for the GV: Deserts of Kharak (GVK) Space Engineers server, as a Torch plugin. It replaces the King of the Hill - The Complete Edition mod (Workshop 2388326362) and the Space-Chemist KOTH plugin.

**Status:** in development. The design and the full feature list are in [GVK-Settings#741](https://github.com/GV-Server-Mods/GVK-Settings/issues/741).

## How KOTH works on GVK
- Every faction in a zone fills one shared capture bar; more players fill it faster (levelling off below 2x).
- When the bar fills, every faction in the zone scores points and a small credit payout, and the bar starts again.
- Zone points from all factions together drop loot into a public container that players fight over.
- Zones run on weekly slots (a random zone per slot), all the time, or for a set time on an NPC grid.

## Projects
| Project | What it is |
|---|---|
| `GVK_KOTH` | The Torch plugin |
| `GVK_KOTH.Core` | Capture, scoring, schedule and config rules, with no Torch or game references |
| `GVK_KOTH.Tests` | Rule tests: `dotnet test GVK_KOTH.Tests` |

## Build
`dotnet build -c Release` builds the plugin and copies `GVK-KOTH.zip` into `$(TorchDir)\Plugins` (set in `GVK_KOTH/GVK_KOTH.csproj`, default `C:\SE_GVK_S10`).

## Config and data
- Settings: `Instance\GVK_KOTH.cfg`. Edit it and run `!koth reload`; problems are listed in chat and the log.
- Scores, the score log (CSV) and saved state: `Instance\GVK_KOTH\`. To start a new season, stop the server and delete that folder.

## License
AGPL-3.0, see `LICENSE.txt`.
