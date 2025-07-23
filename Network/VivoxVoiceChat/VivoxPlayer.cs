using UnityEngine;
using Unity.Services.Vivox;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Netcode;

public class VivoxPlayer : MonoBehaviour
{
    [SerializeField] private GameObject _localPlayerHead;
    private Vector3 lastPlayerHeadPos;

    private string gameChannelName = "Test3DChannel";
    Channel3DProperties _player3Dproperties;
    private int clientID;
    [SerializeField] private int newVolumeMinusPlus50 = 0;

    private float _nextPostUpdate;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
