using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Authentication.ExtendedProtection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ConfirmPlayerWatcher : MonoBehaviour
{
    private @PlayerControls playerControls { get; set; }

    [SerializeReference]
    private TDSCamera gameCamera;
    [SerializeReference]
    private MapGenerator mapGenerator;
    [SerializeReference]
    private SingleLevelDirector singleLevelDirector;

    private void Awake()
    {
        // Create a general playerControls object that can hear all players
        this.playerControls = new PlayerControls();
        this.playerControls.Enable();
    }

    private void OnEnable()
    {
        this.playerControls.Gameplay.Start.performed += OnStartPressed;
        this.playerControls.Gameplay.Reset.performed += OnResetPressed;
        this.playerControls.Gameplay.SkipLevel.performed += OnSkipPressed;
        this.playerControls.Enable();
    }

    private void OnDisable()
    {
        this.playerControls.Gameplay.Start.performed -= OnStartPressed;
        this.playerControls.Gameplay.Reset.performed -= OnResetPressed;
        this.playerControls.Gameplay.SkipLevel.performed -= OnSkipPressed;
        this.playerControls.Disable();
    }

    void OnStartPressed(InputAction.CallbackContext context)
    {
        if (!MapGenerator.MapReady)
        {
            return;
        }

        InputDevice[] devices = new InputDevice[] { context.control.device };

        // If this device is arleady tracked, ignore this input
        if (StaticLevelDirector.InputDeviceIsAlreadyRegistered(devices, out PlayerIdentity currentIdentity))
        {
            if (currentIdentity.CurrentController == null)
            {
                this.singleLevelDirector.SpawnPlayer(currentIdentity.Devices);
            }
            return;
        }

        Debug.Log($"Spawning new player because of an input from the '{context.control.device.displayName}'.");

        // If this devices is the keyboard, tie it to the mouse
        if (context.control.device == Keyboard.current)
        {
            devices = new InputDevice[] { devices[0], Mouse.current };
        }

        this.singleLevelDirector.SpawnPlayer(devices);
    }

    void OnResetPressed(InputAction.CallbackContext context)
    {
        StaticLevelDirector.RestartEntireGame();
    }

    void OnSkipPressed(InputAction.CallbackContext context)
    {
        if (!context.action.WasPressedThisFrame())
        {
            return;
        }

        StaticLevelDirector.AdvanceLevel();
    }
}
