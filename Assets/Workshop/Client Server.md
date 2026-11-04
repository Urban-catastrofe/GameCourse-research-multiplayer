# Client-server: getting Join to work

**Part of the Simme workshop · 20 to 25 minutes**

**Server:** `tank.simmeprive.nl:7777` · **Unity:** 6000.5.9f1

## What you're doing

In this exercise you'll fix the Join button on your client so it can connect to the central server, report what happened, and let you join again after leaving. By the end you should see your own tank plus the tank of another player in the scene.

The central server is already running, so you only change your local client. Max handles movement sync in the next section, Niels takes shot events with RPCs after that, and Sacha covers latency and rollback.

## Background for people who've never touched Netcode

If this is your first time with Unity Netcode for GameObjects, here's the short version of what's going on:

- The server runs the shared scene. It knows which prefab represents a player, listens on a UDP port, and decides whether to let each client in.
- Everyone connects to the same address and port. If you started a local host instead you'd create a separate session that only you can see, which defeats the point of the exercise.
- Each build already contains the scripts and prefabs. NGO does not send GameObjects or script files over the network. It sends spawn, state, and event messages, and each client creates its own local copies of the objects it needs.
- The server checks whether your build is compatible and whether there is room, then assigns you a spawn point and asks NGO to create your player object. All of that is already implemented.
- Gameplay communication goes through the server. Connecting is what enables that communication. Movement and shooting come later.

One thing that trips people up: **starting the client is not the same as being connected.** `StartClient()` can return `true` while the connection is still in progress. The only thing that confirms you are actually connected is the connection callback firing.

## How the flow works

```
ConnectionView reads host and port, then calls controller.Join()
    ↓
ClientConnectionController.JoinRoutine configures the destination,
starts the client, and waits for NGO's result
    ↓
Central server approves or rejects, and creates your player on approval
    ↓
Client callbacks fire (connected / disconnected with reason), UI updates
```

## What's missing in the code

Open `Assets/Scripts/Networking/Client/ClientConnectionController.cs`. You'll find three TODO regions:

1. In `JoinRoutine`: configure the destination and start the client.
2. In `HandleConnected`: record NGO's success notification.
3. In `HandleDisconnected`: capture failure or disconnection, along with the reason.

Everything else is already in place. The callback subscriptions in `Initialize`, the waiting loop, the timeout handling, the UI updates, Leave, and cleanup are all supplied. Don't change `Protocol.cs` or the server code. There's no binary serialization task in this exercise either.

## Step 1: Request a connection

In `JoinRoutine` you'll find this starter block:

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

Replace the TODO and the placeholder with two calls:

- `transport.ApplyClientEndpoint(Host, Port, nm)` uses the values from the Join panel. This helper is already supplied and does two things for you: it configures UnityTransport with the right address and port, and it attaches the existing compatibility data. You don't need to encode anything yourself.
- `nm.StartClient()` starts the client. Store its return value in `clientStarted` so the supplied failure check still works.

Leave the waiting loop below this block as it is. Don't set `InGame` here. The waiting loop does that once it sees the connection succeeded.

## Step 2: Recognize success

`Initialize` already subscribes these methods to NGO:

```csharp
nm.OnClientConnectedCallback += HandleConnected;
nm.OnClientDisconnectCallback += HandleDisconnected;
```

Restore `HandleConnected` so it sets `sawConnected` to `true`. The waiting loop that's already in place watches this flag, switches the state to `InGame`, and shows the connected address. For this exercise the controller is only used as a client.

## Step 3: Recognize failure and disconnection

Keep the guard at the start of `HandleDisconnected`. Below it, do three things:

- Set `sawDisconnected` to `true` so the waiting loop stops waiting.
- Save `nm.DisconnectReason` in `lastDisconnectReason` (or `null` if `nm` is null).
- If we were already `InGame`, change the state to `Failed` and show the reason. If there's no reason, use `"server closed the connection"` instead.

Intentional Leave is handled separately by the supplied code, so it won't turn into a failure message.

## Testing against the central server

Implement all three regions before you test. If you only restore some of them, you might see a tank briefly appear while the controller still times out because it missed a callback.

1. Open `Assets/Scenes/SampleScene.unity` and press Play.
2. Enter host `tank.simmeprive.nl`, port `7777`, then press Join.
3. Check that the panel says "Connected to tank.simmeprive.nl:7777" and that your tank spawns.
4. Ask someone else in the room to join the same server. Both tanks should now be visible.
5. Press Leave, then Join again without exiting Play mode. You should reconnect.
6. Bonus: leave, then try a deliberately unused port (ask the presenter which one). The UI should eventually report a failure instead of claiming you're connected.
7. Restore port 7777 and join successfully again.

A wrong port usually produces a timeout rather than an explicit rejection. A timeout means no successful connection was confirmed. It doesn't tell you exactly where the request failed, so don't read too much into it.

## Done checklist

- [ ] Join uses the host and port from the panel and calls `StartClient()`.
- [ ] The UI shows Connecting while waiting, not Connected immediately.
- [ ] A connection callback leads to Connected and a spawned tank.
- [ ] Another student's tank is visible in the same session.
- [ ] Leave then Join works without restarting Play mode.
- [ ] Bonus: an unused endpoint eventually produces a failure; the correct one works again.

Movement and shots may not work for other players yet. That's Max's and Niels's part, so don't rebuild the server or edit their exercise code to compensate.

## Troubleshooting

| Symptom | What to check |
|---|---|
| `Could not start the client` immediately | TODO 1 still uses `false`, or NGO couldn't start. Check the Console. |
| Tank appears briefly, then UI reports failure | `HandleConnected` must set `sawConnected`. Check the callback subscriptions. |
| `server not reachable` or timeout | Destination, UDP reachability, the supplied timeout, and callback handling. This isn't a precise network diagnosis. |
| `join:bad` or `join:version` | Server rejected the compatibility data. Restore the supplied protocol and helper, and check your build version. |
| `join:full` | Server is at capacity. Ask the presenter or wait for a slot. |
| Connected but no tank, or prefab errors | Check the Console and the workshop scene and prefab versions. |
| Other tank visible but movement or shots wrong | That's expected. Continue with Max's and Niels's sections. |

## Full solution

### 1. Inside `JoinRoutine`

Keep the rest of the method. Replace the TODO block with:

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

## Discuss with your partner

1. Why call `StartClient()` instead of `StartHost()`?
2. Why can `StartClient()` return true before you're actually connected?
3. Who decides whether you're allowed to join, and where your tank spawns?
4. Why doesn't connecting automatically synchronize every field on your tank?

**Next up: Max. Now that we're sharing a session, let's make movement visible to everyone.**
