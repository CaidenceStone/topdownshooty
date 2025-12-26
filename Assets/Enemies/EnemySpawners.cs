using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemySpawners : MonoBehaviour
{
    [SerializeReference]
    private Enemy enemyPF;
    [SerializeField]
    private float secondsBetweenEnemySpawns = 1f;
    [SerializeField]
    private float distanceToSpawnEnemyAt = 10f;

    private float curSecondsBetweenEnemySpawns { get; set; } = 0;

    private void FixedUpdate()
    {
        if (!StaticLevelDirector.GameActive)
        {
            return;
        }

        // Wait for a character to spawn
        if (!StaticLevelDirector.CurrentLevelDirector.AlivePlayers.Any())
        {
            return;
        }

        this.curSecondsBetweenEnemySpawns -= Time.deltaTime;

        if (this.curSecondsBetweenEnemySpawns > 0)
        {
            return;
        }

        this.curSecondsBetweenEnemySpawns = this.secondsBetweenEnemySpawns;

        Vector2 positionToSpawn = MapGenerator.GetRandomNegativeSpaceAwayFromPoints(StaticLevelDirector.CurrentLevelDirector.GetCharacterPositions(), distanceToSpawnEnemyAt, float.MaxValue);
        Enemy newEnemy = Instantiate(this.enemyPF, this.transform);
        newEnemy.transform.position = positionToSpawn;
    }
}
