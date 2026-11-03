# Client-server: make Join work

**Part:** Simme · **Time:** 20–25 minutes

**Server:** `tank.simmeprive.nl:7777` · **Unity:** 6000.5.9f1

## Goal

Restore the Join button so your client can join the central server, report the
result, and join again after leaving. Finish with your tank and another player's
tank visible. Max's next section handles movement synchronization; Niels then
handles shot events with RPCs, and Sacha covers latency and rollback.

The central server is already running. This exercise only changes your local client.

## What are we building on?

- The server runs the shared scene, knows the network player prefab, listens on
  a UDP port, and decides whether to admit each client.
- Everyone connects to the same address and port. A local host would create a
  separate session, not join the class.
- Each build already contains the scripts and prefabs. NGO sends spawn/state/event
  messages, not GameObjects or script files. Clients create local representations.
- Our server checks compatibility and capacity, assigns a spawn, and asks NGO to
  create the player object. Those parts are already implemented.
- Gameplay communication goes through the server. Connecting enables that
  communication; movement synchronization and shot RPCs are the next exercises.

## Follow the Join button

```text
ConnectionView: read host/port → controller.Join()
    ↓
ClientConnectionController.JoinRoutine
    configure destination → StartClient() → wait for NGO's result
    ↓
Central server: approve or reject; create player on approval
    ↓
Client callbacks: connected / disconnected + reason → UI feedback
```

**Starting the client is not the same as being connected.** `StartClient()` can
return `true` while the connection is still in progress. Only the connection
callback confirms that the local client connected.

## What is missing?

Open `Assets/Scripts/Networking/Client/ClientConnectionController.cs`.
There are three TODO regions:

1. In `JoinRoutine`: configure the destination and start the client.
2. In `HandleConnected`: record NGO's success notification.
3. In `HandleDisconnected`: capture failure/disconnection and its reason.

The callback subscriptions in `Initialize`, waiting loop, timeout, UI, Leave,
and cleanup are supplied. **Do not change `Protocol.cs` or the server code.**
There is no binary serialization task.

## Step 1 — Request a connection

In `JoinRoutine`, find this starter block:

```csharp
SetState(ConnectionState.Connecting, "Connecting to " + Host + ":" + Port + "...");

// TODO 1: configure the destination and start the client.
bool clientStarted = false;

if (!clientStarted)
{
    Fail("Could not start the client. Is another session still running?");
    yield break;
}
```

Replace the TODO and placeholder with two calls:

- `transport.ApplyClientEndpoint(Host, Port, nm)` uses the values from the Join
  panel. This supplied helper configures UnityTransport and attaches the
  existing compatibility data. You do not need to encode anything yourself.
- `nm.StartClient()` starts the client. Store its return value in `clientStarted`
  so the supplied failure check still works.

Leave the waiting loop below this block intact. **Do not set `InGame` here.**

## Step 2 — Recognize success

`Initialize` already subscribes these methods to NGO:

```csharp
nm.OnClientConnectedCallback += HandleConnected;
nm.OnClientDisconnectCallback += HandleDisconnected;
```

Restore `HandleConnected` so it sets `sawConnected` to `true`. The supplied
waiting loop sees this flag, changes the state to `InGame`, and shows the
connected address. This controller is used as a client for this exercise.

## Step 3 — Recognize failure and disconnection

Keep the guard at the start of `HandleDisconnected`. Below it:

- Set `sawDisconnected` to `true` so the waiting loop stops waiting.
- Save `nm.DisconnectReason` in `lastDisconnectReason` (or `null` if `nm` is null).
- If we were already `InGame`, change the state to `Failed` and show the reason.
  Use `"server closed the connection"` when no reason was provided.

The full solution below shows the exact code. Intentional Leave is handled
separately by the supplied code; it should not become a failure message.

## Test against the central server

Implement all three regions before testing. With only some restored, a tank may
briefly appear while the controller still times out because it missed a callback.

1. Open `Assets/Scenes/SampleScene.unity` and press Play.
2. Enter host **`tank.simmeprive.nl`**, port **`7777`**, then press **Join**.
3. Confirm the panel shows **Connected to tank.simmeprive.nl:7777** and a tank spawns.
4. Ask a classmate to join the same server. Check that both tanks are visible.
5. Press **Leave**, then **Join** again without
   leaving Play mode. You should reconnect.
6. **Bonus:** Leave. Try a deliberately unused port (ask the presenter which one). Confirm
   the UI eventually reports failure instead of claiming you are connected.
7. Restore **7777** and join successfully again.

A wrong port usually produces a timeout, not an explicit rejection. A timeout
means **no successful connection was confirmed**; it does not prove exactly
where the request failed.

## Done checklist

- [ ] Join uses the host/port from the panel and calls `StartClient()`.
- [ ] The UI says Connecting while waiting, not Connected immediately.
- [ ] A connection callback leads to Connected and a spawned tank.
- [ ] Another student's tank is visible in the same session.
- [ ] Leave → Join works without restarting Play mode.
- [ ] Bonus: an unused endpoint eventually produces a failure; the correct one works again.

Movement and shots may not yet work for other players. That is the handoff to
Max and Niels, not a reason to rebuild the server or edit their exercise code.

## Troubleshooting

| What you see | Check |
|---|---|
| `Could not start the client` immediately | TODO 1 still uses `false`, or NGO could not start; inspect the Console |
| Tank appears, then UI reports failure | `HandleConnected` must set `sawConnected`; check callback subscriptions |
| `server not reachable` / timeout | Destination, UDP reachability, supplied timeout, and callback handling; this is not a precise network diagnosis |
| `join:bad` / `join:version` | Server rejected compatibility data; restore the supplied protocol/helper and correct build version |
| `join:full` | Server capacity reached; ask the presenter or wait for a slot |
| Connected but no tank / prefab errors | Check the Console and the workshop scene/prefab versions |
| Other tank visible but movement/shots wrong | Continue with Max's and Niels's sections |

## Full solution — three regions in the same file

### 1. Inside `JoinRoutine`

Keep the rest of the method. Replace its TODO block with:

```csharp
transport.ApplyClientEndpoint(Host, Port, nm);
bool clientStarted = nm.StartClient();

if (!clientStarted)
{
    Fail("Could not start the client. Is another session still running?");
    yield break;
}
```

### 2. `HandleConnected`

```csharp
private void HandleConnected(ulong _)
{
    sawConnected = true;
}
```

### 3. `HandleDisconnected`

```csharp
private void HandleDisconnected(ulong _)
{
    if (destroyed || leaveRequested || !Application.isPlaying)
        return;

    sawDisconnected = true;
    lastDisconnectReason = nm != null ? nm.DisconnectReason : null;

    if (State == ConnectionState.InGame)
    {
        string reason = string.IsNullOrEmpty(lastDisconnectReason)
            ? "server closed the connection"
            : lastDisconnectReason;
        SetState(ConnectionState.Failed, "Disconnected: " + reason);
    }
}
```

## Explain it to your partner

1. Why call `StartClient()` rather than `StartHost()`?
2. Why can `StartClient()` return true before we are connected?
3. Who decides whether we may join and where our tank spawns?
4. Why does connecting not automatically synchronize every field on our tank?

**Next: Max — we share a session; now make movement visible to everyone.**
