using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConfirmPlayerWatcher : MonoBehaviour
{
    private Dictionary<InputDevice, TDSCharacterController> recognizedDevicesToPlayer { get; set; } = new Dictionary<InputDevice, TDSCharacterController>();
    private @PlayerControls playerControls { get; set; }

    [SerializeReference]
    private TDSCharacterController characterPF;

    private void Awake()
    {
        // Create a general playerControls object that can hear all players
        this.playerControls = new PlayerControls();
        this.playerControls.Enable();
    }

    private void OnEnable()
    {
        this.playerControls.Gameplay.Start.performed += OnStartPressed;
    }

    private void OnDisable()
    {
        this.playerControls.Gameplay.Start.performed += OnStartPressed;
    }

    void OnStartPressed(InputAction.CallbackContext context)
    {
        // If this device is arleady tracked, ignore this input
        if (this.recognizedDevicesToPlayer.ContainsKey(context.control.device))
        {
            return;
        }

        Debug.Log($"Spawning new player because of an input from the '{context.control.device.displayName}'.");
        TDSCharacterController newController = Instantiate(characterPF, this.transform);
        this.recognizedDevicesToPlayer.Add(context.control.device, newController);
        newController.SetDevice(context.control.device);
    }
}
