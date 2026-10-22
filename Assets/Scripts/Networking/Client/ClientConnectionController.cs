using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TankDemo.Networking.Client
{
    public enum ConnectionState
    {
        Idle,
        Connecting,
        InGame,
        Failed
    }

    /// <summary>
    /// Owns the client connection lifecycle. Connect to one host/port, report
    /// what happened, and shut down cleanly. No room service, no retry maze:
    /// if it fails, the UI lets you press Join again.
    /// </summary>
    public class ClientConnectionController : MonoBehaviour
    {
        public event Action Changed;

        public ConnectionState State { get; private set; } = ConnectionState.Idle;
        public string Status { get; private set; } = "idle";
        public string Host { get; private set; } = "127.0.0.1";
        public ushort Port { get; private set; } = 7777;

        private NetworkManager nm;
        private UnityTransportAdapter transport;

        private Coroutine routine;
        private bool destroyed;
        private bool leaveRequested;
        private bool sawConnected;
        private bool sawDisconnected;
        private string lastDisconnectReason;

        private const float ConnectTimeoutSeconds = 10f;
        private const float ShutdownTimeoutSeconds = 5f;

        public void Initialize(NetworkManager networkManager, UnityTransportAdapter adapter)
        {
            nm = networkManager;
            transport = adapter;

            nm.OnClientConnectedCallback += HandleConnected;
            nm.OnClientDisconnectCallback += HandleDisconnected;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
#endif
        }

        public void SetEndpoint(string host, ushort port)
        {
            if (!string.IsNullOrEmpty(host))
                Host = host.Trim();
            if (port > 0)
                Port = port;
        }

        /// <summary>Start (or retry) connecting to the current host/port.</summary>
        public void Join()
        {
            if (destroyed || nm == null || transport == null)
            {
                SetState(ConnectionState.Failed, "No NetworkManager in this scene.");
                return;
            }

            if (State == ConnectionState.Connecting || State == ConnectionState.InGame)
                return;

            leaveRequested = false;
            StopRoutine();
            routine = StartCoroutine(JoinRoutine());
        }

        /// <summary>User-initiated exit; also valid while hosting.</summary>
        public void Leave()
        {
            leaveRequested = true;
            StopRoutine();
            if (nm != null && (nm.IsClient || nm.IsServer))
                nm.Shutdown();
            SetState(ConnectionState.Idle, "left");
        }

        private void OnApplicationQuit()
        {
            leaveRequested = true;
        }

        private IEnumerator JoinRoutine()
        {
            // Let a previous session finish shutting down before reusing the transport.
            float shutdownDeadline = Time.realtimeSinceStartup + ShutdownTimeoutSeconds;
            while (nm.ShutdownInProgress && Time.realtimeSinceStartup < shutdownDeadline)
                yield return null;

            if (destroyed || leaveRequested)
                yield break;

            sawConnected = false;
            sawDisconnected = false;
            lastDisconnectReason = null;

            SetState(ConnectionState.Connecting, "Connecting to " + Host + ":" + Port + "...");
            transport.ApplyClientEndpoint(Host, Port, nm);

            if (!nm.StartClient())
            {
                Fail("Could not start the client. Is another session still running?");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            while (!sawConnected && !sawDisconnected && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (destroyed)
                yield break;

            if (sawConnected)
            {
                routine = null;
                SetState(ConnectionState.InGame, "Connected to " + Host + ":" + Port);
                yield break;
            }

            string reason = sawDisconnected ? lastDisconnectReason : null;
            if (!sawDisconnected && (nm.IsClient || nm.IsServer))
                nm.Shutdown();

            Fail("Connection failed: " + (string.IsNullOrEmpty(reason) ? "server not reachable" : reason));
        }

        private void HandleConnected(ulong _)
        {
            sawConnected = true;
        }

        private void HandleDisconnected(ulong _)
        {
            if (destroyed || leaveRequested || !Application.isPlaying)
                return;

            sawDisconnected = true;
            lastDisconnectReason = nm != null ? nm.DisconnectReason : null;

            if (State == ConnectionState.InGame)
            {
                string reason = string.IsNullOrEmpty(lastDisconnectReason)
                    ? "server closed the connection"
                    : lastDisconnectReason;
                SetState(ConnectionState.Failed, "Disconnected: " + reason);
            }
        }

        private void Fail(string message)
        {
            routine = null;
            SetState(ConnectionState.Failed, message);
        }

        private void StopRoutine()
        {
            if (routine == null)
                return;
            StopCoroutine(routine);
            routine = null;
        }

        private void SetState(ConnectionState state, string status)
        {
            State = state;
            Status = status;
            Debug.Log("[connection] " + state + ": " + status);
            if (Changed != null)
                Changed();
        }

        private void OnDestroy()
        {
            destroyed = true;
            StopRoutine();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
#endif

            if (nm == null)
                return;
            nm.OnClientConnectedCallback -= HandleConnected;
            nm.OnClientDisconnectCallback -= HandleDisconnected;
        }

#if UNITY_EDITOR
        private void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode)
                return;

            destroyed = true;
            leaveRequested = true;
            StopRoutine();
        }
#endif
    }
}
