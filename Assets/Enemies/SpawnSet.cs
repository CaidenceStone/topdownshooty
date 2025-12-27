using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnSet
{
    [SerializeReference]
    public Entity ToSpawn;

    [SerializeField]
    public int SpawnTickets = 1;

    public SpawnSet CloneSet()
    {
        return new SpawnSet()
        {
            SpawnTickets = SpawnTickets,
            ToSpawn = ToSpawn
        };
    }

    public void ApplyCurrentLevelModifiers(EntityModifier modifiers)
    {
        this.ToSpawn.Modifiers = modifiers;
    }
}
