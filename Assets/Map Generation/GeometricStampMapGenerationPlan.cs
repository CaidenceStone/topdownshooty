using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "MapGenerator", menuName = "Map Generation/Geometric Stamp Map Generation Plan", order = 1)]
public class GeometricStampMapGenerationPlan : MapGenerationPlan
{
    public const float SUFFICIENTDISTANCEFROMWALLFORROOMLINESS = .2f;

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

    public float MinimumWallProximity = 3;

    public TileBase WallTilebase;

    public override async Task<IReadOnlyList<SpatialCoordinate>> GenerateMapAsync(Transform root, Tilemap onMap)
    {
        HashSet<Vector2Int> negativeSpace = new HashSet<Vector2Int>();
        int chosenWidth = UnityEngine.Random.Range(this.MapWidthMin, this.MapWidthMax);
        int chosenHeight = UnityEngine.Random.Range(this.MapHeightMin, this.MapHeightMax);

        ConcurrentBag<Vector2Int> addedWalls = new ConcurrentBag<Vector2Int>();
        ConcurrentBag<Vector2Int> addedMoldableWalls = new ConcurrentBag<Vector2Int>();
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
                        addedMoldableWalls.Add(thisCoordinate);
                    }

                    int previousIndex = currentIndex;
                    int nextIndex = System.Threading.Interlocked.Increment(ref currentIndex);
                    addedWalls.Add(thisCoordinate);
                }
            }
        });


        HashSet<Vector2Int> walls = new HashSet<Vector2Int>(addedWalls);
        List<Vector2Int> moldableWalls = new List<Vector2Int>(addedMoldableWalls);

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

        if (connectionPoints.Count >= 2)
        {
            Debug.Log($"Connecting the maps of {connectionPoints.Count} connection points");
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
        }
        

        // Finally, we need to eliminate "islands"
        // It is likely that there will be spaces that aren't connected to any other open space
        // Find the largest island, and turn the rest in to walls

        // Temporarily set the negative space so that connections can be built
        Debug.Log($"Setting negative space to {negativeSpace.Count} tiles and baking spatial reasoning calculator");

        this.WriteTilemap(onMap, walls, WallTilebase);

        await Awaitable.NextFrameAsync();

        NativeArray<Vector2Int> negativeSpaceNativeArray = new NativeArray<Vector2Int>(negativeSpace.Count, Allocator.Persistent);

        await Task.Run(() =>
        {
            int currentNegativeSpaceIndex = 0;
            foreach (Vector2Int space in negativeSpace)
            {
                negativeSpaceNativeArray[currentNegativeSpaceIndex] = space;
                currentNegativeSpaceIndex++;
            }
        });

        MapBakingResult bakedCoordinates = await SpatialReasoningCalculator.BakeNeighborsGridAsync(negativeSpaceNativeArray, negativeSpace.Count, SUFFICIENTDISTANCEFROMWALLFORROOMLINESS);
        negativeSpaceNativeArray.Dispose();

        if (!Application.isPlaying)
        {
            return null;
        }

        List<SpatialCoordinate> wallsToRefill = new List<SpatialCoordinate>();
        Debug.Log($"Finding largest island out of {negativeSpace.Count}");

        IEnumerable<SpatialCoordinate> largestIsland = SpatialReasoningCalculator.FindLargestIsland(bakedCoordinates.AllCoordinates, out wallsToRefill, out int largestIslandSize);

        Debug.Log($"Limiting negative space to {largestIslandSize}");
        List<SpatialCoordinate> limitedCoordinates = SpatialReasoningCalculator.LimitTo(bakedCoordinates.AllCoordinates, largestIsland.ToHashSet(), bakedCoordinates.CoordinatesWithLegroom.ToHashSet(), out IReadOnlyList<SpatialCoordinate> newCoordinatesWithRoom);
        SpatialReasoningCalculator.SetPositions(bakedCoordinates.Chunks);

        foreach (SpatialCoordinate refillWalls in wallsToRefill)
        {
            walls.Add(refillWalls.BasedOnPosition);
        }

        onMap.ClearAllTiles();
        this.WriteTilemap(onMap, walls, WallTilebase);
        // await this.SpawnPF(this.WallPF, walls, root);
        return limitedCoordinates;
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
            center = MapGenerator.GetRandomNegativeSpacePointAtDistanceRangeFromPoint(previousLinkPosition.Value, coordinates, minDistance, maxDistance);
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
        List<Vector2Int> foundCoordinates = new List<Vector2Int>();
        float lineThickness = UnityEngine.Random.Range(LineThicknessMin, LineThicknessMax);

        Vector2 diff = (pointB - pointA);
        Vector2 lineDirection = ((Vector2)(diff)).normalized;
        float lineDistance = (pointB - pointA).magnitude;

        for (int ii = 0, coordinateLength = coordinates.Count; ii < coordinateLength; ii++)
        {
            Vector2Int currentPosition = coordinates[ii];
            float distance = SpatialReasoningCalculator.GetClosestDistanceFromLineSegment(currentPosition, pointA, pointB, lineDirection, lineDistance);

            if (distance < lineThickness)
            {
                foundCoordinates.Add(currentPosition);
            }
        }

        return foundCoordinates;
    }

    protected virtual IReadOnlyList<Vector2Int> DrawLineBetween(Vector2Int pointA, Vector2Int pointB, IReadOnlyList<Vector2Int> coordinates, Vector2 lineDirection, float lineDistance)
    {
        ConcurrentBag<Vector2Int> removeWalls = new ConcurrentBag<Vector2Int>();
        float lineThickness = UnityEngine.Random.Range(LineThicknessMin, LineThicknessMax);

        int coordinatesCount = coordinates.Count;
        Task[] selectionTasks = new Task[coordinatesCount];

        Debug.Log($"Drawing a line from {pointA} to {pointB} across {coordinatesCount} coordinates");

        for (int ii = 0; ii < coordinatesCount; ii++)
        {
            int thisIndex = ii;
            Vector2Int currentPosition = coordinates[thisIndex];
            float distance = SpatialReasoningCalculator.GetClosestDistanceFromLineSegment(currentPosition, pointA, pointB, lineDirection, lineDistance);

            if (distance < lineThickness)
            {
                removeWalls.Add(currentPosition);
            }
        }

        Debug.Log($"Drawing a line in the subset of coordinates between {pointA} and {pointB} results in selecting {removeWalls.Count}.");

        return removeWalls.ToArray();
    }
}