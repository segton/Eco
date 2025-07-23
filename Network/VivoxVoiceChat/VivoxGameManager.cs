using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using System.Collections.Generic;
using Unity.Netcode;

public class VivoxGameManager : MonoBehaviour
{
    public static VivoxGameManager Instance { get; private set; }
    public static string SharedProximityChannel = "ProximityVoice";
    public const string RadioChannelName = "WalkieTalkieChannel";

    private IVivoxService vivoxService;
    private bool isLoggedIn = false;
    private bool isLoggingIn = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        // this kicks off Vivox init + login
        await InitializeVivox();
    }

    // Called by UnityServicesInitializer (or our own Start) after sign-in.
    public async Task InitializeVivox()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        if (vivoxService == null)
            vivoxService = VivoxService.Instance;

        if (vivoxService.IsLoggedIn || isLoggingIn)
        {
            Debug.LogWarning("[Vivox] Already logged in or in process; skipping re-login.");
            return;
        }

        try
        {
            isLoggingIn = true;

            await vivoxService.InitializeAsync();
            Debug.Log("[Vivox] Vivox initialized successfully.");

            // If we're host, we already have LocalClientId==0, so log in now.
            if (NetworkManager.Singleton.IsHost)
            {
                await DoVivoxLogin(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                // For a client, wait until Netcode tells us our real ID.
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedForLogin;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Vivox] Initialization Failed: {e}");
            isLoggingIn = false;
        }
    }

    private async void OnClientConnectedForLogin(ulong clientId)
    {
        // only fire once for our own local client
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedForLogin;
        await DoVivoxLogin(clientId);
    }

    private async Task DoVivoxLogin(ulong clientId)
    {
        try
        {
            var displayName = clientId.ToString();
            var loginOptions = new LoginOptions
            {
                // when using Unity Auth, PlayerId override is ignored
                DisplayName = displayName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond,
                BlockedUserList = new List<string>()
            };

            await vivoxService.LoginAsync(loginOptions);
            isLoggedIn = true;
            Debug.Log($"[Vivox] Successfully logged into Vivox as {displayName}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Vivox] Login Failed: {e}");
        }
        finally
        {
            isLoggingIn = false;
        }
    }

    public async Task LogoutFromVivox()
    {
        if (!isLoggedIn) return;
        try
        {
            Debug.Log("[Vivox] Logging out...");
            await vivoxService.LogoutAsync();
            isLoggedIn = false;
            Debug.Log("[Vivox] Logged out of Vivox.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Vivox] Logout Failed: {e.Message}");
        }
    }

    private async void OnApplicationQuit()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[HostUI] Stopping host and kicking everyone.");
            NetworkManager.Singleton.Shutdown();
        }
        await LogoutFromVivox();
    }
}
