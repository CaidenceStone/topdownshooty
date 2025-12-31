using NavMeshPlus.Components;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    public const float COORDINATETOPOSITIONDIVISOR = 2f;
    const int GETRANDOMNEARBYMAXITERATIONS = 1000;

    public static int MostLeft = int.MaxValue;
    public static int MostRight = int.MinValue;
    public static int MostTop = int.MinValue;
    public static int MostBottom = int.MaxValue;

    [SerializeReference]
    public Tilemap MyTilemap;

    public static IReadOnlyList<SpatialCoordinate> NegativeSpaceSpatialCoordinates { get; set; } = new List<SpatialCoordinate>();
    public static int NegativeSpaceSize { get; set; } = 0;

    [SerializeReference]
    private MapGenerationPlan plan;
    [SerializeReference]
    private TDSCamera gameCamera;


    public static bool MapReady { get; private set; } = false;

    [SerializeField]
    public bool UseSeed = false;
    [SerializeField]
    public int Seed = 0;

    private void Awake()
    {
        MostLeft = int.MaxValue;
        MostRight = int.MinValue;
        MostTop = int.MinValue;
        MostBottom = int.MaxValue;
    }

    public async Task GenerateWorld()
    {
        if (this.UseSeed)
        {
            Random.InitState(Seed);
        }

        MapReady = false;

        await this.plan.GenerateMapAsync(this.transform, this.MyTilemap);
        NegativeSpaceSize = NegativeSpaceSpatialCoordinates.Count;

        MapReady = true;
    }

    public static Vector2Int GetAnyRandomNegativeSpace(List<SpatialCoordinate> fromList)
    {
        if (fromList.Count == 0)
        {
            Debug.Log($"There is no negative space to retrieve.");
            return Vector2Int.zero;
        }

        return fromList[UnityEngine.Random.Range(0, fromList.Count - 1)].BasedOnPosition;
    }

    public static Vector2 GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2 near, float minDistance, float maxDistance, float minimumWallDistance)
    {
        if (SpatialReasoningCalculator.NegativeSpaceWithLegRoom.Count != 0)
        {
            return GetRandomNegativeSpacePointAtDistanceRangeFromPoint(near, SpatialReasoningCalculator.NegativeSpaceWithLegRoom, minDistance, maxDistance, minimumWallDistance);
        }
        return GetRandomNegativeSpacePointAtDistanceRangeFromPoint(near, NegativeSpaceSpatialCoordinates, minDistance, maxDistance, minimumWallDistance);
    }

    /*
     * WARNING: There's a bunch of duplicated code below
     * Still figuring out my footing on the arguments for some things
     * so they're redundant and messy
     * */

    public static Vector2 GetRandomNegativeSpacePointAtDistanceRangeFromPoints(List<MapChunk> chunks, HashSet<Vector2> near, float minDistance, float maxDistance, float minimumRoomliness)
    {
        if (near.Count == 0)
        {
            Debug.Log($"Asked to get a negative space away from nothing");
            return Vector2.zero;
        }

        if (chunks.Count == 0)
        {
            Debug.LogError($"There are no chunks to search through.");
            return Vector2.zero;
        }

        List<SpatialCoordinate> candidateCoordinate = new List<SpatialCoordinate>();

        foreach (MapChunk chunk in chunks)
        {
            HashSet<SpatialCoordinate> chunkCoordinateCandidates = new HashSet<SpatialCoordinate>();
            foreach (Vector2 nearPoint in near)
            {
                float distanceToChunk = Vector2.Distance(chunk.VisualCenter, nearPoint);
                if (distanceToChunk < minDistance - chunk.ChunkHalfWidth || distanceToChunk > maxDistance + chunk.ChunkHalfWidth)
                {
                    // Not a match
                    break;
                }

                foreach (SpatialCoordinate coordinate in chunk.CoordinatesInChunk)
                {
                    if (coordinate.ClosestWallInWorldSpace < minimumRoomliness)
                    {
                        continue;
                    }

                    float distanceToCoordinate = Vector2.Distance(coordinate.WorldPosition, nearPoint);
                    if (distanceToCoordinate < minDistance || distanceToCoordinate > maxDistance)
                    {
                        break;
                    }

                    chunkCoordinateCandidates.Add(coordinate);
                }
            }

            // TODO: Ensure that this is near ALL applicable points
            candidateCoordinate.AddRange(chunkCoordinateCandidates);
        }

        if (candidateCoordinate.Count == 0)
        {
            Debug.LogError($"There are no valid candidate coordinates.");
            return Vector2.zero;
        }

        Vector2 randomSpace = Vector2.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            foreach (Vector2 nearPoint in near)
            {
                randomSpace = candidateCoordinate[UnityEngine.Random.Range(0, candidateCoordinate.Count)].WorldPosition;
                float distance = Vector2.Distance(randomSpace, nearPoint);

                if (distance > minDistance && distance < maxDistance)
                {
                    return randomSpace;
                }
            }
        }
        Debug.Log("Failed to find any suitable space");
        return randomSpace;
    }

    public static Vector2Int GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2Int near, HashSet<Vector2Int> subset, float minDistance, float maxDistance)
    {
        if (subset.Count == 0)
        {
            Debug.Log("Asked to pick from an empty subset");
            return default(Vector2Int);
        }

        Vector2Int randomSpace = Vector2Int.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            int randomIndex = UnityEngine.Random.Range(0, subset.Count);

            foreach (Vector2Int vector in subset)
            {
                randomIndex--;
                if (randomIndex < 0)
                {
                    randomSpace = vector;
                    break;
                }
            }

            float distance = Vector2.Distance(randomSpace, near);

            if (distance > minDistance && distance < maxDistance)
            {
                return randomSpace;
            }
        }
        return randomSpace;
    }

    public static Vector2 GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2 near, IReadOnlyList<SpatialCoordinate> subset, float minDistance, float maxDistance, float minimumRoomliness)
    {
        if (subset.Count == 0)
        {
            Debug.Log("Asked to pick from an empty subset");
            return default(Vector2Int);
        }

        Vector2Int randomSpace = Vector2Int.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            int randomIndex = UnityEngine.Random.Range(0, subset.Count);
            randomSpace = subset[randomIndex].BasedOnPosition;
            float distance = Vector2.Distance(randomSpace, near);

            if (subset[randomIndex].ClosestWallInWorldSpace < minimumRoomliness)
            {
                continue;
            }

            if (distance > minDistance && distance < maxDistance)
            {
                return randomSpace;
            }
        }

        Debug.Log($"Couldn't find an appropriate negative space, so just returning the near point.");
        return near;
    }

    public static Vector2Int GetRandomNegativeSpacePointAtDistanceRangeFromPoint(SpatialCoordinate near, IReadOnlyList<SpatialCoordinate> subset, float minDistance, float maxDistance)
    {
        if (subset.Count == 0)
        {
            Debug.Log("Asked to pick from an empty subset");
            return default(Vector2Int);
        }

        Vector2Int randomSpace = Vector2Int.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            int randomIndex = UnityEngine.Random.Range(0, subset.Count);
            randomSpace = subset[randomIndex].BasedOnPosition;
            float distance = Vector2.Distance(randomSpace, near.WorldPosition);

            if (distance > minDistance && distance < maxDistance)
            {
                return randomSpace;
            }
        }
        return randomSpace;
    }

    public static Vector2 GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2 near, IReadOnlyList<SpatialCoordinate> subset, float minDistance, float maxDistance)
    {
        if (subset.Count == 0)
        {
            Debug.Log("Asked to pick from an empty subset");
            return default(Vector2);
        }

        Vector2 randomSpace = Vector2.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            randomSpace = subset[UnityEngine.Random.Range(0, subset.Count)].BasedOnPosition;
            float distance = Vector2.Distance(randomSpace, near);

            if (distance > minDistance && distance < maxDistance)
            {
                return randomSpace;
            }
        }
        return randomSpace;
    }

    public static Vector2 GetNegativeSpaceFromReasonableDistanceFromSubset(Vector2 near, IReadOnlyList<Vector2> subset, float minDistance, float maxDistance)
    {
        if (subset.Count == 0)
        {
            Debug.Log("Asked to pick from an empty subset");
            return default(Vector2);
        }

        Vector2 randomSpace = Vector2.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            randomSpace = subset[UnityEngine.Random.Range(0, subset.Count)];
            float distance = Vector2.Distance(randomSpace, near);

            if (distance > minDistance && distance < maxDistance)
            {
                return randomSpace;
            }
        }
        return randomSpace;
    }
}
