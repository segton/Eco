using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    [System.Serializable]
    public enum ItemType
    {
        None,
        Glass,
        Metal,
        Paper,
        Plastic,
        Organic
    }

    [System.Serializable]
    public class ItemEntry
    {
        public string itemID;
        public string itemName;
        public int itemValue; // New: Value of the item (score, currency, etc.)
        public ItemType itemType; // New: Type of the item
        public Sprite icon;
        [TextArea]
        [Tooltip("The text to show in the info panel.")]
        public string description;
        public GameObject prefab;
        [Tooltip("Can this item ever be spawned by LootSpawner?")]
        public bool dungeonSpawnable = true;  
    }

    public ItemEntry[] items;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep the database alive across levels
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Find item by ID
    public ItemEntry GetItem(string itemID)
    {
        foreach (var entry in items)
        {
            if (entry.itemID == itemID) return entry;
        }
        return null;
    }

    // New: Find item by Type
    public ItemEntry[] GetItemsByType(ItemType type)
    {
        return System.Array.FindAll(items, entry => entry.itemType == type);
    }
}
