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
    public float CircleStampRadiusMin = 10;
    public float CircleStampRadiusMax = 20;

    public int LineThicknessMin = 3;
    public int LineThicknessMax = 6;

    public int InnerStampMinCount = 0;
    public int InnerStampMaxCount = 3;
    public float CircleInnerStampRadiusMin = 3;
    public float CircleInnerStampRadiusMax = 5;

    public float MinDistanceBetweenStamps = 4;
    public float MaxDistanceBetweenStamps = 8;

    public override async Task<List<Vector2Int>> GenerateMapAsync()
    {
        List<Vector2Int> negativeSpace = new List<Vector2Int>();
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

        List<Vector2Int> connectionPoints = new List<Vector2Int>();
        Vector2Int? previousConnectionPoint = null;
        for (int stampIndex = 0; stampIndex < StampCount; stampIndex++)
        {
            // Pick a shape and a center point, and generate a list of all coordinates affected by the stamp
            // The first stamp is to get a maximum removal area

            Vector2Int center;
            float randomRadius = UnityEngine.Random.Range(CircleStampRadiusMin, CircleStampRadiusMax);
            List<Vector2Int> wallsToRemove = new List<Vector2Int>(StampCircle(moldableWalls, out center, randomRadius, previousConnectionPoint, MinDistanceBetweenStamps, MaxDistanceBetweenStamps));
            int originalWallsToRemoveSize = wallsToRemove.Count;
            if (originalWallsToRemoveSize == 0)
            {
                continue;
            }

            // Then make a stamp inside these affected areas, reintroducing some parts
            int randomInnerStamp = UnityEngine.Random.Range(InnerStampMinCount, InnerStampMaxCount);
            Debug.Log($"Rolled {randomInnerStamp} random inner stamps");
            for (int innerStampIndex = 0; innerStampIndex < randomInnerStamp; innerStampIndex++)
            {
                randomRadius = UnityEngine.Random.Range(CircleInnerStampRadiusMin, CircleInnerStampRadiusMax);
                IReadOnlyList<Vector2Int> wallsToRetain = StampCircle(wallsToRemove, out _, randomRadius, null, 0, 0);

                if (wallsToRetain.Count == wallsToRemove.Count)
                {
                    wallsToRemove.Clear();
                    break;
                }

                Debug.Log($"Inner stamp iteration {innerStampIndex} is removing {wallsToRetain.Count} from the removal");

                foreach (Vector2Int wallToRetain in wallsToRetain)
                {
                    wallsToRemove.Remove(wallToRetain);
                    moldableWalls.Add(wallToRetain);
                }
            }

            if (wallsToRemove.Count == 0)
            {
                continue;
            }

            Debug.Log($"Stamp is removing {wallsToRemove.Count} / {moldableWalls.Count}");

            for (int ii = wallsToRemove.Count - 1; ii >= 0; ii--)
            {
                Vector2Int wallToRemove = wallsToRemove[ii];
                walls.Remove(wallToRemove);
                moldableWalls.Remove(wallToRemove);
                negativeSpace.Add(wallToRemove);
            }

            if (wallsToRemove.Count == 0)
            {
                continue;
            }

            previousConnectionPoint = wallsToRemove[UnityEngine.Random.Range(0, wallsToRemove.Count)];
            connectionPoints.Add(previousConnectionPoint.Value);
        }

        for (int ii = 1; ii < connectionPoints.Count; ii++)
        {
            IReadOnlyList<Vector2Int> wallsToRemove = this.DrawLineBetween(connectionPoints[ii - 1], connectionPoints[ii], moldableWalls);
            foreach (Vector2Int wallToRemove in wallsToRemove)
            {
                walls.Remove(wallToRemove);
                moldableWalls.Remove(wallToRemove);
                negativeSpace.Add(wallToRemove);
            }
        }

        await this.SpawnPF(this.WallPF, walls);
        return negativeSpace;
    }

    protected virtual IReadOnlyList<Vector2Int> StampCircle(IReadOnlyList<Vector2Int> coordinates, out Vector2Int center, float radius, Vector2Int? previousLinkPosition, float minDistance, float maxDistance)
    {
        if (coordinates.Count == 0)
        {
            center = Vector2Int.zero;
            return Array.Empty<Vector2Int>();
        }

        List<Vector2Int> wallsToStamp = new List<Vector2Int>(coordinates.Count);

        if (previousLinkPosition.HasValue)
        {
            center = MapGenerator.GetNegativeSpaceFromSubset(previousLinkPosition.Value, coordinates, minDistance, maxDistance);
        }
        else
        {
            center = coordinates[UnityEngine.Random.Range(0, coordinates.Count)];
        }

        for (int ii = 0, coordinatesLength = coordinates.Count; ii < coordinatesLength; ii++)
        {
            Vector2Int currentCoordinate = coordinates[ii];

            if (Vector2Int.Distance(coordinates[ii], center) <= radius)
            {
                wallsToStamp.Add(coordinates[ii]);
            }
        }

        return wallsToStamp;
    }

    protected virtual List<Vector2Int> DrawLineBetween(Vector2Int pointA, Vector2Int pointB, IReadOnlyList<Vector2Int> coordinates)
    {
        List<Vector2Int> removeWalls = new List<Vector2Int>();
        float lineThickness = UnityEngine.Random.Range(LineThicknessMin, LineThicknessMax);

        Vector2 lineDirection = ((Vector2)(pointB - pointA)).normalized;
        for (int ii = 0, coordinateLength = coordinates.Count; ii < coordinateLength; ii++)
        {
            Vector2Int currentPosition = coordinates[ii];
            Vector2 differenceFromLine = currentPosition - pointA;
            float t = Vector2.Dot(differenceFromLine, lineDirection);

            if (t < 0 || t > differenceFromLine.magnitude)
            {
                continue;
            }

            Vector2 closestPointOnLine = pointA + lineDirection * t;
            float distance = Vector2.Distance(currentPosition, closestPointOnLine);

            if (distance < lineThickness)
            {
                removeWalls.Add(currentPosition);
            }
        }

        return removeWalls;
    }
}