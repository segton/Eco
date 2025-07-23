using UnityEngine;

public class SoundLibrary : MonoBehaviour
{
    public static SoundLibrary Instance;

    [System.Serializable]
    public class SoundEntry
    {
        public string name;
        public AudioClip clip;
    }

    public SoundEntry[] soundEntries;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Optionally, DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public AudioClip GetClip(int id)
    {
        if (id >= 0 && id < soundEntries.Length)
            return soundEntries[id].clip;
        return null;
    }
}
