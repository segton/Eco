using DilmerGames.Core.Singletons;
using Unity.Netcode;

public class PlayersManager : NetworkSingleton<PlayersManager>
{
    NetworkVariable<int> playersInGame = new NetworkVariable<int>();

    public int PlayersInGame
    {
        get
        {
            return playersInGame.Value;
        }
    }
   
    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            if (IsServer)
            {
                Logger.Instance.LogInfo($"{id} just connected");
                playersInGame.Value++;
            }    
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            if (IsServer)
            {
                Logger.Instance.LogInfo($"{id} has disconnected");
                playersInGame.Value--;
            }             
        };
    }
}
