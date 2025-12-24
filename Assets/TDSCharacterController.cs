using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class TDSCharacterController : MonoBehaviour
{
    [SerializeReference]
    private Rigidbody2D body;
    [SerializeReference]
    private Transform rotationPoint;
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

    private void FixedUpdate()
    {
        this.HandleFixedMovement();
        this.HandleFacingAndAiming();
    }

    void HandleFixedMovement()
    {
        if (this.playerControls.Gameplay.Move.IsPressed())
        {
            Vector2 movement = this.playerControls.Gameplay.Move.ReadValue<Vector2>();
            movement = Vector2.ClampMagnitude(movement, 1f);
            Vector2 distanceMoving = movement * this.moveSpeedPerSecond * Time.deltaTime;
            this.body.position += distanceMoving;
        }
    }

    void HandleFacingAndAiming()
    {
        if (useMousePosition)
        {
            Vector2 mouseScreenPosition = this.playerControls.Gameplay.MousePosition.ReadValue<Vector2>();
            Vector2 cameraWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            float angleToLook = Vector2.SignedAngle(cameraWorldPosition - this.body.position, Vector2.up);
            this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
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
}
