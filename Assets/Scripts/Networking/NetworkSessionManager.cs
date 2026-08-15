using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Willowstead.World;

namespace Willowstead.Networking
{
    /// <summary>
    /// Peer-to-Peer Host / Join Session Manager utilizing Unity Netcode for GameObjects and Unity Relay.
    /// Handles lobby creation, join codes, world seed synchronization, and connection state.
    /// </summary>
    public class NetworkSessionManager : MonoBehaviour
    {
        public static NetworkSessionManager Instance { get; private set; }

        public static string CurrentJoinCode { get; private set; } = string.Empty;
        public static bool IsMultiplayerActive => NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

        public static event Action<string> OnHostStarted;
        public static event Action OnClientConnected;
        public static event Action<string> OnConnectionFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[NetworkSessionManager]");
            DontDestroyOnLoad(go);
            go.AddComponent<NetworkSessionManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            EnsureNetworkManager();
        }

        private void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton == null)
            {
                var netGo = new GameObject("NetworkManager");
                DontDestroyOnLoad(netGo);
                var netMgr = netGo.AddComponent<NetworkManager>();
                var transport = netGo.AddComponent<UnityTransport>();
                netMgr.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    PlayerPrefab = null, // Dynamically handled or assigned
                    EnableSceneManagement = false
                };
            }
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkSessionManager] Relay service offline or auth error (falling back to direct host): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts hosting a co-op session. Allocates a Relay room and generates a Join Code.
        /// </summary>
        public async Task<string> StartHostSessionAsync(int maxPlayers = 4)
        {
            EnsureNetworkManager();

            bool authenticated = await EnsureAuthenticatedAsync();
            if (authenticated)
            {
                try
                {
                    Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                    string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    CurrentJoinCode = joinCode;

                    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                    transport.SetHostRelayData(
                        allocation.RelayServer.IpV4,
                        (ushort)allocation.RelayServer.Port,
                        allocation.AllocationIdBytes,
                        allocation.Key,
                        allocation.ConnectionData
                    );

                    NetworkManager.Singleton.StartHost();
                    OnHostStarted?.Invoke(joinCode);
                    Debug.Log($"<color=#FFD670>[NetworkSessionManager] Host session active! Join Code: {joinCode}</color>");
                    return joinCode;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkSessionManager] Failed to create Relay allocation: {ex.Message}");
                }
            }

            // Local fallback host (LAN / local IP)
            CurrentJoinCode = "LOCAL-HOST";
            var localTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            localTransport.SetConnectionData("127.0.0.1", 7777);
            NetworkManager.Singleton.StartHost();
            OnHostStarted?.Invoke(CurrentJoinCode);
            return CurrentJoinCode;
        }

        /// <summary>
        /// Joins an existing co-op session using a 6-character Relay Join Code.
        /// </summary>
        public async Task<bool> JoinSessionAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnConnectionFailed?.Invoke("Join Code cannot be empty.");
                return false;
            }

            joinCode = joinCode.Trim().ToUpperInvariant();
            EnsureNetworkManager();

            if (joinCode == "LOCAL-HOST" || joinCode == "127.0.0.1" || joinCode == "LOCALHOST")
            {
                var localTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                localTransport.SetConnectionData("127.0.0.1", 7777);
                bool success = NetworkManager.Singleton.StartClient();
                if (success) OnClientConnected?.Invoke();
                return success;
            }

            bool authenticated = await EnsureAuthenticatedAsync();
            if (!authenticated)
            {
                OnConnectionFailed?.Invoke("Could not connect to multiplayer services.");
                return false;
            }

            try
            {
                JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetClientRelayData(
                    joinAlloc.RelayServer.IpV4,
                    (ushort)joinAlloc.RelayServer.Port,
                    joinAlloc.AllocationIdBytes,
                    joinAlloc.Key,
                    joinAlloc.ConnectionData,
                    joinAlloc.HostConnectionData
                );

                bool clientStarted = NetworkManager.Singleton.StartClient();
                if (clientStarted)
                {
                    CurrentJoinCode = joinCode;
                    OnClientConnected?.Invoke();
                    Debug.Log($"<color=#80D27F>[NetworkSessionManager] Successfully joined session with code: {joinCode}</color>");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkSessionManager] Join session failed: {ex.Message}");
                OnConnectionFailed?.Invoke($"Invalid code or room closed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Leaves the current session and shuts down networking cleanly.
        /// </summary>
        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
            {
                NetworkManager.Singleton.Shutdown();
            }
            CurrentJoinCode = string.Empty;
        }
    }
}
