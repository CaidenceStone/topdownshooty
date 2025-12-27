using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;


// [CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Basic Map Generation", order = 0)]
public abstract class MapGenerationPlan : ScriptableObject
{
    public GameObject WallPF;
    public TileBase WallTile;

    public abstract Task<List<SpatialCoordinate>> GenerateMapAsync(Transform root, Tilemap onMap);

    protected async Task SpawnPF(GameObject toSpawn, IEnumerable<Vector2Int> spawnPoints, Transform root)
    {
        foreach (Vector2Int spawnPoint in spawnPoints)
        {
            MapGenerator.MostBottom = Mathf.Min(MapGenerator.MostBottom, spawnPoint.y);
            MapGenerator.MostLeft = Mathf.Min(MapGenerator.MostLeft, spawnPoint.x);
            MapGenerator.MostTop = Mathf.Max(MapGenerator.MostTop, spawnPoint.y);
            MapGenerator.MostRight = Mathf.Max(MapGenerator.MostRight, spawnPoint.x);

            Instantiate(toSpawn, new Vector2(spawnPoint.x, spawnPoint.y) / MapGenerator.COORDINATETOPOSITIONDIVISOR, Quaternion.identity, root);
        }
    }

    protected async Task WriteTilemap(Tilemap toWriteOn, HashSet<Vector2Int> positions, TileBase toPlace)
    {
        foreach (Vector2Int spawnPoint in positions)
        {
            MapGenerator.MostBottom = Mathf.Min(MapGenerator.MostBottom, spawnPoint.y);
            MapGenerator.MostLeft = Mathf.Min(MapGenerator.MostLeft, spawnPoint.x);
            MapGenerator.MostTop = Mathf.Max(MapGenerator.MostTop, spawnPoint.y);
            MapGenerator.MostRight = Mathf.Max(MapGenerator.MostRight, spawnPoint.x);
        }

        int positionsCount = positions.Count;
        List<Vector2Int> positionList = new List<Vector2Int>(positions);
        Vector3Int[] positionsList = new Vector3Int[positionsCount];
        TileBase[] tilesComparativeList = new TileBase[positionsCount];

        for (int ii = 0; ii < positionsCount; ii++)
        {
            positionsList[ii] = (Vector3Int)positionList[ii];
            tilesComparativeList[ii] = toPlace;
        }

        toWriteOn.SetTiles(positionsList, tilesComparativeList);
    }
}
