using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HotbarItemDatabase", menuName = "Inventory/HotbarItemDatabase")]
public class HotbarItemDatabase : ScriptableObject
{
    public List<HotbarItem> items;

    public HotbarItem GetItemByID(int id)
    {
        return items.Find(item => item.itemID == id);
    }
}
