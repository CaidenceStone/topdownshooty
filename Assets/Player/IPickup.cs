using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPickup
{
    public void ApplyToCharacter(TDSCharacterController toApply);
}
