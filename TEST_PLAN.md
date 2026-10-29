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

## Stage 4: NPC zones
Setup: add a line `GVK-KOTH=TestWreck;` to one wreck's MES behavior profile in GVK_Derelicts (it lands in the remote control's CustomData), build GVK_Derelicts, and add this zone to `GVK_KOTH.cfg`:
```xml
<ZoneProfile>
  <Name>TestWreck</Name>
  <Attach>NpcTag</Attach>
  <Activation>Lifetime</Activation>
  <LifetimeMinutes>10</LifetimeMinutes>
  <Radius>500</Radius>
  <CaptureSeconds>60</CaptureSeconds>
  <PointsPerLoot>0</PointsPerLoot>
</ZoneProfile>
```

| # | Do | Pass when |
|---|---|---|
| 1 | Spawn the wreck with `/MES.Spawn...` | The log shows "NPC zone 'TestWreck' found"; chat says "KOTH TestWreck is open for 10 minutes!" with a GPS at the wreck. |
| 2 | `!koth` and `!koth status` | TestWreck is listed as OPEN while it runs. |
| 3 | Capture it with a valid grid | Points and SC as for fixed zones. |
| 4 | Wait out the lifetime | Chat says "KOTH TestWreck has closed"; it doesn't reopen. |
| 5 | Spawn another, then grind or destroy its remote control | Chat says "has ended: its control was destroyed"; the zone ends at once. |
| 6 | Spawn another, fly away until the concealment plugin hides it, come back | No new "open" message; the zone is back with its remaining time. |
| 7 | Spawn another, then `/MES.Clear` (or let it despawn) | The zone disappears quietly. |
| 8 | Put a tag with an unknown name on a behavior | The admin alerts webhook (or log) reports the unknown profile once. |

## Stage 5: KOTH rules and linked blocks
The old GVK_Settings rule scripts still run until cutover, so test at a spot without old rule beacons, or with a zone set to a new test location.

| # | Do | Pass when |
|---|---|---|
| 1 | Fly a player grid with thrusters inside a zone's `NoThrusterRange` | Its thrusters switch off within a second; you get one chat notice per minute ("thrusters are switched off within 3 km of ..."). Turning them on again switches them off again. |
| 2 | Bring a large grid inside `NoLargeGridPowerRange` | Its batteries, reactors, solar, wind and hydrogen engines switch off. A small grid is unaffected. |
| 3 | Place a safe zone block or projector inside `NoSafeZoneRange` | It switches off. |
| 4 | Same with an NPC grid | Nothing changes on NPC grids. |
| 5 | NPC zone: destroy its remote control | Its rules stop applying at once. |
| 6 | Add `[GVK-KOTH-Link]` to a store block's CustomData on an NPC grid near a zone, set that zone's `LinkRange`, spawn it | The store is off while the zone is closed and on while it's open (`!koth start` / `!koth stop`). |
| 7 | Same with `[GVK-KOTH-Link:KharToba]` far from Khar Toba | It follows Khar Toba only, regardless of distance. |
| 8 | Conceal and reveal the camp | The store comes back in the right state. |

## Stage 6: mod API and MES sandbox booleans
| # | Do | Pass when |
|---|---|---|
| 1 | `!koth start KharToba`, then check an MES condition or `/MES.Debug...` for `GVK_KOTH_KharToba_Active` | It is true while open and false after `!koth stop KharToba`. |
| 2 | Restart Torch while a zone is open, then stop it | The boolean is false again after the zone closes (no stale true). |
| 3 | A test mod with the `GvkKothApi` class from `docs/API.md` calls `AddPoints(factionId, 3, "Test", "API test")` | Chat and Discord say "[TAG] gained 3 KOTH points: API test"; `ScoreLog.csv` has a line with source "API: Test". This also confirms mods reach the plugin through mod messages. |
| 4 | Same with -100 on a faction with fewer points | The total stops at 0. |
| 5 | The test mod registers `AddZoneListener` and you open and close a zone | The callback fires with the zone name and true, then false. |
