using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NeutralHUD : MonoBehaviour
{
    [SerializeReference]
    private TMP_Text levelLabel;
    [SerializeReference]
    private TMP_Text remainingEnemiesLabel;
    [SerializeReference]
    private SingleLevelDirector singleLevelDirector;

    private void Awake()
    {
        this.levelLabel.text = $"Level {StaticLevelDirector.LoadedLevel}";
        this.UpdateRemainingEnemiesLabel(0);
    }

    private void UpdateRemainingEnemiesLabel(int toNumber)
    {
        this.remainingEnemiesLabel.text = $"Remaining Enemies: {toNumber}";
    }

    private void OnEnemyCountUpdated(int newCount)
    {
        this.UpdateRemainingEnemiesLabel(newCount);
    }

    private void OnEnable()
    {
        this.singleLevelDirector.OnEnemyCountUpdated += OnEnemyCountUpdated;
    }

    private void OnDisable()
    {
        this.singleLevelDirector.OnEnemyCountUpdated -= OnEnemyCountUpdated;
    }
}
