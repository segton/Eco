using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Multiplayer.Widgets;
using System.Threading.Tasks;
using System;

public class UnityServicesInitializer : MonoBehaviour
{
    async void Awake()
    {
        // Ensure this script runs early in the execution order
        // (Project Settings > Script Execution Order).
        await InitializeServices();
        DontDestroyOnLoad(gameObject);
    }

    private async Task InitializeServices()
    {
        // 1. If needed, read a command-line argument to get a unique ID for this instance.
        string editorID = "default";
        string[] args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.StartsWith("-EditorID="))
            {
                editorID = arg.Substring("-EditorID=".Length);
                break;
            }
        }
        // If none found, fallback to a random number so each Editor instance is unique.
        if (editorID == "default")
        {
            editorID = UnityEngine.Random.Range(1000, 9999).ToString();
        }

        // 2. Initialize Unity Services first, to avoid 'Singleton not initialized' errors.
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[UnityServicesInitializer] Unity Services Initialized.");
        }
        else
        {
            Debug.Log("[UnityServicesInitializer] Unity Services already initialized.");
        }
         
        // 3. Now that services are initialized, we can safely switch profile.
        string uniqueProfile = "Profile_" + editorID;
        Debug.Log($"[UnityServicesInitializer] Switching profile to: {uniqueProfile}");
        AuthenticationService.Instance.SwitchProfile(uniqueProfile);

        // 4. Sign in if not already signed in.
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[UnityServicesInitializer] Signed in as: {AuthenticationService.Instance.PlayerId}");
        }

       /* // 5. Initialize your custom Vivox login.
        if (VivoxGameManager.Instance != null)
        {
            Debug.Log("[UnityServicesInitializer] Initializing Vivox via custom manager...");
            await VivoxGameManager.Instance.InitializeVivox();
            Debug.Log("[UnityServicesInitializer] Custom Vivox initialization complete.");
        }
        else
        {
            Debug.LogError("[UnityServicesInitializer] VivoxGameManager instance not found in the scene.");
        }

        // 6. If you have a VoiceChannelManager or other scripts that rely on Vivox,
        //    call their static initialization method here (if needed).
        VoiceChannelManager.InitializeChannels();*/

        // 7. Finally, notify Multiplayer Widgets that services are ready.
        WidgetServiceInitialization.ServicesInitialized();
        Debug.Log("[UnityServicesInitializer] Multiplayer Widgets have been told services are initialized.");
    }
}
