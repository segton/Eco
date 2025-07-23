using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerIKController : MonoBehaviour
{
    public Transform cameraTransform;   // your player camera
    [Range(0, 1f)] public float headWeight = 0.7f;  // how much the head turns

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || animator == null) return;

        // Aim point 10 units ahead of the camera
        Vector3 lookPos = cameraTransform.position + cameraTransform.forward * 10f;

        // (1) globalWeight: overall IK influence
        // (2) bodyWeight: how much the torso/chest follows (small value)
        // (3) headWeight: how much the head/neck follows (high value)
        // (4) eyesWeight: if you have eye bones you can nudge these
        // (5) clampWeight: how much to clamp extreme rotations
        animator.SetLookAtWeight(
            /*globalWeight:*/ 0.8f,
            /*bodyWeight:*/   0.1f,
            /*headWeight:*/   1.0f,
            /*eyesWeight:*/   0f,
            /*clampWeight:*/  0.5f
        );
        animator.SetLookAtPosition(lookPos);
    }
    [Header("Body Turn Lag")]
    public float bodyLagSpeed = 5f;  // higher = snappier, lower = lazier

    void Update()
    {
        // … your existing ground check + input + Animator feeds …

        // 1) Grab current root yaw & target yaw from camera
        float currentYaw = transform.eulerAngles.y;
        float targetYaw = cameraTransform.eulerAngles.y;

        // 2) Smoothly interpolate
        float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, bodyLagSpeed * Time.deltaTime);

        // 3) Apply only yaw (so pitch/roll unaffected)
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
    }
}
