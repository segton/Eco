/*using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SpawnItemButton : MonoBehaviour
{
    public ItemSpawner itemSpawner;
    public int itemID;

    public void SpawnItem()
    {
        if (itemSpawner != null && NetworkManager.Singleton.IsServer)
        {
            itemSpawner.SpawnItemServerRpc(new Vector3(Random.Range(-5, 5), 1, Random.Range(-5, 5)), itemID);
        }
    }
}
*/