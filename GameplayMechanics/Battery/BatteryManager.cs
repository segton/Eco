using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BatteryManager : NetworkBehaviour
{
    public static BatteryManager Instance { get; private set; }

    // Using a NetworkList ensures that any changes are sent to clients.
    public NetworkList<BatteryData> batteryDataList;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes.
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        batteryDataList = new NetworkList<BatteryData>();
        Debug.Log("BatteryManager Awake: batteryDataList created.");

    }

    public override void OnNetworkSpawn()
    {
        if (batteryDataList == null)
        {
            batteryDataList = new NetworkList<BatteryData>();
            Debug.Log("BatteryManager OnNetworkSpawn: batteryDataList re-created.");
        }
    }

    public float GetBatteryLevel(string uniqueID, float defaultLevel)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            foreach (var data in batteryDataList)
            {
                if (data.uniqueItemID.ToString() == uniqueID)
                    return data.batteryLevel;
            }
            return defaultLevel;
        }
        foreach (var data in batteryDataList)
        {
            if (data.uniqueItemID.ToString() == uniqueID)
                return data.batteryLevel;
        }
        BatteryData newData = new BatteryData { uniqueItemID = uniqueID, batteryLevel = defaultLevel };
        batteryDataList.Add(newData);
        return defaultLevel;
    }

    public void SaveBatteryLevel(string uniqueID, float level)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        bool found = false;
        for (int i = 0; i < batteryDataList.Count; i++)
        {
            if (batteryDataList[i].uniqueItemID.ToString() == uniqueID)
            {
                BatteryData data = batteryDataList[i];
                data.batteryLevel = level;
                batteryDataList[i] = data;
                found = true;
                break;
            }
        }
        if (!found)
        {
            BatteryData newData = new BatteryData { uniqueItemID = uniqueID, batteryLevel = level };
            batteryDataList.Add(newData);
        }
        Debug.Log($"[BatteryManager] Saved battery level for {uniqueID}: {level}");
    }

}
