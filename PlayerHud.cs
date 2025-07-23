using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;


public class PlayerHud : NetworkBehaviour
{
    private NetworkVariable<NetworkString> playerNetworkName =
     //    initial value,     read-permission,               write-permission
     new NetworkVariable<NetworkString>(
         default,
         NetworkVariableReadPermission.Everyone,
         NetworkVariableWritePermission.Owner
     );

    private bool overlaySet = false;

    
    void UpdateNameLabel()
    {
        var label = GetComponentInChildren<TMP_Text>();
        label.text = playerNetworkName.Value.ToString();
    }
    public void SetOverlay()
    {
        var localPlayerOverlay = gameObject.GetComponentInChildren<TextMeshProUGUI>();
        localPlayerOverlay.text = $"{playerNetworkName.Value}";
    }

    public void Update()
    {
        if(!overlaySet && !string.IsNullOrEmpty(playerNetworkName.Value))
        {
            SetOverlay();
            overlaySet = true;
        }
    }
    public override void OnNetworkSpawn()
    {
        // 1) If this is YOUR player, tell ScoreManager your name:
        if (IsOwner)
        {
            var n = PlayerPrefs.GetString("LocalPlayerName", $"Player{OwnerClientId}");
            ScoreManager.Instance.SubmitNameServerRpc(new FixedString64Bytes(n));

        }

        // 2) Subscribe so we repaint our floating label whenever our own name entry changes:
        ScoreManager.Instance.IndividualScores.OnListChanged += OnScoresChanged;

        // 3) Onetime initial paint:
        OnScoresChanged(default);
    }
    public override void OnNetworkDespawn()
    {
        // Clean up our subscription so we don't get called after destruction
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.IndividualScores.OnListChanged -= OnScoresChanged;
        }
    }

    private void OnScoresChanged(NetworkListEvent<ScoreManager.PlayerScoreData> changeEvent)
    {
        var label = GetComponentInChildren<TMP_Text>();
        foreach (var e in ScoreManager.Instance.IndividualScores)
        {
            if (e.playerId == OwnerClientId)
            {
                label.text = e.playerName.ToString();
                break;
            }
        }
    }
}
