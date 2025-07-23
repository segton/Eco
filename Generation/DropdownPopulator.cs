using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DropdownPopulator : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown difficultyDropdown;

    void Start()
    {
        // 1) Clear any existing entries
        difficultyDropdown.ClearOptions();

        // 2) Build your new list of labels
        List<string> labels = new List<string>()
        {
            "Very Easy",
            "Easy",
            "Medium",
            "Hard",
            "Insane",
            "Nightmare"
        };

        // 3) Add them in one shot
        difficultyDropdown.AddOptions(labels);

        // 4) (Optional) Force the UI to update its shown value
        difficultyDropdown.RefreshShownValue();
    }
}
