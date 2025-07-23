using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class TestInternet : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(CheckConnection());
    }

    IEnumerator CheckConnection()
    {
        UnityWebRequest request = UnityWebRequest.Get("https://www.google.com");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            Debug.Log("Internet connection is working.");
        else
            Debug.LogError("No internet connection.");
    }
}
