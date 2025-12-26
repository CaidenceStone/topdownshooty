using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Circular Map Generation Plan", order = 0)]
public class CircularMapGenerationPlan : MapGenerationPlan
{
    public int CircleRadius = 15;
    public int WallFillingBufferSize = 5;

    public override async Task<List<Vector2Int>> GenerateMapAsync()
    {
        List<Vector2Int> spawnPoints = new List<Vector2Int>();
        List<Vector2Int> emptySpace = new List<Vector2Int>();

        for (int xx = -this.CircleRadius - WallFillingBufferSize; xx < this.CircleRadius + WallFillingBufferSize; xx++)
        {
            for (int yy = -this.CircleRadius - WallFillingBufferSize; yy < this.CircleRadius + WallFillingBufferSize; yy++)
            {
                Vector2Int thisPosition = new Vector2Int(xx, yy);
                float distanceRadius = Vector2Int.Distance(Vector2Int.zero, thisPosition);

                if (distanceRadius >= this.CircleRadius)
                {
                    spawnPoints.Add(thisPosition);
                }
                else
                {
                    emptySpace.Add(thisPosition);
                }
            }
        }

        await this.SpawnPF(this.WallPF, spawnPoints);
        return emptySpace;
    }
}
