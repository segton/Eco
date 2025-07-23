using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class InfoPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image iconImage;
    [SerializeField] private float displayDuration = 5f;

    /// <summary>
    /// Call right after Instantiate().
    /// </summary>
    public void Setup(ItemDatabase.ItemEntry entry)
    {
        titleText.text = entry.itemName;
        bodyText.text = entry.description;
        if (iconImage != null)
            iconImage.sprite = entry.icon;

        StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        Destroy(gameObject);
    }
}
