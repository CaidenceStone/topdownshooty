using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class StaticLevelDirector
{
    public static int LoadedLevel { get; private set; } = 1;
    public static SingleLevelDirector CurrentLevelDirector { get; private set; }
    public static bool GameActive { get; private set; } = false;
    private static Dictionary<InputDevice, PlayerIdentity> recognizedDevicesToPlayer { get; set; } = new Dictionary<InputDevice, PlayerIdentity>();

    public static void RestartEntireGame()
    {
        GameActive = false;
        LoadedLevel = 1;
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    public static void AdvanceLevel()
    {
        foreach (PlayerIdentity curIdentity in recognizedDevicesToPlayer.Values)
        {
            Debug.Log($"Player identity should save {curIdentity.WeaponData.WeaponPFs.Count} prefab weapons");
        }

        LoadedLevel++;
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    public static void RegisterSingleLevelDirector(SingleLevelDirector currentLevelDirector)
    {
        if (CurrentLevelDirector != null)
        {
            Debug.LogWarning($"When setting up the next level director, there was still an active previous one. It should be disposed/destroyed beforehand.");
        }

        CurrentLevelDirector = currentLevelDirector;
    }

    public static void UnregisterSingleLevelDirector(SingleLevelDirector outgoingLevelDirector)
    {
        if (CurrentLevelDirector != outgoingLevelDirector)
        {
            Debug.LogWarning($"When unregistering a level director, the previously registered level director was a different one.");
        }

        CurrentLevelDirector = null;
    }

    public static void RegisterInputPlayer(InputDevice[] devices, TDSCharacterController character)
    {
        if (InputDeviceIsAlreadyRegistered(devices, out _))
        {
            Debug.Log($"Attempting to register a player, but the device is already registered.");
            return;
        }

        PlayerIdentity newIdentity = new PlayerIdentity(devices);
        foreach (InputDevice curDevice in devices)
        {
            recognizedDevicesToPlayer.Add(curDevice, newIdentity);
        }
        newIdentity.CurrentController = character;
        newIdentity.WeaponData = newIdentity.CurrentController.OwnWeaponCollection.Data;
    }

    public static bool InputDeviceIsAlreadyRegistered(InputDevice[] devices, out PlayerIdentity currentIdentity)
    {
        foreach (InputDevice curDevice in devices)
        {
            bool newAnswer = recognizedDevicesToPlayer.TryGetValue(curDevice, out currentIdentity);
            if (newAnswer)
            {
                return true;
            }
        }

        currentIdentity = null;
        return false;
    }

    public static void BeginLevel()
    {
        GameActive = true;
    }

    public static IReadOnlyCollection<PlayerIdentity> GetPlayerIdentities()
    {
        return new HashSet<PlayerIdentity>(recognizedDevicesToPlayer.Values);
    }
}
