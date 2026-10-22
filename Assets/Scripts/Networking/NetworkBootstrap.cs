using TankDemo.Networking.Client;
using TankDemo.Networking.Server;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TankDemo.Networking
{
    /// <summary>
    /// Composition root: decides server vs client and hands control to the
    /// dedicated bootstrap. Creates itself after scene load, so the scene needs
    /// no edits and builds stay drop-in.
    ///
    ///   server:  GameServer.x86_64 -batchmode -nographics -port 7777
    ///   client:  Game.exe -ip 127.0.0.1 -port 7777
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // HideAndDontSave objects survive Play Mode exit in the editor, so older
            // sessions can leave zombie bootstraps behind (each drawing its own UI and
            // holding a dead NetworkManager). FindObjectsOfTypeAll sees hidden objects;
            // clear any strays and start fresh.
            var strays = Resources.FindObjectsOfTypeAll<NetworkBootstrap>();
            for (int i = 0; i < strays.Length; i++)
            {
                if (strays[i] != null)
                    Destroy(strays[i].gameObject);
            }

            var go = new GameObject("~NetworkBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy; // hidden, but cleaned up on play exit
            go.AddComponent<NetworkBootstrap>();
        }

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            var transport = nm != null ? nm.GetComponent<UnityTransport>() : null;
            if (nm == null || transport == null)
            {
                Debug.LogWarning("[bootstrap] No NetworkManager in this scene. Open Assets/Scenes/SampleScene, then press Play.");
                return;
            }

            string[] args = CommandLine.Args();
            bool clientOverride = CommandLine.Has(args, "-client");
            bool serverMode = (Application.isBatchMode || CommandLine.Has(args, "-server")) && !clientOverride;

            if (serverMode)
            {
                var server = gameObject.AddComponent<ServerBootstrap>();
                server.Setup(nm, transport, args);
            }
            else
            {
                var client = gameObject.AddComponent<ClientBootstrap>();
                client.Setup(nm, transport, args, !Application.isEditor);
            }
        }
    }
}
