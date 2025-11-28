using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

namespace EmotionBank
{
    public class RelayLauncher : MonoBehaviour
    {
        [Header("UI")]
        public Button hostButton;
        public Button joinButton;
        public TMP_InputField joinCodeInput;
        public TMP_Text statusText;
        public TMP_Text hostJoinCodeDisplay;

        bool servicesReady;

        UnityTransport Transport =>
            (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (hostButton != null)
                hostButton.onClick.AddListener(HostGame);

            if (joinButton != null)
                joinButton.onClick.AddListener(JoinGame);
        }

        async Task InitServices()
        {
            if (servicesReady) return;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                servicesReady = true;
                Log("Relay ready.");
            }
            catch (Exception e)
            {
                servicesReady = false;
                Log("Init failed: " + e.Message);
            }
        }

        public async void HostGame()
        {
            await InitServices();
            if (!servicesReady) return;

            try
            {
                Allocation alloc =
                    await RelayService.Instance.CreateAllocationAsync(4 - 1);

                string joinCode =
                    await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

                if (hostJoinCodeDisplay != null)
                    hostJoinCodeDisplay.text = "Join Code: " + joinCode;

                Log("Hosting. Code: " + joinCode);

                RelayServerData serverData =
                    AllocationUtils.ToRelayServerData(alloc, "dtls");

                Transport.SetRelayServerData(serverData);

                if (!NetworkManager.Singleton.StartHost())
                    Log("StartHost() failed.");
            }
            catch (Exception e)
            {
                Log("Host error: " + e.Message);
            }
        }

        public async void JoinGame()
        {
            Log("Join button clicked."); // <--- watch for this in Console

            await InitServices();
            if (!servicesReady) return;

            string code = joinCodeInput
                ? joinCodeInput.text.Trim().ToUpperInvariant()
                : "";

            if (string.IsNullOrEmpty(code))
            {
                Log("Enter join code.");
                return;
            }

            try
            {
                JoinAllocation joinAlloc =
                    await RelayService.Instance.JoinAllocationAsync(code);

                Log("JoinAllocation OK. Region: " + joinAlloc.Region);

                RelayServerData serverData =
                    AllocationUtils.ToRelayServerData(joinAlloc, "dtls");

                Transport.SetRelayServerData(serverData);

                if (!NetworkManager.Singleton.StartClient())
                    Log("StartClient() failed.");
            }
            catch (RelayServiceException e)
            {
                Log("Relay join failed: " + e.Reason + " / " + e.Message);
            }
            catch (Exception e)
            {
                Log("Join error: " + e.Message);
            }
        }

        void Log(string msg)
        {
            Debug.Log("[RelayLauncher] " + msg);
            if (statusText != null) statusText.text = msg;
        }
    }
}
