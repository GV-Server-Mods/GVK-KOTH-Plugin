# GVK KOTH test plan

In-game tests on the dev Torch instance (`C:\SE_GVK_S10`, join with direct connect to `localhost`), using the published Workshop mods. A Torch plugin can't run in an offline world. Each stage adds its tests here. Run the rule tests first: `dotnet test GVK_KOTH.Tests`.

No NPC mod changes are needed: NPC zone and linked-block tags can be added in game. With admin "use terminals" on, edit the NPC block's CustomData, then run `!koth reload` (it rescans every grid).

## Setup
1. `dotnet build -c Release` (deploys `Plugins\GVK-KOTH.zip`).
2. Remove `Plugins\SenX-KOTH-Plugin.zip` and `Plugins\Koth-Plugin.zip` from the test instance, so only one KOTH plugin runs.
3. Copy `docs\GVK_KOTH.sample.cfg` to `Instance\GVK_KOTH.cfg`.
4. At one site, name a cargo container on the KOTH structure `KOTH Loot`.
5. Start Torch. The log should show `GVK KOTH started with 4 zones and 3 slots.` and no config warnings.

## Stage 3: fixed zones
| # | Do | Pass when |
|---|---|---|
| 1 | `!koth reload` twice | Both reply "4 zones, 3 slots, no problems", and `GVK_KOTH.cfg` is unchanged on disk (comments and `SlotWarningMinutes` intact). Break a value (e.g. `Radius` -1), reload: the problem is listed. Fix it. |
| 2 | `!koth list` as a player | Lists KharToba, Kalash, CrashedStarship, Scraptown with next times; open zones get a GPS added to your GPS list. |
| 3 | `!koth start KharToba`, then `!koth status` | KharToba shows OPEN; chat shows "KOTH KharToba is open! GPS added." and a "KOTH KharToba" GPS appears in the GPS list (terminal GPS tab). `!koth stop KharToba` removes it. |
| 4 | Fly a powered small grid of 50+ blocks into the zone and sit in its cockpit | `!koth status` shows counted 1 and the bar rising; after about 5 minutes chat says your faction scored 5 points and 1,000,000 SC; faction balance goes up. |
| 5 | Repeat with a grid under 50 blocks, an unpowered grid, a station, and on foot | Counted stays 0. |
| 6 | Second faction joins | Bar fills faster (about 4 minutes for two players); both factions score; one loot drop lands in `KOTH Loot` and a "KOTH KharToba Loot" GPS is added for players within 15 km. |
| 7a | Build your own container named `KOTH Loot` in the zone (unowned or yours), earn a drop | Loot goes only to the NPC-owned `KOTH Loot` container, never yours. With two NPC-owned ones, it goes to the one nearest the centre, never both. |
| 7 | Rename the container, earn a drop | `!koth status` shows a queued drop; rename it back: the drop arrives within 10 seconds. |
| 8 | Leave the zone mid-bar | The bar drains (about 3.5 minutes from full). |
| 9 | Restart Torch mid-bar | `!koth status` shows the bar and queued drops kept. |
| 10 | `!koth score`, `!koth points <tag> -3 test` | Standings shown; the change is announced and appears in `Instance\GVK_KOTH\ScoreLog.csv`. |
| 11 | `!koth stop KharToba`, `!koth auto KharToba` | Zone closes with a chat message, then returns to its schedule. |
| 12 | Set a slot to start 12 minutes from now, reload | A 10-minute warning appears in chat, then the zone opens at the slot time and closes after its duration. |

## Stage 4: NPC zones
Setup: add this zone to `GVK_KOTH.cfg`. For each test, spawn a wreck with `/MES.Spawn...`, add a line `GVK-KOTH=TestWreck;` to its remote control's CustomData, and run `!koth reload`. (In production the line goes in the MES behavior profile, which MES copies into the remote control's CustomData at spawn.)
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
| 1 | Tag a wreck and `!koth reload` | The log shows "NPC zone 'TestWreck' found"; chat says "KOTH TestWreck is open for 10 minutes! GPS added." and a GPS at the wreck appears in the GPS list. |
| 2 | `!koth list` and `!koth status` | TestWreck is listed as OPEN while it runs. |
| 3 | Capture it with a valid grid | Points and SC as for fixed zones. |
| 4 | Wait out the lifetime | Chat says "KOTH TestWreck has closed"; it doesn't reopen. |
| 5 | Tag another, then grind or destroy its remote control | Chat says "has ended: its control was destroyed"; the zone ends at once. |
| 6 | Tag another, fly away until the concealment plugin hides it, come back | No new "open" message; the zone is back with its remaining time. |
| 7 | Tag another, then `/MES.Clear` (or let it despawn) | The zone disappears quietly. |
| 8 | Tag a wreck with an unknown name and `!koth reload` | The admin alerts webhook (or log) reports the unknown profile once. |

## Stage 5: KOTH rules and linked blocks
The old GVK_Settings rule scripts still run until cutover, so test at a spot without old rule beacons, or with a zone set to a new test location.

| # | Do | Pass when |
|---|---|---|
| 1 | Fly a player grid with thrusters inside a zone's `NoThrusterRange` | Its thrusters switch off within a second; you get one chat notice per minute ("thrusters are switched off within 3 km of ..."). Turning them on again switches them off again. |
| 2 | Bring a large grid inside `NoLargeGridPowerRange` | Its batteries, reactors, solar, wind and hydrogen engines switch off. A small grid is unaffected. |
| 3 | Place a safe zone block inside `NoSafeZoneRange`, and a projector inside `NoProjectorRange` | Each switches off; with one range set to 0, that block type is left alone. |
| 4 | Same with an NPC grid | Nothing changes on NPC grids. |
| 5 | NPC zone: destroy its remote control | Its rules stop applying at once. |
| 6 | Set a zone's `LinkRange`, add `[GVK-KOTH-Link]` to the CustomData of a store block on an NPC grid near it, `!koth reload` | The store is off while the zone is closed and on while it's open (`!koth start` / `!koth stop`). |
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

## Stage 7: Flying KOTH spawn (plugin side)
Setup: a zone with `Attach` NpcTag, `Activation` Slot, `SpawnGroup` set to the airship's spawn group (with `[RivalAiSpawn:true]`), X/Y/Z at the spawn point, its behavior carrying `GVK-KOTH=<Name>;`, and a slot whose Pool contains only it, starting in a few minutes.

| # | Do | Pass when |
|---|---|---|
| 1 | Wait for the slot | The log shows "spawned '<SpawnGroup>' for slot"; the airship appears upright at X/Y/Z; the zone opens on it once MES writes the behavior. This also confirms the plugin can read the MES API. |
| 2 | Restart Torch during the slot | No second airship spawns. |
| 3 | Slot ends | MES despawns the airship (its own behavior timer); the zone ends quietly. |

## Stage 8: player HUD (GVK_Settings branch `feat/koth-client`)
The test server needs a GVK_Settings build from that branch (published to a test Workshop item, or the server's local mod copy).

| # | Do | Pass when |
|---|---|---|
| 1 | Open a zone (`!koth start KharToba`), open the satellite map (M) | A flashing orange KOTH marker labelled "KOTH KharToba 0%" with a dotted ring; the % updates while capturing. |
| 2 | Check the corner minimap and the compass ribbon | The same flashing marker on both. |
| 3 | F2 > each settings submenu > KOTH toggle | Each hides KOTH markers in that view only; the setting survives a relog. |
| 4 | With a slot scheduled later | The next slot's zone shows as a steady, dimmed marker with its local start time. |
| 5 | Fly into the open zone | The capture panel appears on the left (name, %, bar, counted); it hides when you leave. |
| 6 | Approach within 1 km of the zone edge | A faint orange wireframe sphere marks the capture radius. |
| 7 | Complete a bar | Firework bursts above the zone for players within 5 km. |
| 7b | Flying KOTH: fly near the moving airship | The boundary sphere moves smoothly with it (no once-a-second jumps); the panel appears as soon as you're inside. |
| 8 | Stop the plugin (or wait with no zones) | Markers and the panel disappear within 30 seconds. |
| 9 | Profiler with the map open near an open KOTH | No noticeable cost from GVK_Settings compared with no KOTH. |
