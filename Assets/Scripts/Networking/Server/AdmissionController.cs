using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TankDemo.Networking.Server
{
    /// <summary>
    /// The game server's final say on who may join: protocol version and capacity.
    /// It also assigns a spawn point before the player object is created.
    /// </summary>
    public class AdmissionController
    {
        public int MaxPlayers { get; }

        private readonly NetworkManager nm;
        private readonly SpawnService spawn;
        private readonly HashSet<ulong> pending = new HashSet<ulong>();

        public AdmissionController(NetworkManager nm, SpawnService spawn, int maxPlayers)
        {
            this.nm = nm;
            this.spawn = spawn;
            MaxPlayers = Mathf.Clamp(maxPlayers, 1, SpawnSlots.Count);

            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;
        }

        /// <summary>Connected players plus approved-but-not-yet-connected reservations.</summary>
        public int Players
        {
            get
            {
                int connected = 0;
                foreach (ulong id in nm.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId)
                        connected++;
                return connected + pending.Count;
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;

            if (!Protocol.TryParsePayload(request.Payload, out ushort version))
            {
                response.Reason = Protocol.Reject.BadRequest;
                return;
            }

            if (version != Protocol.Version)
            {
                response.Reason = Protocol.Reject.Version;
                return;
            }

            if (Players >= MaxPlayers)
            {
                response.Reason = Protocol.Reject.Full;
                return;
            }

            if (!spawn.TryAssign(request.ClientNetworkId, out Vector3 position, out Quaternion rotation))
            {
                response.Reason = Protocol.Reject.Full;
                return;
            }

            pending.Add(request.ClientNetworkId);
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = position;
            response.Rotation = rotation;

            Debug.Log("[server] admitted client " + request.ClientNetworkId + " (" + Players + "/" + MaxPlayers + ")");
        }

        private void HandleClientConnected(ulong clientId)
        {
            pending.Remove(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            pending.Remove(clientId);
        }
    }
}
