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
    public static bool GameActive { get; private set; } = false;
    private static Dictionary<InputDevice, TDSCharacterController> recognizedDevicesToPlayer { get; set; } = new Dictionary<InputDevice, TDSCharacterController>();
    private @PlayerControls playerControls { get; set; }

    [SerializeReference]
    private TDSCharacterController characterPF;
    [SerializeReference]
    private PlayerHUDManager hudManager;

    private void Awake()
    {
        GameActive = false;
        // Create a general playerControls object that can hear all players
        this.playerControls = new PlayerControls();
        this.playerControls.Enable();
    }

    private void OnEnable()
    {
        this.playerControls.Gameplay.Start.performed += OnStartPressed;
        this.playerControls.Gameplay.Reset.performed += OnResetPressed;
    }

    private void OnDisable()
    {
        this.playerControls.Gameplay.Start.performed -= OnStartPressed;
        this.playerControls.Gameplay.Reset.performed -= OnResetPressed;
    }

    void OnStartPressed(InputAction.CallbackContext context)
    {
        if (!MapGenerator.MapReady)
        {
            return;
        }

        // If this device is arleady tracked, ignore this input
        if (recognizedDevicesToPlayer.ContainsKey(context.control.device))
        {
            return;
        }

        Debug.Log($"Spawning new player because of an input from the '{context.control.device.displayName}'.");
        TDSCharacterController newController = Instantiate(characterPF, this.transform);
        recognizedDevicesToPlayer.Add(context.control.device, newController);

        List<InputDevice> devices = new List<InputDevice>();
        devices.Add(context.control.device);

        // If this device is the keyboard, also assign the mouse
        if (context.control.device == Keyboard.current.device)
        {
            devices.Add(Mouse.current.device);
        }

        newController.SetDevices(devices.ToArray());
        this.hudManager.TryRegisterCanvas(newController, out _);
    }

    void OnResetPressed(InputAction.CallbackContext context)
    {
        RestartGame();
    }

    public static IEnumerable<TDSCharacterController> GetCharacters()
    {
        return recognizedDevicesToPlayer.Values;
    }

    public static void RestartGame()
    {
        recognizedDevicesToPlayer.Clear();
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    public void Begin(StartPlayCircle circle)
    {
        GameActive = true;
    }
}
