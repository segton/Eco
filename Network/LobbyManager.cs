using UnityEngine;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using System.Threading.Tasks;
using TMPro;
using System.Xml.Linq;

public class LobbyManager : MonoBehaviour
{
    public TMP_InputField displayNameInput;
    public static LobbyManager Instance { get; private set; }
    public string CurrentSessionId { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        // when the user finishes editing their name, save it immediately
        displayNameInput.onEndEdit.AddListener(SaveDisplayName);
    }
    private void SaveDisplayName(string s)
    {
        s = s.Trim();
        if (string.IsNullOrEmpty(s)) s = $"Player{Random.Range(1, 9999)}";
        PlayerPrefs.SetString("LocalPlayerName", s);
        PlayerPrefs.Save();
        if (ScoreManager.Instance != null)
        { 
            ScoreManager.Instance.SubmitNameServerRpc(s);
            Debug.Log($"[LobbyManager] Sent display name RPC: {s}");
        }

    }

    // Called when the player successfully joins a session
    public async void OnJoinedSession(ISession session)
    {
        if (session == null)
        {
            Debug.LogWarning("[LobbyManager] OnJoinedSession called with a null session.");
            return;
        }

        CurrentSessionId = session.Id;
        Debug.Log($"[LobbyManager] Joined session: {session.Id} - {session.Name}");

        // 1) Initialize Vivox if not already done
        if (VivoxGameManager.Instance != null)
        {
            await VivoxGameManager.Instance.InitializeVivox();
        }
        else
        {
            Debug.LogError("[LobbyManager] VivoxGameManager instance not found!");
        }
        
        // 2) Pass the session ID to your VoiceChannelManager
        VoiceChannelManager.InitializeChannels(CurrentSessionId);
            // 3) Immediately send our chosen displayName into ScoreManager
    if (ScoreManager.Instance != null)
                {
            string chosen = displayNameInput.text.Trim();
                    if (!string.IsNullOrEmpty(chosen))
                        {
                ScoreManager.Instance.SubmitNameServerRpc(chosen);
                Debug.Log($"[LobbyManager] Sent name RPC: {chosen}");
                        }
                }
            else Debug.LogError("[LobbyManager] ScoreManager instance not found!");

        // 3) (Optional) If you use Netcode for game objects, load the game scene
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Start",
                UnityEngine.SceneManagement.LoadSceneMode.Single);
            Debug.Log("[LobbyManager] Host loading the game scene.");
        }

                
    }
}
