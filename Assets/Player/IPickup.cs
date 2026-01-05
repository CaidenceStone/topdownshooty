using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPickup
{
    public void ApplyToCharacter(TDSCharacterController toApply);
    public IPickup InstantiateInstance(Vector3 position);
    public int GetSpawnTickets();

    public bool CanTake(TDSCharacterController character);
}
