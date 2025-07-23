using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using System.Threading.Tasks;

public class VivoxProximityManager : MonoBehaviour
{
    private IVivoxService _vivoxService;
    private bool _isGlobalChatActive = false;

    async void Start()
    {
        await InitializeUnityServices();

        _vivoxService = VivoxService.Instance;
        if (_vivoxService == null)
        {
            Debug.LogError("Vivox service instance is null. Ensure Vivox is installed and enabled via Unity Services.");
            return;
        }

        try
        {
            // Explicitly initialize Vivox before logging in
            await _vivoxService.InitializeAsync();
            Debug.Log("Vivox Initialized Successfully.");

            // Login to Vivox
            await _vivoxService.LoginAsync();
            Debug.Log("Logged into Vivox.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Vivox login failed: {ex.Message}");
        }
    }

    private async Task InitializeUnityServices()
    {
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in anonymously.");
        }
    }
}
