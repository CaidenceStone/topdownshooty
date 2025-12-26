using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


// [CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Basic Map Generation", order = 0)]
public abstract class MapGenerationPlan : ScriptableObject
{
    public GameObject WallPF;

    public abstract Task<List<Vector2Int>> GenerateMapAsync();

    protected async Task SpawnPF(GameObject toSpawn, IEnumerable<Vector2Int> spawnPoints)
    {
        foreach (Vector2Int spawnPoint in spawnPoints)
        {
            MapGenerator.MostBottom = Mathf.Min(MapGenerator.MostBottom, spawnPoint.y);
            MapGenerator.MostLeft = Mathf.Min(MapGenerator.MostLeft, spawnPoint.x);
            MapGenerator.MostTop = Mathf.Max(MapGenerator.MostTop, spawnPoint.y);
            MapGenerator.MostRight = Mathf.Max(MapGenerator.MostRight, spawnPoint.x);

            Instantiate(toSpawn, new Vector2(spawnPoint.x, spawnPoint.y), Quaternion.identity);
        }
    }
}
