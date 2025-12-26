using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Geometric Stamp Map Generation Plan", order = 1)]
public class GeometricStampMapGenerationPlan : MapGenerationPlan
{
    public int WallFillingBufferSize = 5;

    public int MapWidthMin = 50;
    public int MapWidthMax = 100;
    public int MapHeightMin = 50;
    public int MapHeightMax = 100;
    public int ExtraWallBuffer = 10;

    public int StampCount = 5;
    public int CircleStampRadiusMin = 10;
    public int CircleStampRadiusMax = 20;

    public int LineThicknessMin = 3;
    public int LineThicknessMax = 6;

    public override async Task GenerateMapAsync()
    {
        int chosenWidth = UnityEngine.Random.Range(this.MapWidthMin, this.MapWidthMax);
        int chosenHeight = UnityEngine.Random.Range(this.MapHeightMin, this.MapHeightMax);

        List<Vector2Int> walls = new List<Vector2Int>((chosenWidth + ExtraWallBuffer * 2) * (chosenHeight + ExtraWallBuffer * 2));
        List<Vector2Int> moldableWalls = new List<Vector2Int>(chosenWidth * chosenHeight);
        int currentIndex = 0;

        await Task.Run(() =>
        {
            for (int xx = -ExtraWallBuffer; xx < chosenWidth + ExtraWallBuffer; xx++)
            {
                for (int yy = -ExtraWallBuffer; yy < chosenHeight + ExtraWallBuffer; yy++)
                {
                    Vector2Int thisCoordinate = new Vector2Int(xx, yy);

                    if (!(thisCoordinate.x < 0 || thisCoordinate.y < 0 || thisCoordinate.x >= chosenWidth || thisCoordinate.y >= chosenHeight))
                    {
                        moldableWalls.Add(thisCoordinate);
                    }

                    int previousIndex = currentIndex;
                    int nextIndex =System.Threading.Interlocked.Increment(ref currentIndex);
                    walls.Add(thisCoordinate);
                }
            }
        });

        Vector2Int previousCenter = Vector2Int.zero;
        for (int stampIndex = 0; stampIndex < StampCount; stampIndex++)
        {
            Vector2Int center;
            Vector2Int[] wallsToRemove = StampCircle(chosenWidth, chosenHeight, moldableWalls, out center);
            foreach (Vector2Int wallToRemove in wallsToRemove)
            {
                walls.Remove(wallToRemove);
                moldableWalls.Remove(wallToRemove);
            }

            if (stampIndex > 0)
            {
                wallsToRemove = this.DrawLineBetween(chosenWidth, chosenHeight, center, previousCenter, moldableWalls);
                foreach (Vector2Int wallToRemove in wallsToRemove)
                {
                    walls.Remove(wallToRemove);
                    moldableWalls.Remove(wallToRemove);
                }
            }
            previousCenter = center;
        }

        await this.SpawnPF(this.WallPF, walls);
    }

    protected virtual Vector2Int[] StampCircle(int chosenWidth, int chosenHeight, IReadOnlyList<Vector2Int> coordinates, out Vector2Int center)
    {
        Vector2Int[] removeWalls = new Vector2Int[chosenWidth * chosenHeight];
        int randomX = UnityEngine.Random.Range(0, chosenWidth);
        int randomY = UnityEngine.Random.Range(0, chosenHeight);
        int randomRadius = UnityEngine.Random.Range(CircleStampRadiusMin, CircleStampRadiusMax);
        center = new Vector2Int(randomX, randomY);

        int usedIndex = 0;

        for (int ii = 0, coordinatesLength = coordinates.Count; ii < coordinatesLength; ii++)
        {
            Vector2Int currentCoordinate = coordinates[ii];

            if (currentCoordinate.x < 0 || currentCoordinate.y < 0 || currentCoordinate.x >= chosenWidth || currentCoordinate.y >= chosenHeight)
            {
                continue;
            }

            if (Vector2Int.Distance(coordinates[ii], center) <= randomRadius)
            {
                removeWalls[usedIndex] = coordinates[ii];
                usedIndex++;
            }
        }

        Array.Resize(ref removeWalls, usedIndex);
        return removeWalls;
    }

    protected virtual Vector2Int[] DrawLineBetween(int chosenWidth, int chosenHeight, Vector2Int pointA, Vector2Int pointB, IReadOnlyList<Vector2Int> coordinates)
    {
        Vector2Int[] removeWalls = new Vector2Int[chosenWidth * chosenHeight];
        float lineThickness = UnityEngine.Random.Range(LineThicknessMin, LineThicknessMax);

        Vector2 lineDirection = ((Vector2)(pointB - pointA)).normalized;
        int usedIndex = 0;
        for (int ii = 0, coordinateLength = coordinates.Count; ii < coordinateLength; ii++)
        {
            Vector2Int currentPosition = coordinates[ii];
            Vector2 differenceFromLine = currentPosition - pointA;
            float t = Vector2.Dot(differenceFromLine, lineDirection);
            Vector2 closestPointOnLine = pointA + lineDirection * t;
            float distance = Vector2.Distance(currentPosition, closestPointOnLine);

            if (distance < lineThickness)
            {
                removeWalls[usedIndex] = currentPosition;
                usedIndex++;
            }
        }

        return removeWalls;
    }
}