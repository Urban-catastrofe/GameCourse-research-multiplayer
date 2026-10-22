#nullable disable
using System;

namespace TankDemo.Networking
{
    /// <summary>
    /// Minimal connection payload: just the protocol version. It travels in NGO's
    /// NetworkConfig.ConnectionData so mismatched builds get a clear rejection
    /// instead of a confusing timeout.
    /// </summary>
    public static class Protocol
    {
        public const ushort Version = 1;

        public static byte[] BuildPayload()
        {
            var payload = new byte[2];
            payload[0] = (byte)Version;
            payload[1] = (byte)(Version >> 8);
            return payload;
        }

        public static bool TryParsePayload(byte[] payload, out ushort version)
        {
            version = 0;
            if (payload == null || payload.Length < 2)
                return false;

            version = (ushort)(payload[0] | (payload[1] << 8));
            return true;
        }

        /// <summary>
        /// Rejection reasons travel back through NGO's approval response
        /// (NetworkManager.DisconnectReason on the client).
        /// </summary>
        public static class Reject
        {
            public const string BadRequest = "join:bad";
            public const string Version = "join:version";
            public const string Full = "join:full";

            public static bool Is(string reason, string kind)
            {
                return !string.IsNullOrEmpty(reason) && reason.StartsWith(kind, StringComparison.Ordinal);
            }
        }
    }
}
