using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TankDemo.Networking.Server
{
    /// <summary>
    /// Headless game server setup: listen on one UDP port and admit players.
    ///
    ///   GameServer.x86_64 -batchmode -nographics -port 7777 -maxplayers 20
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        public void Setup(NetworkManager nm, UnityTransport transport, string[] args)
        {
            ushort port = CommandLine.GetUShort(args, "-port", 7777);
            int maxPlayers = CommandLine.GetInt(args, "-maxplayers", 20);

            var adapter = new UnityTransportAdapter(transport);
            adapter.ConfigureServer(port);
            var admission = new AdmissionController(nm, new SpawnService(), maxPlayers);

            if (!nm.StartServer())
            {
                Debug.LogError("[server] StartServer() failed");
                return;
            }

            Debug.Log("[server] listening on UDP :" + port + " (max " + admission.MaxPlayers + ")");
        }

        /// <summary>Editor convenience: local host with the same admission rules.</summary>
        public static bool StartLocalHost(NetworkManager nm, ushort port)
        {
            if (nm == null)
                return false;

            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null || nm.IsServer || nm.IsClient)
                return false;

            new UnityTransportAdapter(transport).ConfigureServer(port);
            _ = new AdmissionController(nm, new SpawnService(), SpawnSlots.Count);
            return nm.StartHost();
        }

        /// <summary>Editor convenience: dedicated local server.</summary>
        public static bool StartLocalServer(NetworkManager nm, ushort port)
        {
            if (nm == null)
                return false;

            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null || nm.IsServer || nm.IsClient)
                return false;

            new UnityTransportAdapter(transport).ConfigureServer(port);
            _ = new AdmissionController(nm, new SpawnService(), SpawnSlots.Count);
            return nm.StartServer();
        }
    }
}
