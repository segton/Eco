using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : NetworkBehaviour
{
    private Animator animator;
    private int holdLayer;

    public NetworkVariable<Vector3> headLookDir = new NetworkVariable<Vector3>(
        writePerm: NetworkVariableWritePermission.Owner,
        readPerm:  NetworkVariableReadPermission.Everyone
    );
    public NetworkVariable<bool> isCarrying = new NetworkVariable<bool>(
        writePerm: NetworkVariableWritePermission.Owner,
        readPerm: NetworkVariableReadPermission.Everyone
    );
    [Tooltip("World units in front of the head to look at")]
    public float lookDistance = 10f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Input Thresholds")]
    public float walkSpeedThreshold = 0.1f;

    [Header("Body Turn Settings")]
    public float headTurnLimit = 60f;
    public float bodyTurnSpeed = 90f;
    public bool wasGrounded = true;
    public bool wasMoving = false;
    [Header("Head Look IK")]
    public Transform cameraTransform;
    [Range(0f,1f)] public float globalWeight = 0.8f;
    [Range(0f,1f)] public float bodyWeight   = 0.1f;
    [Range(0f,1f)] public float headWeight   = 1.0f;
    [Range(0f,1f)] public float eyesWeight   = 0f;
    [Range(0f,1f)] public float clampWeight  = 0.5f;

    struct AnimState
    {
        public bool grounded, walk, run;
        public bool walkL, runL, walkR, runR, walkB, runB;
        public bool Equals(AnimState o)
        {
            return grounded==o.grounded
                && walk==o.walk && run==o.run
                && walkL==o.walkL && runL==o.runL
                && walkR==o.walkR && runR==o.runR
                && walkB==o.walkB && runB==o.runB;
        }
    }
    private AnimState _lastState;            //  ADDED

    void Awake()
    {
        animator = GetComponent<Animator>();
        
    }
    public override void OnNetworkSpawn()
    {
        animator = GetComponent<Animator>();
        holdLayer = animator.GetLayerIndex("HoldArm");

        // initialize the layer weight on everyone
        animator.SetLayerWeight(holdLayer, isCarrying.Value ? 1f : 0f);

        // listen for changes
        isCarrying.OnValueChanged += (oldVal, newVal) => {
            animator.SetLayerWeight(holdLayer, newVal ? 1f : 0f);
        };
    }
    void Update()
    {
        if (!IsOwner) return;
        var inv = InventoryManager.LocalInstance
                  ?? GetComponentInParent<InventoryManager>();
        bool carrying = inv != null && inv.currentHeldItem != null;

        // write it into the NetVar (this will automatically push to server & clients)
        if (isCarrying.Value != carrying)
            isCarrying.Value = carrying;
       
        headLookDir.Value = cameraTransform.forward;

        // 1) Ground
        bool isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // 2) Input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(h, 0, v);

        float speed = move.magnitude;
        bool isMoving = speed > walkSpeedThreshold;
        bool isRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        bool isWalking = isMoving && !isRunning;

        // 2) ground check


        // 3) jump start
        if (wasGrounded && !isGrounded)
            JumpStartServerRpc();

        // 4) landing
        if (!wasGrounded && isGrounded)
        {
            if (wasMoving)
                LandRollServerRpc();
            else
                LandServerRpc();
        }

        wasGrounded = isGrounded;
        wasMoving = isMoving;
        // 3) Flags
        AnimState current = new AnimState
        {
            grounded = isGrounded,
            walk = isWalking && v > walkSpeedThreshold,
            run = isRunning && v > walkSpeedThreshold,
            walkL = isWalking && h < -walkSpeedThreshold,
            runL = isRunning && h < -walkSpeedThreshold,
            walkR = isWalking && h > walkSpeedThreshold,
            runR = isRunning && h > walkSpeedThreshold,
            walkB = isWalking && v < -walkSpeedThreshold,
            runB = isRunning && v < -walkSpeedThreshold
        };

        if (!current.Equals(_lastState))
        {
            _lastState = current;
            UpdateAnimationServerRpc(
                current.grounded,
                current.walk, current.run,
                current.walkL, current.runL,
                current.walkR, current.runR,
                current.walkB, current.runB
            );
        }

        float bodyYaw = transform.eulerAngles.y;
        float camYaw = cameraTransform.eulerAngles.y;
        float yawDiff = Mathf.DeltaAngle(bodyYaw, camYaw);
        if (current.walk || current.run)
        {
            // instant snap to camera forward
            transform.rotation = Quaternion.Euler(0f, camYaw, 0f);
        }
        else
        {
            // otherwise use your head-turn-limit lag logic
            if (Mathf.Abs(yawDiff) > headTurnLimit)
            {
                float dir = Mathf.Sign(yawDiff);
                float step = bodyTurnSpeed * Time.deltaTime;
                float goal = camYaw - dir * headTurnLimit;
                float newYaw = Mathf.MoveTowardsAngle(bodyYaw, goal, step);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
        }
    }
    [ServerRpc(RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
    void JumpStartServerRpc()
    {
        animator.SetTrigger("JumpStart");
    }

    [ServerRpc(RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
    void LandServerRpc()
    {
        animator.SetTrigger("Land");
    }

    [ServerRpc(RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
    void LandRollServerRpc()
    {
        animator.SetTrigger("LandRoll");
    }
    [ServerRpc(RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]  // CHANGED
    void UpdateAnimationServerRpc(
        bool grounded,
        bool walk, bool run,
        bool walkL, bool runL,
        bool walkR, bool runR,
        bool walkB, bool runB
    )
    {
        animator.SetBool("Grounded",      grounded);
        animator.SetBool("Walk",          walk);
        animator.SetBool("Running",       run);
        animator.SetBool("WalkLeft",      walkL);
        animator.SetBool("RunningLeft",   runL);
        animator.SetBool("WalkRight",     walkR);
        animator.SetBool("RunningRight",  runR);
        animator.SetBool("WalkBack",      walkB);
        animator.SetBool("RunningBack",   runB);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex!=0 || animator==null) return;
        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone==null) return;

        Vector3 dir = IsOwner ? cameraTransform.forward : headLookDir.Value;
        Vector3 lookPos = headBone.position + dir * lookDistance;

        animator.SetLookAtWeight(globalWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(lookPos);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck==null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}
