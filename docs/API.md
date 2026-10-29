# GVK KOTH API for mods

Server-side mods (for example SiegeableSafeZones) can change KOTH season points and see which KOTH zones are open. MES encounters can read a sandbox boolean per zone.

The API uses mod messages, like the WeaponCore API. Mod messages never leave the server, so a client can't call it.

## MES sandbox booleans
For every zone profile (except rule-only ones) the plugin keeps `GVK_KOTH_<Name>_Active` true while any zone with that name is open, e.g. `GVK_KOTH_KharToba_Active`. The plugin sets it with `MyAPIGateway.Utilities.SetVariable`, which is what MES reads (`GetVariable<bool>`), so it works anywhere MES takes a sandbox boolean:
- Spawn conditions: `[SandboxVariables:GVK_KOTH_KharToba_Active]`.
- RivalAI conditions: `[CheckTrueSandboxBooleans:true]` with `[TrueSandboxBooleans:GVK_KOTH_KharToba_Active]`, or `[CheckFalseSandboxBooleans:true]` with `[FalseSandboxBooleans:...]`.

These are game sandbox variables, so the game saves them with the world (the only KOTH data that is). The plugin rewrites every one at startup, so a stale value never survives a restart.

## MES spawns (Flying KOTH)
The plugin calls MES's `CustomSpawnRequest` (MES spawn type OtherNPC). The spawn group's conditions need `[RivalAiSpawn:true]` (or `RivalAiAnySpawn`, `RivalAiAtmosphericSpawn`, `RivalAiSpaceSpawn`), or MES won't pick it. MES writes the behavior profile into the remote control's CustomData before the grid spawns, so a `GVK-KOTH=<Name>;` line in that behavior is there as soon as the grid appears.

## Endpoints
A mod sends the string `"ApiEndpointRequest"` on channel `74100741` and receives a `Dictionary<string, Delegate>`:

| Name | Type | What it does |
|---|---|---|
| `Version` | `Func<int>` | API version, currently 1. |
| `AddPoints` | `Func<long, int, string, string, int>` | `(factionId, change, source, reason)`: adds or (negative) removes season points. Points only, no SC; the total never goes below 0. Announced in chat and on Discord as "[TAG] gained/lost N KOTH points: reason", and logged with `source`. Returns the new total, or -1 if the faction or the plugin isn't available. |
| `IsZoneActive` | `Func<string, bool>` | `(zoneName)`: true while any zone with that profile name is open. |
| `AddZoneListener` | `Action<Action<string, bool>>` | Registers a callback `(zoneName, open)` called when a zone opens or closes. |

Call the endpoints on the game thread (the normal mod update thread).

## Client class to copy into a mod
C# 6, ModAPI whitelist only. Call `Load()` from your session component's `LoadData` (server only) and `Unload()` from `UnloadData`.

```csharp
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;

public class GvkKothApi
{
    private const long Channel = 74100741L;

    private Func<long, int, string, string, int> _addPoints;
    private Func<string, bool> _isZoneActive;
    private Action<Action<string, bool>> _addZoneListener;

    public bool Ready { get; private set; }

    /// <summary>Called once when the API becomes available.</summary>
    public Action OnReady;

    public void Load()
    {
        MyAPIGateway.Utilities.RegisterMessageHandler(Channel, Handle);
        MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
    }

    public void Unload()
    {
        MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, Handle);
        Ready = false;
    }

    private void Handle(object message)
    {
        var endpoints = message as Dictionary<string, Delegate>;
        if (endpoints == null || Ready)
            return;

        Delegate d;
        if (endpoints.TryGetValue("AddPoints", out d)) _addPoints = d as Func<long, int, string, string, int>;
        if (endpoints.TryGetValue("IsZoneActive", out d)) _isZoneActive = d as Func<string, bool>;
        if (endpoints.TryGetValue("AddZoneListener", out d)) _addZoneListener = d as Action<Action<string, bool>>;

        Ready = _addPoints != null && _isZoneActive != null && _addZoneListener != null;
        if (Ready && OnReady != null)
            OnReady();
    }

    /// <summary>Returns the faction's new season total, or -1 if KOTH isn't available.</summary>
    public int AddPoints(long factionId, int change, string source, string reason)
    {
        return Ready ? _addPoints(factionId, change, source, reason) : -1;
    }

    public bool IsZoneActive(string zoneName)
    {
        return Ready && _isZoneActive(zoneName);
    }

    public void AddZoneListener(Action<string, bool> listener)
    {
        if (Ready)
            _addZoneListener(listener);
    }
}
```

Example: take 5 points from a faction whose shield was sieged.
```csharp
_koth.AddPoints(factionId, -5, "SiegeableSafeZones", "Siege: shield broken");
```
