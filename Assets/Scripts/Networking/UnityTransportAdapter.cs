using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace TankDemo.Networking
{
    /// <summary>Applies a plain UDP host/port to UnityTransport.</summary>
    public class UnityTransportAdapter
    {
        private readonly UnityTransport transport;

        public UnityTransportAdapter(UnityTransport transport)
        {
            this.transport = transport;
        }

        public void ApplyClientEndpoint(string host, ushort port, NetworkManager networkManager)
        {
            transport.UseWebSockets = false;
            transport.UseEncryption = false;

            var data = transport.ConnectionData;
            data.Address = host;
            data.Port = port;
            data.ServerListenAddress = "0.0.0.0";
            transport.ConnectionData = data;

            networkManager.NetworkConfig.ConnectionData = Protocol.BuildPayload();
        }

        public void ConfigureServer(ushort port)
        {
            transport.UseWebSockets = false;
            transport.UseEncryption = false;

            var data = transport.ConnectionData;
            data.Address = "0.0.0.0";
            data.Port = port;
            data.ServerListenAddress = "0.0.0.0";
            transport.ConnectionData = data;
        }
    }
}
