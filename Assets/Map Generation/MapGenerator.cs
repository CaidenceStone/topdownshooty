using NavMeshPlus.Components;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
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

    public static IReadOnlyList<SpatialCoordinate> NegativeSpace { get; set; } = new List<SpatialCoordinate>();

    [SerializeReference]
    private MapGenerationPlan plan;
    [SerializeReference]
    private TDSCamera gameCamera;


    public static bool MapReady { get; private set; } = false;

    private void Awake()
    {
        MostLeft = int.MaxValue;
        MostRight = int.MinValue;
        MostTop = int.MinValue;
        MostBottom = int.MaxValue;
    }

    public async Task GenerateWorld()
    {
        List<SpatialCoordinate> negativeSpace = await this.plan.GenerateMapAsync(this.transform, this.MyTilemap);
        NegativeSpace = negativeSpace;

        MapReady = true;
    }

    public static Vector2Int GetAnyRandomNegativeSpace()
    {
        if (NegativeSpace.Count == 0)
        {
            Debug.Log($"There is no negative space to retrieve.");
            return Vector2Int.zero;
        }

        return NegativeSpace[UnityEngine.Random.Range(0, NegativeSpace.Count - 1)].BasedOnPosition;
    }

    public static Vector2 GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2 near, float minDistance, float maxDistance)
    {
        return GetRandomNegativeSpacePointAtDistanceRangeFromPoint(near, NegativeSpace, minDistance, maxDistance);
    }

    /*
     * WARNING: There's a bunch of duplicated code below
     * Still figuring out my footing on the arguments for some things
     * so they're redundant and messy
     * */

    public static Vector2 GetRandomNegativeSpacePointAtDistanceRangeFromPoints(IReadOnlyList<Vector2> near, float minDistance, float maxDistance)
    {
        if (near.Count == 0)
        {
            Debug.Log($"Asked to get a negative space away from nothing");
            return Vector2Int.zero;
        }

        if (NegativeSpace.Count == 0)
        {
            Debug.Log($"There is no negative space.");
            return Vector2Int.zero;
        }

        Vector2Int randomSpace = Vector2Int.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            foreach (Vector2 nearPoint in near)
            {
                randomSpace = NegativeSpace[UnityEngine.Random.Range(0, NegativeSpace.Count)].BasedOnPosition;
                float distance = Vector2.Distance(randomSpace, nearPoint);

                if (distance > minDistance && distance < maxDistance)
                {
                    return randomSpace;
                }
            }
        }
        return randomSpace;
    }

    public static Vector2Int GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2Int near, IReadOnlyList<Vector2Int> subset, float minDistance, float maxDistance)
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
            randomSpace = subset[randomIndex];
            float distance = Vector2.Distance(randomSpace, near);

            if (distance > minDistance && distance < maxDistance)
            {
                return randomSpace;
            }
        }
        return randomSpace;
    }

    public static Vector2Int GetRandomNegativeSpacePointAtDistanceRangeFromPoint(Vector2Int near, IReadOnlyList<SpatialCoordinate> subset, float minDistance, float maxDistance)
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
