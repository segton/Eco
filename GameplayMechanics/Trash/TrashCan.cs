using UnityEngine;
using Unity.Netcode;

public class TrashCan : NetworkBehaviour
{
    [Tooltip("The accepted item type for this trash can.")]
    public ItemDatabase.ItemType acceptedType;

    [Tooltip("Score multiplier. If the item matches, its value * multiplier is added; if not, deducted.")]
    public int scoreMultiplier = 1;

    [Tooltip("Prefab for a pop-up indicator (world canvas) showing the point change. It should have a PopUpText component and NO NetworkObject.")]
    public GameObject popUpPrefab;

    [Header("Info-Panel (last owner only)")]
    [Tooltip("Screen-space Canvas prefab with InfoPanelUI")]
    public GameObject infoPanelPrefab;

    private static GameObject s_CurrentInfoPanel;


    [Tooltip("Offset from the trash can's position where the pop-up will appear.")]
    public Vector3 popUpOffset = new Vector3(0, 2, 0);

    private void OnTriggerEnter(Collider other)
    {
        // Only run on server.
        if (!IsServer)
            return;

        // Look for an Item component.
        Item itemComponent = other.GetComponent<Item>();
        if (itemComponent == null)
            return;

        // Ignore if it's still held.
        if (itemComponent.isHeld)
            return;

        // Fetch database entry.
        var entry = ItemDatabase.Instance.GetItem(itemComponent.itemID);
        if (entry == null)
        {
            Debug.LogWarning($"[TrashCan] No database entry for '{itemComponent.itemID}'");
            return;
        }

        // Compute score.
        int pointChange = (entry.itemType == acceptedType)
            ? entry.itemValue * scoreMultiplier
            : -entry.itemValue * scoreMultiplier;

        // Update scores.
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddTeamScore(pointChange);
            ScoreManager.Instance.AddIndividualScore(itemComponent.lastOwnerId, pointChange);
        }

        // Show the popup on all clients.
        ShowPopupClientRpc(transform.position + popUpOffset, (pointChange >= 0 ? "+" : "") + pointChange);

        // If this item is one you capped, tell SpawnManager it's gone.
        if (SpawnManager.Instance != null &&
            SpawnManager.Instance.IsCappedItem(itemComponent.itemID))
        {
            SpawnManager.Instance.NotifyDestroyed(itemComponent.itemID);
        }

        if (infoPanelPrefab != null)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { itemComponent.lastOwnerId }
                }
            };
            ShowInfoPanelClientRpc(itemComponent.itemID, rpcParams);
        }


        // Finally, destroy the world object.
        Destroy(other.gameObject);
    }
    [ClientRpc]
    private void ShowInfoPanelClientRpc(
       string itemID,
       ClientRpcParams rpcParams = default
   )
    {
        var entry = ItemDatabase.Instance.GetItem(itemID);
        if (entry == null || infoPanelPrefab == null) return;

        // 1) destroy the old one, if it still exists
        if (s_CurrentInfoPanel != null)
        {
            Destroy(s_CurrentInfoPanel);
        }

        // 2) create the new one and stash it
        s_CurrentInfoPanel = Instantiate(infoPanelPrefab);
        if (s_CurrentInfoPanel.TryGetComponent<InfoPanelUI>(out var ui))
        {
            ui.Setup(entry);
        }
    }
    [ClientRpc]
    private void ShowPopupClientRpc(Vector3 popupPosition, string popupText)
    {
        if (popUpPrefab == null)
            return;

        GameObject popup = Instantiate(popUpPrefab, popupPosition, Quaternion.identity);
        var popUpScript = popup.GetComponent<PopUpText>();
        if (popUpScript != null)
            popUpScript.SetText(popupText);
        Destroy(popup, 2f);
    }
}
