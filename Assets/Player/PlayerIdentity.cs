using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerIdentity
{
    public InputDevice[] Devices { get; set; } = new InputDevice[0];
    public TDSCharacterController CurrentController { get; set; } = null;

    public PlayerIdentity(InputDevice[] devices)
    {
        this.Devices = devices;
    }
}
