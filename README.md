# TankDemo

A small Unity multiplayer tank arena built with **Netcode for GameObjects**.
One player can host locally, or you can run a headless dedicated server. Clients
connect directly over UDP — no account system, lobby service or matchmaking.

## Requirements

- Unity **6000.5.10f1** (Unity 6.5)
- Windows and/or Linux standalone build support

## Run it

1. Open this folder in Unity Hub.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play.

The little panel in the bottom-left has:

- **Join** — connect to the host/port in the fields.
- **Host local** — start a server and play in the same process.
- **Server local** — start a dedicated server; connect another client to it.

## Controls

| Input | Action |
|---|---|
| `W` / `S` or arrow keys | Drive forward / reverse |
| `A` / `D` or arrow keys | Turn |
| `Z` / `X` | Rotate the turret |
| Left mouse / `Space` | Fire |

## Standalone builds

From the Unity menu:

- **Demo → Build Linux Server**
- **Demo → Build Windows Client**

Run a server:

```bash
./GameServer.x86_64 -batchmode -nographics -port 7777 -maxplayers 20
```

Run a client:

```bash
TankDemo.exe -ip 127.0.0.1 -port 7777
```

Without `-ip`/`-port` the client opens the join panel. For remote play, the
server's UDP port must be reachable; the client and server must use the same
protocol version.

## Current scope

This is a course/demo project, not a production backend. The server approves
connections, enforces capacity and assigns spawn points. Tank movement is still
client-authoritative (`ClientNetworkTransform`); server-authoritative movement
and prediction are the next netcode step.
