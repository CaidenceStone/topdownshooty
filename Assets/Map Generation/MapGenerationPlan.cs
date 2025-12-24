using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


// [CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Basic Map Generation", order = 0)]
public abstract class MapGenerationPlan : ScriptableObject
{
    public GameObject WallPF;

    public abstract Task GenerateMapAsync();

    protected async Task SpawnPF(GameObject toSpawn, IEnumerable<Vector2Int> spawnPoints)
    {
        foreach (Vector2Int spawnPoint in spawnPoints)
        {
            Instantiate(toSpawn, new Vector3(spawnPoint.x, spawnPoint.y), Quaternion.identity);
        }
    }
}
