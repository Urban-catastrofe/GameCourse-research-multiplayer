# Workshop Exercise: Latency and Rollback

Goal: students should see, in about 10 minutes, why latency feels bad and why
client-side prediction always needs a rollback/reconciliation step.

## Setup

1. Start the game with one host and at least one client.
2. Let two players shoot at each other while one player keeps driving.
3. Look at the overlay underneath the HP display:

```text
Workshop latency: 0.45s server delay | prediction OFF | rollback OFF
```

The artificial delay is configured in `Assets/Scripts/PaintGun.cs`. Because of
this, it visibly takes a moment before a shot gives feedback.

## Problem 1: Latency

What you see:

- You click or press `Space`.
- The tracer, impact, and damage appear later.
- The weapon feels slow, as if your input does not work immediately.

Assignment:

1. Open `Assets/Scripts/PaintGun.cs`.
2. Find `predictLocalShot`.
3. Change the code value from `false` to `true`.
4. Test again.

After this fix, the shooter should immediately see a yellow predicted hit splat.
The server still confirms the shot later with the normal pink/purple splat.

## Problem 2: Rollback

What you see after that:

- Shoot at a moving tank.
- Sometimes a yellow predicted splat appears immediately.
- When the server validates the delayed shot again, the real shot may end up
  somewhere else.
- The yellow prediction then temporarily stays visible in the wrong place.

This is the rollback problem: the client predicted something, but it does not
clean up the predicted feedback when the server sends back the real result.

Assignment:

1. Stay in `Assets/Scripts/PaintGun.cs`.
2. Find `rollbackPredictedShot`.
3. Change the code value from `false` to `true`.
4. Test again by shooting at a moving tank.

After this fix, the yellow predicted feedback is removed as soon as the server
response arrives. After that, only the server-confirmed feedback remains.

## Solution

The minimal solution is:

```csharp
static bool predictLocalShot = true;
static bool rollbackPredictedShot = true;
```

Why this works:

- `predictLocalShot` draws immediate local feedback on the client instead of
  waiting for the server.
- The server intentionally waits `simulatedServerDelay` seconds and then
  validates the shot again against the current world state.
- `rollbackPredictedShot` calls `RollbackPredictedShot(shotId)` when the server
  response arrives.
- Because of `shotId`, the client knows exactly which predicted objects belong
  to which shot.

