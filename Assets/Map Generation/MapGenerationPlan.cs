using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;


// [CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Basic Map Generation", order = 0)]
public abstract class MapGenerationPlan : ScriptableObject
{
    public GameObject WallPF;
    public TileBase WallTile;

    public abstract Task<IReadOnlyList<SpatialCoordinate>> GenerateMapAsync(Transform root, Tilemap onMap);

    protected void WriteTilemap(Tilemap toWriteOn, HashSet<Vector2Int> positions, TileBase toPlace)
    {
        foreach (Vector2Int spawnPoint in positions)
        {
            MapGenerator.MostBottom = Mathf.Min(MapGenerator.MostBottom, spawnPoint.y);
            MapGenerator.MostLeft = Mathf.Min(MapGenerator.MostLeft, spawnPoint.x);
            MapGenerator.MostTop = Mathf.Max(MapGenerator.MostTop, spawnPoint.y);
            MapGenerator.MostRight = Mathf.Max(MapGenerator.MostRight, spawnPoint.x);
        }

        int positionsCount = positions.Count;
        Vector3Int[] positionsList = new Vector3Int[positionsCount];
        TileBase[] tilesComparativeList = new TileBase[positionsCount];

        Array.Fill<TileBase>(tilesComparativeList, toPlace);

        int index = 0;
        foreach (Vector2Int position in positions)
        {
            positionsList[index] = (Vector3Int)position;
            index++;
        }

        toWriteOn.SetTiles(positionsList, tilesComparativeList);
    }
}
