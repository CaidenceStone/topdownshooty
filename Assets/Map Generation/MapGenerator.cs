using NavMeshPlus.Components;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public static IReadOnlyList<Vector2Int> NegativeSpace { get; private set; } = new List<Vector2Int>();

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
        List<Vector2Int> negativeSpace = await this.plan.GenerateMapAsync(this.transform, this.MyTilemap);
        NegativeSpace = negativeSpace;

        MapReady = true;
    }

    public static Vector2Int GetAnyRandomNegativeSpace()
    {
        return NegativeSpace[UnityEngine.Random.Range(0, NegativeSpace.Count)];
    }

    public static Vector2Int GetRandomNegativeSpaceNearPoint(Vector2Int near, float minDistance, float maxDistance)
    {
        return GetNegativeSpaceFromSubset(near, NegativeSpace, minDistance, maxDistance);
    }

    public static Vector2Int GetRandomNegativeSpaceAwayFromPoints(IReadOnlyList<Vector2> near, float minDistance, float maxDistance)
    {
        Vector2Int randomSpace = Vector2Int.zero;
        for (int ii = 0; ii < GETRANDOMNEARBYMAXITERATIONS; ii++)
        {
            foreach (Vector2 nearPoint in near)
            {
                randomSpace = NegativeSpace[UnityEngine.Random.Range(0, NegativeSpace.Count)];
                float distance = Vector2.Distance(randomSpace, nearPoint);

                if (distance > minDistance && distance < maxDistance)
                {
                    return randomSpace;
                }
            }
        }
        return randomSpace;
    }

    public static Vector2Int GetNegativeSpaceFromSubset(Vector2Int near, IReadOnlyList<Vector2Int> subset, float minDistance, float maxDistance)
    {
        return GetNegativeSpaceFromSubset((Vector2)near, subset, minDistance, maxDistance);
    }

    public static Vector2Int GetNegativeSpaceFromSubset(Vector2 near, IReadOnlyList<Vector2Int> subset, float minDistance, float maxDistance)
    {
        Vector2Int randomSpace = Vector2Int.zero;
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
