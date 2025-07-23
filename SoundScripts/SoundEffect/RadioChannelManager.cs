using UnityEngine;
using Unity.Netcode;

public class RadioChannelManager : NetworkBehaviour
{
    public static RadioChannelManager Instance { get; private set; }

    // 0..N are real ClientIds, MAX_VALUE means “no one”
    public const ulong NoSpeaker = ulong.MaxValue;


    public NetworkVariable<ulong> CurrentRadioSpeaker = new NetworkVariable<ulong>(
     NoSpeaker,  // now starts as “no one speaking”
     NetworkVariableReadPermission.Everyone,
     NetworkVariableWritePermission.Server
 );

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);    //  keep this alive across loads
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // force the “nobody” state when the server starts
            CurrentRadioSpeaker.Value = NoSpeaker;
            Debug.Log($"[RCM] Initialized -> NoSpeaker ({NoSpeaker})");
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void RequestSpeakServerRpc(ServerRpcParams rpcParams = default)
    {
        // ALWAYS set to the sender, overriding any previous speaker.
        ulong sender = rpcParams.Receive.SenderClientId;
        CurrentRadioSpeaker.Value = sender;
        Debug.Log($"[RCM] Speaker START  {sender}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void StopSpeakServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only clear if the same client who is currently speaking
        ulong sender = rpcParams.Receive.SenderClientId;
        if (CurrentRadioSpeaker.Value == sender)
        {
            CurrentRadioSpeaker.Value = NoSpeaker;
            Debug.Log($"[RCM] Speaker STOP   {sender}");
        }
    }
}