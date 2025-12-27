using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class EnemySpawnSettings
{
    [SerializeField]
    public List<SpawnSet> SpawnSets = new List<SpawnSet>();

    int spawnSetLength { get; set; } = 0;

    [SerializeField]
    public int EnemyCountToSpawn = 30;

    [SerializeField]
    public AnimationCurve MaxHealthGameOverLevels;

    [SerializeField]
    public AnimationCurve ReloadSpeedReductionOverLevels;

    public bool TryGetEntityAndTakeTicket(out Entity found)
    {
        this.CalculateSize();
        int randomRoll = Random.Range(0, this.spawnSetLength);

        for (int ii = 0; ii < spawnSetLength; ii++)
        {
            randomRoll -= this.SpawnSets[ii].SpawnTickets;

            if (randomRoll < 0)
            {
                this.SpawnSets[ii].SpawnTickets--;

                if (this.SpawnSets[ii].SpawnTickets <= 0)
                {
                    this.spawnSetLength -= this.SpawnSets[ii].SpawnTickets;
                    this.SpawnSets.RemoveAt(ii);
                }

                found = this.SpawnSets[ii].ToSpawn;
                return true;
            }
        }

        Debug.Log($"Failed to find suitable entity");

        found = null;
        return false;
    }

    int CalculateSize()
    {
        this.spawnSetLength = 0;
        for (int ii = 0; ii < this.SpawnSets.Count; ii++)
        {
            this.spawnSetLength += this.SpawnSets[ii].SpawnTickets;
        }
        return this.spawnSetLength;
    }

    public EnemySpawnSettings CloneSettings()
    {
        List<SpawnSet> spawnSets = new List<SpawnSet>();

        foreach (SpawnSet curSet in this.SpawnSets)
        {
            spawnSets.Add(curSet.CloneSet());
        }    

        return new EnemySpawnSettings()
        {
            EnemyCountToSpawn = this.EnemyCountToSpawn,
            spawnSetLength = this.CalculateSize(),
            SpawnSets = spawnSets,
            MaxHealthGameOverLevels = this.MaxHealthGameOverLevels,
            ReloadSpeedReductionOverLevels = this.ReloadSpeedReductionOverLevels
        };
    }

    public void ApplyCurrentLevelModifiers(int currentLevel)
    {
        EntityModifier modifiers = new EntityModifier()
        {
            MaximumHealthFlatAdditionModifier = GainMaxHealthModifier(currentLevel),
            ReloadSpeedPercentageReductionModifier = GainWeaponReloadSpeedModifier(currentLevel),
        };

        foreach (SpawnSet curSpawnSet in this.SpawnSets)
        {
            curSpawnSet.ApplyCurrentLevelModifiers(modifiers);
        }
    }

    public double GainMaxHealthModifier(int currentLevel)
    {
        return this.MaxHealthGameOverLevels.Evaluate(currentLevel);
    }

    public float GainWeaponReloadSpeedModifier(int currentLevel)
    {
        return this.ReloadSpeedReductionOverLevels.Evaluate(currentLevel);
    }
}
