using Unity.Netcode;
using UnityEngine;

namespace TankDemo.Networking.Client
{
    /// <summary>
    /// Client connection UI: host/port + Join, and Leave while connected.
    /// Builds auto-connect to 127.0.0.1; pass -ip/-port for anything else.
    /// </summary>
    public class ConnectionView : MonoBehaviour
    {
        public bool showPanelInEditor = true;
        public bool showToastInBuilds = true;

        private ClientConnectionController controller;
        private NetworkManager nm;
        private string hostText = "127.0.0.1";
        private string portText = "7777";
        private float connectedAt = -1f;

        public void Initialize(ClientConnectionController controller, NetworkManager nm)
        {
            this.controller = controller;
            this.nm = nm;
            if (controller != null)
            {
                hostText = controller.Host;
                portText = controller.Port.ToString();
            }
        }

        private void OnGUI()
        {
            if (controller == null)
                return;

            bool connected = nm != null && (nm.IsClient || nm.IsServer);
            if (controller.State == ConnectionState.InGame)
            {
                if (connectedAt < 0f)
                    connectedAt = Time.timeSinceLevelLoad;
            }
            else
            {
                connectedAt = -1f;
            }

#if UNITY_EDITOR
            if (!showPanelInEditor)
                return;
#else
            if (!showToastInBuilds)
                return;

            // Let the toast disappear once the match is running.
            if (connected && controller.State == ConnectionState.InGame &&
                Time.timeSinceLevelLoad - connectedAt > 6f)
                return;
#endif

            if (connected)
            {
                GUILayout.BeginArea(new Rect(12, Screen.height - 96, 460, 86), GUI.skin.box);
                GUILayout.Label("network: " + controller.State + " - " + controller.Status);
#if UNITY_EDITOR
                if (GUILayout.Button("Leave"))
                {
                    controller.Leave();
                }
#endif
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(12, Screen.height - 236, 460, 224), GUI.skin.box);
            GUILayout.Label("<b>Direct connect</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("client: " + controller.State + " - " + controller.Status);

            GUILayout.BeginHorizontal();
            GUILayout.Label("host", GUILayout.Width(40));
            hostText = GUILayout.TextField(hostText, GUILayout.Width(180));
            GUILayout.Label("port", GUILayout.Width(32));
            portText = GUILayout.TextField(portText, GUILayout.Width(64));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Join"))
            {
                controller.SetEndpoint(hostText, ParsePort());
                controller.Join();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Join connects to the host/port above. No room service.");
            GUILayout.EndArea();
        }

        private ushort ParsePort()
        {
            return ushort.TryParse(portText, out ushort port) && port > 0 ? port : (ushort)7777;
        }
    }
}
