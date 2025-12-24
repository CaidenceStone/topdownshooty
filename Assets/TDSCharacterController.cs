using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class TDSCharacterController : MonoBehaviour
{
    [SerializeReference]
    private Rigidbody2D body;
    [SerializeField]
    private float moveSpeedPerSecond = 5f;

    private @PlayerControls playerControls { get; set; }

    [SerializeField]
    private string deviceDisplayNameLabel;

    public void SetDevice(InputDevice device)
    {
        this.playerControls = new PlayerControls() { devices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(new InputDevice[] { device }) };
        this.playerControls.Enable();
        this.deviceDisplayNameLabel = device.displayName;
    }

    private void FixedUpdate()
    {
        if (playerControls.Gameplay.Move.IsPressed())
        {
            Vector2 movement = playerControls.Gameplay.Move.ReadValue<Vector2>();
            movement = Vector2.ClampMagnitude(movement, 1f);
            Vector2 distanceMoving = movement * moveSpeedPerSecond * Time.deltaTime;
            body.position += distanceMoving;
        }
    }
}
