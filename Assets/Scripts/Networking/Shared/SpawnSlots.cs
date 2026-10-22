using UnityEngine;

namespace TankDemo.Networking
{
    /// <summary>
    /// Spawn positions shared by the server's admission controller and the
    /// owning client. 48 slots use a golden-angle spiral so a full 40-bot
    /// stress test does not stack tanks on top of each other.
    /// </summary>
    public static class SpawnSlots
    {
        public const int Count = 48;
        public const float Radius = 17.5f;
        public const float Height = 2.2f;

        public static void Pose(int slotIndex, out Vector3 position, out Quaternion rotation)
        {
            int slot = ((slotIndex % Count) + Count) % Count;

            // Spread slots evenly over the disc instead of putting all 48 on
            // one ring (which would overlap at this count).
            float t = (slot + 0.5f) / Count;
            float radius = Radius * Mathf.Sqrt(t);
            float angle = slot * 2.39996323f; // golden angle in radians

            var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            position = direction * radius + Vector3.up * Height;
            rotation = Quaternion.LookRotation(-direction); // face the centre
        }
    }
}
