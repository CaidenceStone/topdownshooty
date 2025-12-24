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

    private @PlayerControls playerControls { get; set; }

    /// <summary>
    /// True: Use MousePosition for determining where to aim.
    /// Any Look use will set this from true to false.
    /// False: Use Look for determining where to aim.
    /// Any mouse movement detected will set this from false to true.
    /// </summary>
    private bool useMousePosition { get; set; } = false;
    private Vector2 aimingDirection { get; set; }

    [SerializeField]
    private float timeBetweenShots = .5f;
    private float curTimeBetweenShots { get; set; } = 0f;
    [SerializeReference]
    private Projectile projectilePF;

    [SerializeField]
    private string deviceDisplayNameLabel;

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

        if (this.curTimeBetweenShots > 0)
        {
            this.curTimeBetweenShots -= Time.deltaTime;
        }
    }

    protected override void BehaviourUpdate()
    {
        this.HandleFixedMovement();
        this.HandleFacingAndAiming();
        this.HandleFiring();
    }

    void HandleFixedMovement()
    {
        if (this.playerControls.Gameplay.Move.IsPressed())
        {
            Vector2 movement = this.playerControls.Gameplay.Move.ReadValue<Vector2>();
            movement = Vector2.ClampMagnitude(movement, 1f);
            Vector2 distanceMoving = movement * this.moveSpeedPerSecond * Time.deltaTime;
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
        }
        else
        {
            if (!this.playerControls.Gameplay.Look.IsPressed())
            {
                return;
            }

            Vector2 lookPosition = this.playerControls.Gameplay.Look.ReadValue<Vector2>();
            float angleToLook = Vector2.SignedAngle(lookPosition, Vector2.up);
            this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
            this.aimingDirection = lookPosition.normalized;
        }
    }

    void HandleFiring()
    {
        if (this.curTimeBetweenShots > 0)
        {
            return;
        }

        if (!this.playerControls.Gameplay.Fire.IsPressed())
        {
            return;
        }

        this.curTimeBetweenShots = this.timeBetweenShots;
        Projectile newProjectile = Instantiate(this.projectilePF);
        newProjectile.transform.position = this.firingPoint.position;
        newProjectile.StartProjectile(this.aimingDirection, Faction.Player);
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

    }
}
