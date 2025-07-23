using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Cinemachine;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using Unity.UI.Shaders.Sample;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerMovement : NetworkBehaviour
{
    // --- Add these fields at the top ---
    [Header("Health Regeneration")]
    [Tooltip("Seconds after taking damage before regen starts")]
    [SerializeField] private float healthRegenDelay = 5f;
    [Tooltip("Health per second to regenerate")]
    [SerializeField] private float healthRegenRate = 5f;
    [Header("Respawn Settings")]
    [Tooltip("How far (max) to randomly offset each player from the respawn point")]
    [SerializeField] private float respawnRadius = 1f;
    [Tooltip("Collider defining the ‘train’ area — must stay inside this on respawn")]
    [SerializeField] private Collider trainAreaCollider;
    [Header("Slow-On-Hit Settings")]
    [Tooltip("How long the movement/jump slowdown lasts")]
    [SerializeField] private float slowEffectDuration = 3f;
    [Tooltip("Fraction of normal speed/jump while slowed (0–1)")]
    [SerializeField] private float slowFactor = 0.5f;
    private float timeSinceDamage;
    private Coroutine healthRegenRoutine;
    private bool isSlowed = false;
    // snapshot your original movement values
    private float baseMoveSpeed;
    private float baseSprintSpeed;
    private float baseJumpHeight;
    [Header("Fall-Respawn Settings")]
    [Tooltip("Y-position below which the player is teleported back to the respawn point")]
    [SerializeField] private float fallRespawnY = -50f;

    [Tooltip("Optional: if you want to drag-in a Transform instead of using a tag")]
    [SerializeField] private Transform respawnPointOverride;


    [Header("Debug")]
    [Tooltip("When true, sprint uses sprintSpeed and never drains stamina")]
    [SerializeField] private bool debugInfiniteSprint = false;
    [Header("UI Meters")]
    [Tooltip("Sci-Fi Meter (Image+Meter) for Health")]
    [SerializeField] private Meter healthMeter;
    [Tooltip("Sci-Fi Meter (Image+Meter) for Stamina")]
    [SerializeField] private Meter staminaMeter;

    [Header("Ragdoll")]
    [SerializeField] RagdollController ragdollController;
    private VCamPostProcess camPost;
    [Header("UI")]
    [SerializeField] GameObject hotbarCanvas;      // assign in inspector
    [SerializeField] public GameObject nameCanvas;        // assign in inspector
    [SerializeField] GameObject spectateCanvas;    // assign in inspector
    InventoryManager _inv;

    [Header("Respawn")]
    [Tooltip("Where to put the live model when reviving")]
    [SerializeField] private Transform respawnPoint;

    [Header("Player Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public NetworkVariable<int> Health = new (100);
    public NetworkVariable<bool> IsDead = new (false);

    [Header("Look Reporting")]
    [SerializeField] LayerMask obstacleLayer;
    float _reportTimer;

    [Header("Scanning")]
    [Tooltip("Profile to use when scanning (URP/HDRP volume)")]
    [SerializeField] private VolumeProfile scanNightVisionProfile;
    [Tooltip("Max stamina available for scanning")]
    [Header("Stamina")]
    [SerializeField] private float staminaRegenDelay = 0.5f;  // seconds to wait before regen
    private float timeSinceDrain = 0f;
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("Drain per second while scanning")]
    [SerializeField] private float scanStaminaDrainRate = 20f;
    [Tooltip("Drain per second while sprinting")]
    [SerializeField] private float sprintStaminaDrainRate = 10f;
    [Tooltip("Regen per second when idle")]
    [SerializeField] private float staminaRegenRate = 10f;
    [Tooltip("Seconds you must wait after last drain before regen begins")]

    [SerializeField] private float minStaminaToUse = 5f;
    private float currentStamina;

    private CinemachineVolumeSettings scanVolSettings;
    private bool isScanning;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    public int MaxHealth = 100;
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.4f;
    public LayerMask groundMask;
    [SerializeField] private CinemachineCamera vc;
    bool hasDied;


    public Camera playerCamera;
    public AudioListener playerAudioListener;

    public string PlayerId => OwnerClientId.ToString();
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
        writePerm: NetworkVariableWritePermission.Owner
    );

    private Vector2 moveInput;
    private IEnumerator HealthRegenLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            if (Health.Value > 0
              && Health.Value < MaxHealth
              && !isSlowed                            // << block during slow
              && Time.time - timeSinceDamage >= healthRegenDelay)
            {
                Health.Value = Mathf.Min(
                    MaxHealth,
                    Health.Value + Mathf.RoundToInt(healthRegenRate)
                );
            }
        }
    }
    /// <summary>
    /// Serveronly: immediately revive this player and tell their client to re-enable controls & reposition.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ForceReviveServerRpc(ServerRpcParams rpcParams = default)
    {
        // (Re-use your existing revive logic here)
        Health.Value = MaxHealth;
        IsDead.Value = false;
        ragdollController?.DisableRagdoll();
        // fire client RPC to re-enable controls & teleport
        DisableRagdollClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        });
    }
    public void OnRevivedClient()
        {
            Debug.Log("[PlayerMovement] OnRevivedClient() called — running local revive");
            HandleLocalRevive();
        }
    public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();
        _inv = GetComponent<InventoryManager>();
        var scan = GetComponent<ScanOutliner>();
        scan.playerCam = playerCamera;
        // SERVER: track health changes to clamp and enforce dead flag
        if (IsServer)
        {
            healthRegenRoutine = StartCoroutine(HealthRegenLoop());
            Health.OnValueChanged += (oldH, newH) =>
            {
                // clamp
                if (newH <= 0)
                    IsDead.Value = true;
                else
                    IsDead.Value = false;
            };
        }

        // OWNER: only the owner drives input, camera, local death/revive
        if (IsOwner)
        {
            if (healthMeter != null)
            {
                healthMeter.Value = Health.Value / (float)MaxHealth;
                healthMeter.gameObject.SetActive(true);
            }
            // hide stamina bar until first use (optional)
            if (staminaMeter != null)
            {
                currentStamina = maxStamina; // you already do this in Awake
                staminaMeter.Value = currentStamina / maxStamina;
                staminaMeter.gameObject.SetActive(true);
            }
            if (nameCanvas != null)
                nameCanvas.SetActive(false);
            camPost = vc.GetComponent<VCamPostProcess>();
            camPost.SetSpectateMode(false);
            camPost.SetScanMode(false);
            // subscribe to health changes locally
            Health.OnValueChanged += OnHealthChanged;
            Health.OnValueChanged += OnHealthChangedClient;
            // bring you alive in the UI + state manager
            Debug.Log($"[{PlayerId}] spawned alive");
            PlayerStateManager.Instance.SetPlayerDeadStatus(PlayerId, false);

            vc.Priority = 3;
            if (playerCamera != null) playerCamera.enabled = true;
            if (playerAudioListener != null) playerAudioListener.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (nameCanvas != null)
                nameCanvas.SetActive(true);
            // NON-OWNER: disable visuals + input
            if (playerCamera != null) playerCamera.enabled = false;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
            enabled = false;
            vc.Priority = 1;
        }
        if (trainAreaCollider == null)
        {
            var train = GameObject.FindWithTag("Train");
            if (train != null)
                trainAreaCollider = train.GetComponent<Collider>();
            else
                Debug.LogWarning("PlayerMovement: no GameObject tagged 'Train' found for trainAreaCollider!");
        }
    }
    private void OnHealthChangedClient(int oldH, int newH)
    {
        if (healthMeter != null)
        {
            healthMeter.Value = newH / (float)MaxHealth;
        }
        if (oldH > 0 && newH <= 0)
        {
            // **owner** just died locally  tell the ragdoll controller
            //ragdollController?.EnableRagdoll();

            // 2) tell server to drop everything
            if (_inv != null)
                _inv.DropAllItemsServerRpc();

            // 3) disable your UIs, show spectator UI
            hotbarCanvas?.SetActive(false);
            nameCanvas?.SetActive(false);
            spectateCanvas?.SetActive(true);
            if (healthMeter != null)
                healthMeter.Value = newH / (float)MaxHealth;
            // now switch into your spectator mode
            PlayerStateManager.Instance.SetPlayerDeadStatus(OwnerClientId.ToString(), true);
        }
    }
    private void OnHealthChanged(int oldH, int newH)
    {
        if (!IsOwner) return;

        bool wasDead = hasDied;
        bool nowDead = newH <= 0;

        if (!wasDead && nowDead)
            OnDie();
        else if (wasDead && !nowDead)
            HandleLocalRevive();
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        Health.Value = Mathf.Max(0, Health.Value - damage);
        timeSinceDamage = Time.time;      // reset regen timer

        //   Slow-effect trigger 
        if (!isSlowed)
            StartCoroutine(ApplySlowEffect());
    }

    private IEnumerator ApplySlowEffect()
    {
        isSlowed = true;

        // apply slow
        moveSpeed = baseMoveSpeed * slowFactor;
        sprintSpeed = baseSprintSpeed * slowFactor;
        jumpHeight = baseJumpHeight * slowFactor;

        // block regen until this finishes
        yield return new WaitForSeconds(slowEffectDuration);

        // restore
        moveSpeed = baseMoveSpeed;
        sprintSpeed = baseSprintSpeed;
        jumpHeight = baseJumpHeight;

        // stamp regen timer so you wait full delay afterward
        timeSinceDamage = Time.time;

        isSlowed = false;
    }
    private void TeleportBackToRespawn()
    {
        // 1) Figure out where to send them:
        Vector3 targetPos;
        if (respawnPointOverride != null)
        {
            targetPos = respawnPointOverride.position;
        }
        else
        {
            var go = GameObject.FindWithTag("RespawnPoint");
            if (go == null)
            {
                Debug.LogWarning("[PlayerMovement] No GameObject tagged 'RespawnPoint' found!");
                return;
            }
            targetPos = go.transform.position;
        }

        // 2) Snap the CharacterController (disable/enable to teleport cleanly)
        controller.enabled = false;
        transform.position = targetPos;
        controller.enabled = true;

        // 3) Push the new position into your NetworkVariable
        networkPosition.Value = targetPos;

        // (optionally) reset velocity so they don’t immediately fall again
        velocity = Vector3.zero;
    }
    private void Update()
{
    if (!IsOwner) return;
    if (IsDead.Value) return;
        if (transform.position.y < fallRespawnY)
        {
            TeleportBackToRespawn();
            // early-out so you don’t apply more gravity/movement this frame
            return;
        }
        // 1) Handle movement & gravity
        HandleMovement();
    HandleJumping();
    ApplyGravity();

    // 2) SCANNING drain
    bool rawScanInput = Input.GetMouseButton(1);
    bool scanning     = rawScanInput && currentStamina > 0f;
    camPost.SetScanMode(scanning);
    if (scanning)
    {
        currentStamina = Mathf.Max(0f,
            currentStamina - scanStaminaDrainRate * Time.deltaTime);
        timeSinceDrain = 0f;  // reset the regen cooldown
    }

    // 3) SPRINTING drain
    bool rawSprintInput = Keyboard.current.leftShiftKey.isPressed;
    bool sprintDraining = rawSprintInput 
                           && currentStamina > 0f 
                           && !debugInfiniteSprint;
    if (sprintDraining)
    {
        currentStamina = Mathf.Max(0f,
            currentStamina - sprintStaminaDrainRate * Time.deltaTime);
        timeSinceDrain = 0f;  // reset the regen cooldown
    }

    // 4) REGEN only when *both* keys are released and cooldown elapsed
    if (!rawScanInput && !rawSprintInput)
    {
        timeSinceDrain += Time.deltaTime;
        if (timeSinceDrain >= staminaRegenDelay && currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina,
                currentStamina + staminaRegenRate * Time.deltaTime);
        }
    }

    // 5) Update the UI meter
    if (staminaMeter != null)
        staminaMeter.Value = currentStamina / maxStamina;
        


        networkPosition.Value = transform.position;
        if (!hasDied && Health.Value <= 0)
            OnDie();
        if (PlayerStateManager.Instance != null && PlayerStateManager.Instance.IsDead)
                    return;
        _reportTimer += Time.deltaTime;
        if (_reportTimer >= 0.2f)
        {
            _reportTimer = 0;
            ReportLookStates();
        }
        

    }
    void ReportLookStates()
    {
        // get your camera once
        var cam = playerCamera;
        if (cam == null) return;
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);

        // find every enemy in scene
        foreach (var enemy in FindObjectsOfType<PeanutAI>())
        {
            // skip dead enemies if you like…
            var col = enemy.GetComponent<Collider>();
            if (col == null) continue;

            // 1) frustum test
            bool inFrustum = GeometryUtility.TestPlanesAABB(planes, col.bounds);
            // 2) obstacle test
            if (inFrustum)
            {
                var dir = (col.bounds.center - cam.transform.position).normalized;
                var dist = Vector3.Distance(cam.transform.position, col.bounds.center);
                if (Physics.Raycast(cam.transform.position, dir, dist, obstacleLayer))
                    inFrustum = false;
            }

            // 3) tell the server
            enemy.ReportLookingServerRpc(OwnerClientId, inFrustum);
        }
    }
    private void HandleNameCanvasToggle(int oldH, int newH)
    {
        if (nameCanvas == null) return;
        // if health > 0, show it; otherwise hide
        nameCanvas.SetActive(newH > 0);
    }
    private void Awake()
    {
        Health.OnValueChanged += OnHealthChanged;
        Health.OnValueChanged += HandleNameCanvasToggle;
        baseMoveSpeed = moveSpeed;
        baseSprintSpeed = sprintSpeed;
        baseJumpHeight = jumpHeight;
        currentStamina = maxStamina;
        timeSinceDrain = staminaRegenDelay;
        // get or add the VolumeSettings extension on your player vcam
        scanVolSettings = vc.GetComponent<CinemachineVolumeSettings>();
        if (scanVolSettings == null)
            scanVolSettings = vc.gameObject.AddComponent<CinemachineVolumeSettings>();

        // assign profile, start at zero weight
        scanVolSettings.Profile = scanNightVisionProfile;
        scanVolSettings.Weight = 0f;
    }
    private void HandleMovement()
    {
        // 1) Ground check
        isGrounded = CheckIfGrounded();

        // 2) Read raw WASD input
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        input = input.normalized;

        // 3) Build camera relative axes
        Vector3 camF = playerCamera.transform.forward;
        Vector3 camR = playerCamera.transform.right;
        camF.y = 0f; camR.y = 0f;
        camF.Normalize(); camR.Normalize();

        // 4) Compute healthbased speeds
        float healthFrac = Health.Value / (float)MaxHealth;
        float walkSpeed = baseMoveSpeed * healthFrac;
        float sprintSpeed = baseSprintSpeed * healthFrac;

        // 5) Apply t effect if active
        if (isSlowed)
        {
            walkSpeed *= slowFactor;
            sprintSpeed *= slowFactor;
        }

        // 6) Sprint gating
        bool wantsSprint = Keyboard.current.leftShiftKey.isPressed;
        bool canSprint = debugInfiniteSprint || currentStamina > 0f;
        bool sprinting = wantsSprint && canSprint;

        // choose final speed
        float finalSpeed = sprinting ? sprintSpeed : walkSpeed;

        // 7) Drain stamina when sprinting (unless infinite sprint debug)
        if (sprinting && !debugInfiniteSprint)
        {
            currentStamina = Mathf.Max(0f,
                currentStamina - sprintStaminaDrainRate * Time.deltaTime);
            timeSinceDrain = 0f;  // reset regen cooldown
        }

        // 8) Move the character
        Vector3 move = (camR * input.x + camF * input.y) * finalSpeed * Time.deltaTime;
        controller.Move(move);
    }


    private void HandleJumping()
    {
        // Only allow jump if grounded
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            float healthFrac = Health.Value / (float)MaxHealth;
            float jh = baseJumpHeight * healthFrac;
            velocity.y = Mathf.Sqrt(jh * -2f * gravity);
        }
    }


    private void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;  // Prevents gravity accumulation when grounded
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }

    private bool CheckIfGrounded()
    {
        if (groundCheck == null) return false;
        return Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);
    }

    private void LateUpdate()
    {
        if (IsOwner) return;
        transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 10f);
    }
 
    void OnDie()
    {
        hasDied = true;
        Debug.Log($"[{PlayerId}] has died  setting dead state");
        ragdollController?.EnableRagdoll();
        camPost.SetSpectateMode(true);
        // flip in the state manager
        PlayerStateManager.Instance.SetPlayerDeadStatus(PlayerId, true);
    }

    /*public void Revive()
    {
        if (!hasDied) return;
        hasDied = false;
        health = 100;
        Debug.Log($"[{PlayerId}] revived  clearing dead state");
        PlayerStateManager.Instance.SetPlayerDeadStatus(PlayerId, false);
    }
    */
    private void HandleLocalRevive()
    {
        hasDied = false;
        Debug.Log($"[{PlayerId}] revived, exiting dead state");
        PlayerStateManager.Instance.SetPlayerDeadStatus(PlayerId, false);

        // 3) re-enable UIs, hide spectator UI
        hotbarCanvas?.SetActive(true);
        nameCanvas?.SetActive(true);
        spectateCanvas?.SetActive(false);

        camPost.SetSpectateMode(false);
        // re-lock cursor, re-enable inputs, etc.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (healthMeter != null)
            healthMeter.Value = 1f;  // full health
        timeSinceDamage = Time.time;
        // 4) Completely clear your hotbar
        if (_inv != null && IsOwner)
            _inv.ClearInventoryServerRpc();

        // 5) Reset your meters, cursor, etc.
        timeSinceDamage = Time.time;
        currentStamina = maxStamina;
        if (healthMeter != null) healthMeter.Value = 1f;
        if (staminaMeter != null) staminaMeter.Value = 1f;
        // (optionally) reset stamina too:
        currentStamina = maxStamina;
        if (staminaMeter != null)
            staminaMeter.Value = 1f;
    }
    [ServerRpc(RequireOwnership = false)]
    public void ReviveRequestServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // 1) reset health
        Health.Value = MaxHealth;

        // 2) despawn ragdoll & re-show live model
        ragdollController?.DisableRagdoll();

        // 3) notify owner to exit spectator
        DisableRagdollClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
            }
        });
    }

    [ClientRpc]
    void DisableRagdollClientRpc(ClientRpcParams rpc = default)
    {
        if (!IsOwner) return;

        // 1) teleport to spawn + random scatter
        var spawnGO = GameObject.FindWithTag("RespawnPoint");
        if (spawnGO != null)
        {
            Vector3 basePos = spawnGO.transform.position;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * respawnRadius;
            Vector3 finalPos = basePos + offset;
            transform.position = finalPos;
            networkPosition.Value = finalPos;   // so others interpolate you there
        }

        // 2) re-enable camera & local controls
        PlayerStateManager.Instance.SetPlayerDeadStatus(OwnerClientId.ToString(), false);
        spectateCanvas?.SetActive(false);
        hotbarCanvas?.SetActive(true);
        nameCanvas?.SetActive(true);
        camPost.SetSpectateMode(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        HandleLocalRevive();
        // 3) schedule an integrity check in 2s
        StartCoroutine(RespawnIntegrityCheck());
    }
    private IEnumerator RespawnIntegrityCheck()
    {
        yield return new WaitForSeconds(2f);

        // A) Ensure server health is really maxed
        if (Health.Value < MaxHealth)
        {
            // ask the server to clamp you back up
            ReviveRequestServerRpc();
        }

        // B) Ensure you're inside the train
        if (trainAreaCollider != null)
        {
            Vector3 pos = transform.position;
            if (!trainAreaCollider.bounds.Contains(pos))
            {
                // teleport you back
                var spawnGO = GameObject.FindWithTag("RespawnPoint");
                if (spawnGO != null)
                {
                    Vector3 basePos = spawnGO.transform.position;
                    transform.position = basePos;
                    networkPosition.Value = basePos;
                }
                // and restore health if necessary
                if (Health.Value < MaxHealth)
                    ReviveRequestServerRpc();
            }
        }
    }
}

