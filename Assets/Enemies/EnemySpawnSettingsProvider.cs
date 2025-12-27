using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnSettingsProvider : MonoBehaviour
{
    [SerializeField]
    private List<EnemySpawnSettings> EnemySpawnSettingsList = new List<EnemySpawnSettings>();
    public EnemySpawnSettings GenerateEnemySpawnSettings()
    {
        if (this.EnemySpawnSettingsList.Count == 0)
        {
            Debug.LogError($"Oops, there were no enemy spawn settings. You should set some.");
            return null;
        }

        int indexToReturn = System.Math.Clamp(this.EnemySpawnSettingsList.Count - 1, 0, StaticLevelDirector.LoadedLevel - 1);
        EnemySpawnSettings template = this.EnemySpawnSettingsList[indexToReturn];
        EnemySpawnSettings clone = template.CloneSettings();

        clone.ApplyCurrentLevelModifiers(StaticLevelDirector.LoadedLevel);

        return clone;
    }
}
