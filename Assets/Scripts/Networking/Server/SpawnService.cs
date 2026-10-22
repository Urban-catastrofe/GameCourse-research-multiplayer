using UnityEngine;

namespace TankDemo.Networking.Server
{
    /// <summary>
    /// Turns a client id into a spawn pose. Capacity is capped below
    /// <see cref="SpawnSlots.Count"/>, so the modulo can never collide.
    /// The client runs the same math to place its owned tank before the first
    /// NetworkTransform update arrives.
    /// </summary>
    public class SpawnService
    {
        public bool TryAssign(ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            SpawnSlots.Pose((int)(clientId % (ulong)SpawnSlots.Count), out position, out rotation);
            return true;
        }
    }
}
