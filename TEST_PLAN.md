# GVK KOTH test plan

In-game tests on the dev Torch instance (`C:\SE_GVK_S10`). Each stage adds its tests here. Run the rule tests first: `dotnet test GVK_KOTH.Tests`.

## Setup
1. `dotnet build -c Release` (deploys `Plugins\GVK-KOTH.zip`).
2. Remove `Plugins\SenX-KOTH-Plugin.zip` and `Plugins\Koth-Plugin.zip` from the test instance, so only one KOTH plugin runs.
3. Copy `docs\GVK_KOTH.sample.cfg` to `Instance\GVK_KOTH.cfg`.
4. At one site, name a cargo container on the KOTH structure `KOTH Loot`.
5. Start Torch. The log should show `GVK KOTH started with 4 zones and 3 slots.` and no config warnings.

## Stage 3: fixed zones
| # | Do | Pass when |
|---|---|---|
| 1 | `!koth reload` | Replies "no problems". Break a value (e.g. `Radius` -1), reload: the problem is listed. Fix it. |
| 2 | `!koth` as a player | Lists KharToba, Kalash, CrashedStarship, Scraptown with next times and GPS. |
| 3 | `!koth start KharToba`, then `!koth status` | KharToba shows OPEN; chat shows "KOTH KharToba is open!" with a GPS. |
| 4 | Fly a powered small grid of 50+ blocks into the zone and sit in its cockpit | `!koth status` shows counted 1 and the bar rising; after about 5 minutes chat says your faction scored 5 points and 1,000,000 SC; faction balance goes up. |
| 5 | Repeat with a grid under 50 blocks, an unpowered grid, a station, and on foot | Counted stays 0. |
| 6 | Second faction joins | Bar fills faster (about 4 minutes for two players); both factions score; one loot drop lands in `KOTH Loot` with a chat GPS. |
| 7 | Rename the container, earn a drop | `!koth status` shows a queued drop; rename it back: the drop arrives within 10 seconds. |
| 8 | Leave the zone mid-bar | The bar drains (about 3.5 minutes from full). |
| 9 | Restart Torch mid-bar | `!koth status` shows the bar and queued drops kept. |
| 10 | `!koth score`, `!koth points <tag> -3 test` | Standings shown; the change is announced and appears in `Instance\GVK_KOTH\ScoreLog.csv`. |
| 11 | `!koth stop KharToba`, `!koth auto KharToba` | Zone closes with a chat message, then returns to its schedule. |
| 12 | Set a slot to start 12 minutes from now, reload | A 10-minute warning appears in chat, then the zone opens at the slot time and closes after its duration. |
