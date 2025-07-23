using Unity.Netcode;
using UnityEngine;

public class PlayerFlashlightController : NetworkBehaviour
{
    public NetworkVariable<bool> FlashlightOn = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<Vector3> FlashlightDirection = new(
        Vector3.forward,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    [Header("Beam Settings")]
    [SerializeField] private float beamRange = 10f;
    [SerializeField] private float beamAngle = 45f;
    [SerializeField] private float beamIntensity = 1f;
    [SerializeField] private Color beamColor = Color.white;

    [Header("Source Glow")]
    [Tooltip("Exact name of the child GameObject holding your Point Light")]
    [SerializeField] private string glowLightName = "FlashlightGlow";

    [Header("Battery")]
    [SerializeField] private float emptyThreshold = 2f;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.V;

    private InventoryManager _inv;
    private BatteryItem _battery;
    private GameObject _heldFlashlight;
    private Light _itemBeam;
    private Light _itemGlow;

    private const string FLASHLIGHT_ID = "5";

    void Awake()
    {
        _inv = GetComponent<InventoryManager>() ?? GetComponentInParent<InventoryManager>();
        if (_inv == null)
            Debug.LogError("[PlayerFlashlight] No InventoryManager found!");
    }

    public override void OnNetworkSpawn()
    {
        FlashlightOn.OnValueChanged += OnToggleChanged;
        FlashlightDirection.OnValueChanged += OnDirectionChanged;

        OnToggleChanged(false, FlashlightOn.Value);
        OnDirectionChanged(Vector3.zero, FlashlightDirection.Value);
    }

    public override void OnNetworkDespawn()
    {
        FlashlightOn.OnValueChanged -= OnToggleChanged;
        FlashlightDirection.OnValueChanged -= OnDirectionChanged;
    }

    private void OnToggleChanged(bool oldVal, bool newVal)
    {
        if (_itemBeam != null) _itemBeam.enabled = newVal;
        if (_itemGlow != null) _itemGlow.enabled = newVal;
    }

    private void OnDirectionChanged(Vector3 oldDir, Vector3 newDir)
    {
        if (_itemBeam != null)
            _itemBeam.transform.rotation = Quaternion.LookRotation(newDir);
    }

    void Update()
    {
        if (_inv == null) return;

        bool isHolding = false;
        if (_inv.inventory.Count > 0)
        {
            int slot = _inv.selectedSlot.Value;
            if (slot >= 0 && slot < _inv.inventory.Count)
                isHolding = _inv.inventory[slot].itemID.ToString() == FLASHLIGHT_ID;
        }

        var heldGO = isHolding ? _inv.currentHeldItem : null;
        if (heldGO != _heldFlashlight)
        {
            _heldFlashlight = heldGO;
            _battery = heldGO?.GetComponent<BatteryItem>();

            _itemBeam = heldGO != null
                ? heldGO.GetComponentInChildren<Light>(true)
                : null;

            _itemGlow = null;
            if (heldGO != null)
            {
                var glowTF = heldGO.transform.Find(glowLightName);
                if (glowTF != null)
                    _itemGlow = glowTF.GetComponent<Light>();
            }

            if (_itemBeam != null)
            {
                _itemBeam.range = beamRange;
                _itemBeam.spotAngle = beamAngle;
                _itemBeam.intensity = beamIntensity;
                _itemBeam.color = beamColor;
                _itemBeam.enabled = FlashlightOn.Value;
            }
            if (_itemGlow != null)
                _itemGlow.enabled = FlashlightOn.Value;
        }

        if (!isHolding)
        {
            if (IsOwner && FlashlightOn.Value)
                ToggleFlashlightServerRpc(false);
            return;
        }

        if (IsOwner)
        {
            // 4a) Autoturn off when battery runs out
            if (FlashlightOn.Value && _battery != null && _battery.currentBatteryLevel <= emptyThreshold)
            {
                ToggleFlashlightServerRpc(false);
            }

            // 4b) Handle toggle input
            if (Input.GetKeyDown(toggleKey) &&
                _battery != null &&
                _battery.currentBatteryLevel > emptyThreshold)
            {
                ToggleFlashlightServerRpc(!FlashlightOn.Value);
            }

            // 4c) Stream the camera’s forward vector
            if (_inv.cam != null)
                FlashlightDirection.Value = _inv.cam.transform.forward;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ToggleFlashlightServerRpc(bool on, ServerRpcParams _ = default)
    {
        // update the networked state
        FlashlightOn.Value = on;

        // then tell only the owning client to update its UI
        UpdateBatteryUseClientRpc(on, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
    }

    [ClientRpc]
    private void UpdateBatteryUseClientRpc(bool on, ClientRpcParams rpcParams = default)
    {
        // runs only on the client that owns this player
        _battery?.inUse(on);
    }
}
