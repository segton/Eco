using UnityEngine;
using Unity.Netcode;

public class Item : MonoBehaviour
{
    [Tooltip("The unique identifier for this item. Used to look up the networked prefab.")]
    public string itemID;

    // Flag to indicate if this item is currently held in a player's hand.
    public bool isHeld = false;

    // Set this flag to true if this scene item is a placeholder to be converted.
    public bool convertToNetworked = true;

    [Tooltip("The last player who held this item (Owner ClientId).")]
    public ulong lastOwnerId;

    private void Start()
    {
        // Ensure the object has a collider for interaction.
        if (!TryGetComponent<Collider>(out var col))
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        col.isTrigger = false; // For normal collision/interaction.
    }
}
