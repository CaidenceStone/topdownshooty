using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Geometric Stamp Map Generation Plan", order = 1)]
public class GeometricStampMapGenerationPlan : MapGenerationPlan
{
    public int CircleRadius = 15;
    public int WallFillingBufferSize = 5;

    public int MapWidthMin = 50;
    public int MapWidthMax = 100;
    public int MapHeightMin = 50;
    public int MapHeightMax = 100;
    public int ExtraWallBuffer = 10;

    public override async Task GenerateMapAsync()
    {
        int chosenWidth = UnityEngine.Random.Range(this.MapWidthMin, this.MapWidthMax);
        int chosenHeight = UnityEngine.Random.Range(this.MapHeightMin, this.MapHeightMax);

        Vector2Int[] walls = new Vector2Int[(chosenWidth + ExtraWallBuffer * 2) * (chosenHeight + ExtraWallBuffer * 2)];
        int currentIndex = 0;

        await Task.Run(() =>
        {
            for (int xx = -ExtraWallBuffer; xx < chosenWidth + ExtraWallBuffer; xx++)
            {
                for (int yy = -ExtraWallBuffer; yy < chosenHeight + ExtraWallBuffer; yy++)
                {
                    int previousIndex = currentIndex;
                    int nextIndex =System.Threading.Interlocked.Increment(ref currentIndex);
                    walls[previousIndex] = new Vector2Int(xx, yy);
                }
            }
        });

        Array.Resize(ref walls, currentIndex);
        await this.SpawnPF(this.WallPF, walls);
    }
}