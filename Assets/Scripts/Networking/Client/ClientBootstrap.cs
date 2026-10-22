using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TankDemo.Networking.Client
{
    /// <summary>
    /// Parses the host/port and wires up the connection controller and UI.
    ///
    ///   Game.exe                          -> join panel, defaults to 127.0.0.1:7777
    ///   Game.exe -ip 192.168.1.42 -port 7777  -> connects immediately
    /// </summary>
    public class ClientBootstrap : MonoBehaviour
    {
        public string defaultHost = "127.0.0.1";
        public ushort defaultPort = 7777;

        public void Setup(NetworkManager nm, UnityTransport transport, string[] args, bool autoJoin)
        {
            var adapter = new UnityTransportAdapter(transport);

            string host = CommandLine.Get(args, "-ip");
            if (string.IsNullOrEmpty(host))
                host = defaultHost;

            ushort port = CommandLine.GetUShort(args, "-port", defaultPort);

            var controller = gameObject.AddComponent<ClientConnectionController>();
            controller.Initialize(nm, adapter);
            controller.SetEndpoint(host, port);

            var view = gameObject.AddComponent<ConnectionView>();
            view.Initialize(controller, nm);

            Debug.Log("[client] direct UDP " + host + ":" + port);

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogError("[client] This demo uses direct UDP and does not support WebGL. Build a desktop client.");
#endif

            // Only auto-connect when the user gave us an address. Otherwise the
            // join panel is immediately usable (and can start a local host).
            if (autoJoin && (CommandLine.Get(args, "-ip") != null || CommandLine.Has(args, "-autoconnect")))
                controller.Join();
        }
    }
}
