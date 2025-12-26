using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class TDSCharacterController : Entity
{
    [SerializeReference]
    private Transform rotationPoint;
    [SerializeReference]
    private Transform firingPoint;
    [SerializeField]
    private float moveSpeedPerSecond = 5f;
    [SerializeField]
    private float aimingCenterMaximumOffset = 2f;

    private @PlayerControls playerControls { get; set; }
    public PlayerIdentity ForIdentity { get; set; }

    /// <summary>
    /// True: Use MousePosition for determining where to aim.
    /// Any Look use will set this from true to false.
    /// False: Use Look for determining where to aim.
    /// Any mouse movement detected will set this from false to true.
    /// </summary>
    private bool useMousePosition { get; set; } = false;
    private Vector2 aimingDirection { get; set; }
    private Vector2 aimingCenter { get; set; }
    private Vector2? currentMoveInput { get; set; }

    [SerializeField]
    private string deviceDisplayNameLabel;
    [SerializeReference]
    public WeaponCollection OwnWeaponCollection;
    [SerializeField]
    private LayerMask pickupLayerMask;

    protected override void Start()
    {
        base.Start();
    }

    public Vector2 VisualAimingCenter
    {
        get
        {
            return this.Body.position + this.aimingDirection * Mathf.Clamp(Vector3.Distance(this.Body.position, this.aimingCenter), 0f, this.aimingCenterMaximumOffset);
        }
    }

    public void SetDevices(InputDevice[] devices)
    {
        this.playerControls = new PlayerControls() { devices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(devices) };
        this.playerControls.Gameplay.MousePosition.performed += this.MouseMovementDetected;
        this.playerControls.Gameplay.Look.performed += this.LookDetected;
        this.playerControls.Enable();

        StringBuilder deviceNames = new StringBuilder();
        string commaSeparator = "";
        foreach (InputDevice device in devices) 
        {
            deviceNames.Append(commaSeparator + device.displayName);
            commaSeparator = ", ";
        }

        this.deviceDisplayNameLabel = deviceNames.ToString();
    }

    protected override void TickDownTimers()
    {
        base.TickDownTimers();
        this.OwnWeaponCollection.TickDownTimers();
    }

    protected override void InputUpdates()
    {
        base.InputUpdates();

        this.HandleMoveInput();
        this.HandleFacingAndAiming();
        this.HandleCycleWeapons();
    }

    protected override void BehaviourUpdate()
    {
        base.BehaviourUpdate();

        this.HandleFixedMovement();
        this.HandleFiring();
    }

    void HandleMoveInput()
    {
        if (playerControls.Gameplay.Move.IsPressed())
        {
            Vector2 movement = this.playerControls.Gameplay.Move.ReadValue<Vector2>();
            this.currentMoveInput = Vector2.ClampMagnitude(movement, 1f);
        }
        else
        {
            this.currentMoveInput = null;
        }
    }

    void HandleFixedMovement()
    {
        if (currentMoveInput.HasValue)
        {
            Vector2 distanceMoving = Vector2.ClampMagnitude(this.currentMoveInput.Value, 1f) * this.moveSpeedPerSecond * Time.deltaTime;
            this.MoveEntity(distanceMoving);
        }
    }

    void HandleFacingAndAiming()
    {
        if (useMousePosition)
        {
            Vector2 mouseScreenPosition = this.playerControls.Gameplay.MousePosition.ReadValue<Vector2>();
            Vector2 cameraWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 positionDifference = cameraWorldPosition - this.Body.position;
            float angleToLook = Vector2.SignedAngle(positionDifference, Vector2.up);
            this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
            this.aimingDirection = positionDifference.normalized;
            this.aimingCenter = cameraWorldPosition;
        }
        else
        {
            if (this.playerControls.Gameplay.Look.IsPressed())
            {
                Vector2 lookDirection = this.playerControls.Gameplay.Look.ReadValue<Vector2>();
                float angleToLook = Vector2.SignedAngle(lookDirection, Vector2.up);
                this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
                this.aimingDirection = lookDirection.normalized;
                this.aimingCenter = this.Body.position + aimingDirection * this.aimingCenterMaximumOffset;
            }
        }
    }

    void HandleCycleWeapons()
    {
        if (this.playerControls.Gameplay.CycleWeaponRight.WasPressedThisFrame())
        {
            this.OwnWeaponCollection.CycleWeapons(1);
        }
        else if (this.playerControls.Gameplay.CycleWeaponLeft.WasPressedThisFrame())
        {
            this.OwnWeaponCollection.CycleWeapons(-1);
        }
    }

    void HandleFiring()
    {
        if (this.playerControls.Gameplay.Fire.IsPressed())
        {
            this.OwnWeaponCollection.GetCurrentWeapon().FireInDirection(this.aimingDirection);
        }
    }

    void MouseMovementDetected(InputAction.CallbackContext context)
    {
        this.useMousePosition = true;
    }

    void LookDetected(InputAction.CallbackContext context)
    {
        this.useMousePosition = false;
    }

    public override void MarkForDestruction()
    {
        Debug.Log($"Player defeated");
        this.ShouldDestroy = true;
        this.gameObject.SetActive(false);
    }

    protected override void HandleDestroy()
    {
        this.ForIdentity.CurrentController = null;
    }

    private void OnEnable()
    {
        TDSCamera.RegisterFollowing(this);
    }

    private void OnDisable()
    {
        TDSCamera.UnregisterFollowing(this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if ((this.pickupLayerMask & (1 << collision.gameObject.layer)) != 0)
        {
            IPickup pickup = collision.gameObject.GetComponentInParent<IPickup>();

            if (pickup != null)
            {
                this.HandlePickup(pickup);
            }

            return;
        }
    }

    private void HandlePickup(IPickup pickup)
    {
        pickup.ApplyToCharacter(this);
    }
}
