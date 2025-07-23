using UnityEngine;
using Unity.Netcode;

public class SoundTrigger : NetworkBehaviour
{
    [Tooltip("Index into your sound library")]
    public int soundId = 0;

    [Tooltip("Distance at which volume is full")]
    [SerializeField]
    private float fullVolumeDistance = 2f;

    [Tooltip("Distance beyond which the sound is silent")]
    [SerializeField]
    private float maxHearingDistance = 15f;

    private void OnTriggerEnter(Collider other)
    {
        // only play if the thing that hit us is tagged "Tcan"
        if (!other.CompareTag("Tcan"))
            return;

        // invoke your ServerRpc to play it across the network
        AdjustableProximitySoundManager
            .Instance
            .PlaySoundServerRpc(
                soundId,
                transform.position,
                fullVolumeDistance,
                maxHearingDistance
            );
    }
}
