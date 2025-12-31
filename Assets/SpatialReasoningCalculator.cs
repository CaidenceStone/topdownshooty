using NUnit.Framework.Internal;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.VisualScripting;
using Unity.XR.OpenVR;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SpatialReasoningCalculator : MonoBehaviour
{
    /// <summary>
    /// Determines how far (in worldspace coordinates) a SpatialCoordinate searches for nearby neighbors.
    /// Having a higher value results in smoother neighbor gradiations during pathfinding.
    /// Having a lower value decreases computation time and space for map baking.
    /// </summary>
    const float MAXIMUMNEIGHBORDISTANCE = 2.5f;


    /// <summary>
    /// When determining if a coordinate is close enough to a line, it needs to be no farther than
    /// this much worldspace units away to count.
    /// Higher numbers result in larger, fuzzier areas.
    /// Lower numbers result in faster computation time, but risks false negatives.
    /// </summary>
    const float NEIGHBORLINETOLERANCE = .075f;

    const float SPHERECASTNEIGHBORCHECKRADIUS = .3f;

    const float OVERLAPINITIALCHECKCIRCLERADIUS = .3f;

    /// <summary>
    /// When generating a map, break tiles in to chunks of this size squared.
    /// </summary>
    public const int CHUNKDIMENSIONSIZE = 10;

    public static SpatialReasoningCalculator CurrentInstance { get; private set; }
    public static List<SpatialCoordinate> NegativeSpaceWithLegRoom { get; private set; } = new List<SpatialCoordinate>();

    private bool baked { get; set; } = false;
    public Dictionary<Vector2Int, SpatialCoordinate> Positions { get; set; } = new Dictionary<Vector2Int, SpatialCoordinate>();
    public List<MapChunk> Chunks { get; private set; } = new List<MapChunk>();

    [SerializeField]
    private LayerMask environmentLayerMask;
    [SerializeField]
    private int raycastDivisionSteps = 4 * 4 * 4;
    private Vector2[] raycastAnglesToCheckVector2s { get; set; } = new Vector2[] { };
    [SerializeField]
    private Vector2Int[] orthogonalNeighbors = new Vector2Int[] { };
    private Dictionary<Vector2Int, Vector2Int[]> relativeNeighborToNeighbors { get; set; } = new Dictionary<Vector2Int, Vector2Int[]>();
    [SerializeReference]
    public Transform MapGeneratorTransform;

    private static List<SpatialCoordinate> spatialCoordinatesReusableList { get; set; } = new List<SpatialCoordinate>();

    public bool TryGetPath(SpatialCoordinate startingCoordinate, Vector2 startingPosition, SpatialCoordinate goalCoordinate, Vector2 goalVector, float minimumWallDistance, out Path calculatedPath)
    {
        if (startingCoordinate == goalCoordinate)
        {
            // We're already here!
            calculatedPath = new Path(startingPosition, goalVector);
            return true;
        }

        Dictionary<SpatialCoordinate, PriorityQueueEntry<SpatialCoordinate>> previousVisited = new Dictionary<SpatialCoordinate, PriorityQueueEntry<SpatialCoordinate>>();
        PriorityQueue<SpatialCoordinate> coordinateFrontier = new PriorityQueue<SpatialCoordinate>();
        PriorityQueueEntry<SpatialCoordinate> startingSpatialCoordinatePriorityEntry = new PriorityQueueEntry<SpatialCoordinate>(startingCoordinate);
        coordinateFrontier.AddEntry(new PriorityQueueEntry<SpatialCoordinate>(startingCoordinate));
        previousVisited.Add(startingCoordinate, startingSpatialCoordinatePriorityEntry);

        // For the first iteration, we need to account for the subposition in this vector; build a scoring list based on current position rather than what neighbors say
        bool firstEntryException = true;

        int coordinateFrontiersCalculated = 0;
        while (coordinateFrontier.TryGetHighestScoringValue(out PriorityQueueEntry<SpatialCoordinate> currentFrontierEntry))
        {
            coordinateFrontiersCalculated++;

            // For each neighbor relationship this coordinate has, determine which one is the closest to the goal
            foreach (SpatialCoordinate coord in currentFrontierEntry.Value.NeighborsToDistances.Keys)
            {
                double distanceTowardsNode;

                if (firstEntryException)
                {
                    distanceTowardsNode = Vector2.Distance(startingPosition, coord.WorldPosition);
                }
                else
                {
                    distanceTowardsNode = currentFrontierEntry.Value.NeighborsToDistances[coord];
                }

                if (ReferenceEquals(coord, goalCoordinate))
                {
                    // Debug.Log($"Found path with {coordinateFrontiersCalculated} iterations");

                    // Found our goal!
                    List<SpatialCoordinate> coordinatePath = currentFrontierEntry.AllEntriesToList();

                    List<Vector2> vectorPath = new List<Vector2>() { };
                    for (int ii = 0; ii < coordinatePath.Count - 1; ii++)
                    {
                        vectorPath.Add(coordinatePath[ii].WorldPosition);
                    }
                    vectorPath.Add(goalCoordinate.WorldPosition);

                    calculatedPath = new Path(startingPosition, vectorPath);
                    return true;
                }

                if (coord.ClosestWallInWorldSpace < minimumWallDistance)
                {
                    continue;
                }

                // The distance from this newly considered coordinate and the goal is the distance to goal
                // The score is the total travel cost (distances covered) minus the distance to the goal from this tile
                float distanceToGoal = Vector2.Distance(coord.WorldPosition, goalVector);
                double travelCost = currentFrontierEntry.TravelCost + distanceTowardsNode;
                double score = -distanceToGoal - currentFrontierEntry.TravelCost;
                PriorityQueueEntry<SpatialCoordinate> consideredNewEntry = new PriorityQueueEntry<SpatialCoordinate>(coord, score, currentFrontierEntry.TravelCost + distanceTowardsNode, currentFrontierEntry);

                if (previousVisited.TryGetValue(coord, out PriorityQueueEntry<SpatialCoordinate> previousEntry))
                {
                    // If we've visited this node before, but our current path is better scoring, replace it
                    if (consideredNewEntry.Score > previousEntry.Score)
                    {
                        // Debug.Log($"Replacing an entry for {previousEntry.Value.BasedOnPosition} with {consideredNewEntry.Value.BasedOnPosition} because {consideredNewEntry.Score} > {previousEntry.Score}");
                        previousVisited[coord] = consideredNewEntry;
                        coordinateFrontier.AddEntry(consideredNewEntry);
                    }
                    else
                    {
                        // Otherwise if our score is worse, just skip this one
                        continue;
                    }
                }
                else
                {
                    previousVisited.Add(consideredNewEntry.Value, consideredNewEntry);
                    coordinateFrontier.AddEntry(consideredNewEntry);
                }
            }

            // All other entries can use cached values
            firstEntryException = false;
        }

        Debug.Log($"I couldn't find a path from {startingPosition} to {goalVector}! Had {coordinateFrontiersCalculated} iterations.");
        calculatedPath = null;
        return false;
    }

    private void OnEnable()
    {
        CurrentInstance = this;
    }

    private void OnDisable()
    {
        CurrentInstance = null;
    }

    public static async Task<List<SpatialCoordinate>> GetPositionsWithinDistanceOfLineAsync(IReadOnlyList<SpatialCoordinate> bakedCoordinates, NativeArray<Vector2> bakedCoordinateVector2s, float distancePermitted, Vector2 pointA, Vector2 pointB, Vector2 precalculatedLineDirection, float precalculatedLineDistance)
    {
        int bakedCoordinatesCount = bakedCoordinates.Count;
        ConcurrentBag<SpatialCoordinate> spatialCoordinatesWithinDistanceOfLine = new ConcurrentBag<SpatialCoordinate>();

        await Task.Run(() =>
        {
            for (int ii = 0; ii < bakedCoordinatesCount; ii++)
            {
                if (IsPointCloseEnoughToLine(bakedCoordinateVector2s[ii], pointA, pointB, precalculatedLineDirection, distancePermitted, precalculatedLineDistance))
                {
                    spatialCoordinatesWithinDistanceOfLine.Add(bakedCoordinates[ii]);
                }
            }
        });

        // Debug.Log($"{spatialCoordinatesWithinDistanceOfLine.Count} detected");

        return spatialCoordinatesWithinDistanceOfLine.ToList();
    }

    public static async Task<List<SpatialCoordinate>> GetPositionsWithinDistanceOfLineAsync(IReadOnlyList<MapChunk> bakedChunks, float distancePermitted, Vector2 pointA, Vector2 pointB, Vector2 precalculatedLineDirection, float precalculatedLineDistance)
    {
        int bakedChunksCount = bakedChunks.Count;

        // A coordinate can only be relevant to this process if the chunk that it is in is close enough to the operation
        float acceptableDistanceForChunkInclusion = (precalculatedLineDistance / 2f + distancePermitted);
        List<MapChunk> chunksWithinDistance = new List<MapChunk>();

        await Task.Run(() =>
        {
            for (int ii = 0; ii < bakedChunksCount; ii++)
            {
                if (Vector2.Distance(bakedChunks[ii].VisualCenter, pointA) < acceptableDistanceForChunkInclusion)
                {
                    chunksWithinDistance.Add(bakedChunks[ii]);
                }
            }
        });

        int chunkCount = chunksWithinDistance.Count;
        ConcurrentBag<SpatialCoordinate> spatialCoordinatesWithinDistanceOfLine = new ConcurrentBag<SpatialCoordinate>();

        await Task.Run(() =>
        {
            for (int ii = 0; ii < chunkCount; ii++)
            {
                List<SpatialCoordinate> coordinatesInChunk = chunksWithinDistance[ii].CoordinatesInChunk;
                for (int jj = 0; jj < coordinatesInChunk.Count; jj++)
                {
                    SpatialCoordinate thisCoordinate = coordinatesInChunk[jj];
                    if (IsPointCloseEnoughToLine(thisCoordinate.WorldPosition, pointA, pointB, precalculatedLineDirection, distancePermitted, precalculatedLineDistance))
                    {
                        spatialCoordinatesWithinDistanceOfLine.Add(thisCoordinate);
                    }
                }
            }
        });

        // Debug.Log($"{spatialCoordinatesWithinDistanceOfLine.Count} detected");

        return spatialCoordinatesWithinDistanceOfLine.ToList();
    }

    public static float GetClosestDistanceFromLineSegment(Vector2 positionToCheck, Vector2 linePointA, Vector2 linePointB, Vector2 precalculatedTrustedLineDirection, float precalculatedLineLength)
    {
        Vector2 differenceFromLine = positionToCheck - linePointA;
        float t = Mathf.Clamp(Vector2.Dot(differenceFromLine, precalculatedTrustedLineDirection), 0, precalculatedLineLength);
        return Vector2.Distance(positionToCheck, linePointA + precalculatedTrustedLineDirection * t);
    }

    public static IEnumerable<SpatialCoordinate> FindLargestIsland(MapBakingResult baking, out List<SpatialCoordinate> wallsToRefill, out int largestIslandSize)
    {
        List<SpatialCoordinate> allCoordinates = baking.AllCoordinates;
        Debug.Log($"Finding largest island out of {allCoordinates.Count}");

        if (allCoordinates.Count == 0)
        {
            Debug.Log($"The negative space list is empty, nothing to refill.");
            wallsToRefill = new List<SpatialCoordinate>();
            largestIslandSize = 0;
            return new List<SpatialCoordinate>();
        }

        // Debug.Log($"Asked to parse {negativeSpaceList.Count} negative spaces into islands");

        wallsToRefill = new List<SpatialCoordinate>(allCoordinates.Count);
        List<SpatialCoordinate[]> islands = new List<SpatialCoordinate[]>();
        List<int> islandSizes = new List<int>();

        HashSet<SpatialCoordinate> remainingCoordinatesToCheck = new HashSet<SpatialCoordinate>(allCoordinates);
        SpatialCoordinate placeToStart = allCoordinates[0];
        while (remainingCoordinatesToCheck.Count > 0)
        {
            placeToStart = remainingCoordinatesToCheck.First();
            remainingCoordinatesToCheck.Remove(placeToStart);

            int currentCoordinateIndex = 1;
            SpatialCoordinate[] thisIslandCoordinates = new SpatialCoordinate[allCoordinates.Count];
            thisIslandCoordinates[0] = placeToStart;

            Queue<SpatialCoordinate> nextCheckList = new Queue<SpatialCoordinate>(allCoordinates.Count);
            nextCheckList.Enqueue(placeToStart);

            do
            {
                SpatialCoordinate thisCheck = nextCheckList.Dequeue();
                foreach (SpatialCoordinate neighbor in thisCheck.NeighborsToDistances.Keys)
                {
                    if (remainingCoordinatesToCheck.Contains(neighbor))
                    {
                        thisIslandCoordinates[currentCoordinateIndex] = neighbor;
                        remainingCoordinatesToCheck.Remove(neighbor);
                        nextCheckList.Enqueue(neighbor);
                        currentCoordinateIndex++;
                    }
                }
            } while (nextCheckList.Count > 0) ;

            // Debug.Log($"Island identified with {currentCoordinateIndex} coordinates");

            // If we've reached here, we couldn't find any neighbors-of-neighbors
            islandSizes.Add(currentCoordinateIndex);
            islands.Add(thisIslandCoordinates);
        }

        if (islands.Count == 0)
        {
            Debug.Log($"Couldn't find any islands in the negative space of {allCoordinates.Count}");
            largestIslandSize = 0;
            return new List<SpatialCoordinate>();
        }

        // Now to find the largest island
        int largestIslandIndex = 0;
        int largestIslandCount = 0;

        for (int ii = 0; ii < islands.Count; ii ++)
        {
            int islandSize = islandSizes[ii];

            if (islandSize > largestIslandCount)
            {
                largestIslandCount = islandSize;
                largestIslandIndex = ii;
            }
        }

        for (int ii = 0; ii < islands.Count; ii++)
        {
            if (ii != largestIslandIndex)
            {
                wallsToRefill.AddRange(islands[ii].Take(islandSizes[ii]));
            }
        }

        Debug.Log($"The largest island was size {largestIslandCount}, index {largestIslandIndex}, with {wallsToRefill.Count} walls to refill");

        largestIslandSize = islandSizes[largestIslandIndex];
        return islands[largestIslandIndex].Take(largestIslandSize);
    }

    public static List<SpatialCoordinate> RemoveNarrowSpaces(IReadOnlyList<SpatialCoordinate> toRestrict, float roomliness, out List<SpatialCoordinate> wallsToRefill)
    {
        List<SpatialCoordinate> screenedCoordinates = new List<SpatialCoordinate>();
        wallsToRefill = new List<SpatialCoordinate>();

        for (int ii = 0; ii < toRestrict.Count; ii++)
        {
            if (toRestrict[ii].ClosestWallInWorldSpace >= roomliness)
            {
                screenedCoordinates.Add(toRestrict[ii]);
            }
            else
            {
                wallsToRefill.Add(toRestrict[ii]);
            }
        }

        return screenedCoordinates;
    }

    public static List<SpatialCoordinate> RemoveNarrowSpaces(MapBakingResult toRestrict, int requiredNeighborsForOpenness, out List<SpatialCoordinate> wallsToRefill)
    {
        List<SpatialCoordinate> screenedCoordinates = new List<SpatialCoordinate>();
        wallsToRefill = new List<SpatialCoordinate>();

        for (int ii = 0; ii < toRestrict.AllCoordinates.Count; ii++)
        {
            SpatialCoordinate thisCoordinate = toRestrict.AllCoordinates[ii];
            if (thisCoordinate.NeighborsToDistances.Count >= requiredNeighborsForOpenness)
            {
                screenedCoordinates.Add(thisCoordinate);
            }
            else
            {
                wallsToRefill.Add(thisCoordinate);
            }
        }

        return screenedCoordinates;
    }

    public static void LimitTo(MapBakingResult bakedResults, HashSet<SpatialCoordinate> limitingSubset)
    {
        int limitedCoordinates = 0;
        int positionsToConsiderLength = bakedResults.AllCoordinates.Count;
        for (int ii = positionsToConsiderLength - 1; ii >= 0; ii--)
        {
            SpatialCoordinate coordinate = bakedResults.AllCoordinates[ii];
            if (!limitingSubset.Contains(coordinate))
            {
                foreach (SpatialCoordinate neighboringCoordinate in coordinate.NeighborsToDistances.Keys)
                {
                    neighboringCoordinate.NeighborsToDistances.Remove(coordinate);
                }
                coordinate.NeighborsToDistances.Clear();
                bakedResults.AllCoordinates.RemoveAt(ii);
                bakedResults.CoordinatesWithLegroom.Remove(coordinate);
                coordinate.FromChunk.CoordinatesInChunk.Remove(coordinate);
                coordinate.FromChunk.CoordinatesInChunkWithLegRoom.Remove(coordinate);

                limitedCoordinates++;
            }
        }

        if (limitedCoordinates > 0)
        {
            Debug.Log($"Limited away {limitedCoordinates} coordinates");
        }
    }

    public static bool IsPointCloseEnoughToLine(Vector2 position, Vector2 linePointA, Vector2 linePointB, Vector2 precalculatedTrustedLineDirection, float threshold, float precalculatedLineLength)
    {
        return GetClosestDistanceFromLineSegment(position, linePointA, linePointB, precalculatedTrustedLineDirection, precalculatedLineLength) <= threshold;
    }

    public static void SetPositions(IReadOnlyList<SpatialCoordinate> positions, IReadOnlyList<SpatialCoordinate> positionsWithRoom)
    {
        CurrentInstance.Positions.Clear();
        foreach (SpatialCoordinate coordinate in positions)
        {
            CurrentInstance.Positions.Add(coordinate.BasedOnPosition, coordinate);
        }

        NegativeSpaceWithLegRoom.Clear();
        foreach (SpatialCoordinate coordinate in positionsWithRoom)
        {
            NegativeSpaceWithLegRoom.Add(coordinate);
        }
    }


    public static void SetPositions(IReadOnlyList<MapChunk> chunks)
    {
        CurrentInstance.Chunks.Clear();
        CurrentInstance.Positions.Clear();
        NegativeSpaceWithLegRoom.Clear();
        CurrentInstance.Chunks.AddRange(chunks);

        // Debug.Log($"Setting positions with {chunks.Count} chunks");

        foreach (MapChunk chunk in chunks)
        {
            // Debug.Log($"Setting position for chunk with {chunk.CoordinatesInChunk.Count} coordinates in the chunk");

            foreach (SpatialCoordinate coordinate in chunk.CoordinatesInChunk)
            {
                CurrentInstance.Positions.Add(coordinate.BasedOnPosition, coordinate);
            }

            // Debug.Log($"Setting roomy positions for chunk with {chunk.CoordinatesInChunkWithLegRoom.Count} coordinates in the chunk");

            foreach (SpatialCoordinate coordinate in chunk.CoordinatesInChunkWithLegRoom)
            {
                NegativeSpaceWithLegRoom.Add(coordinate);
            }
        }
    }

    [Obsolete]
    public static async Task<MapBakingResult> BakeNeighborsAsync(NativeArray<Vector2Int> positions, int positionsLength, float roomThreshold)
    {
        // Generate the spatial awareness raycasts by dividing them by the step count
        CurrentInstance.raycastAnglesToCheckVector2s = new Vector2[CurrentInstance.raycastDivisionSteps];
        for (int ii = 0; ii < CurrentInstance.raycastDivisionSteps; ii++)
        {
            float angle = Mathf.Lerp(0, 360f, (float)ii / (float)CurrentInstance.raycastDivisionSteps);
            CurrentInstance.raycastAnglesToCheckVector2s[ii] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }

        // First build a lookup table of every spatial coordinate
        List<SpatialCoordinate> everyCoordinate = new List<SpatialCoordinate>(positionsLength);
        List<SpatialCoordinate> foundCoordinatesWithRoom = new List<SpatialCoordinate>(positionsLength);
        NativeArray<Vector2> everyCoordinateVector2 = new NativeArray<Vector2>(positionsLength, Allocator.Persistent);

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        await Task.Run(() =>
        {
            for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
            {
                Vector2Int currentCoordinate = positions[positionIndex];
                SpatialCoordinate newCoordinate = new SpatialCoordinate(currentCoordinate, null);
                everyCoordinate.Add(newCoordinate);
                everyCoordinateVector2[positionIndex] = newCoordinate.WorldPosition;

                min.x = Mathf.Min(min.x, currentCoordinate.x);
                min.y = Mathf.Min(min.y, currentCoordinate.y);
                max.x = Mathf.Max(max.x, currentCoordinate.x);
                max.y = Mathf.Max(max.y, currentCoordinate.y);
            }
        });

        // Break the coordinates in to a grid that can contain each of them in chunks
        Dictionary<Vector2Int, MapChunk> chunkCoordinateToChunk = new Dictionary<Vector2Int, MapChunk>();
        for (int xx = min.x; xx <= max.x; xx += CHUNKDIMENSIONSIZE)
        {
            for (int yy = min.y; yy <= max.y; yy += CHUNKDIMENSIONSIZE)
            {
                Vector2Int chunkCoordinate = GetChunkCoordinate(xx, yy);
                chunkCoordinateToChunk.Add(chunkCoordinate, new MapChunk(chunkCoordinate));
            }
        }

        // Now sort each coordinate in to chunks
        await Task.Run(() =>
        {
            for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
            {
                SpatialCoordinate thisPosition = everyCoordinate[positionIndex];
                Vector2Int thisChunkCoordinate = GetChunkCoordinate(thisPosition.BasedOnPosition);
                chunkCoordinateToChunk[thisChunkCoordinate].CoordinatesInChunk.Add(thisPosition);
            }
        });

        List<MapChunk> chunks = new List<MapChunk>(chunkCoordinateToChunk.Values);

        // Now that we have a lookup table of each of these, we can build relationships
        for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
        {
            SpatialCoordinate currentSpatialCoordinate = everyCoordinate[positionIndex];

            //// If there's an overlap directly on this circle, then it has no neighbors
            //if (Physics2D.OverlapCircle(currentSpatialCoordinate.WorldPosition, OVERLAPINITIALCHECKCIRCLERADIUS, CurrentInstance.environmentLayerMask))
            //{
            //    continue;
            //}

            float closestWall = float.MaxValue;
            // For a set of predetermined directions, project a raycast
            // We can determine what neighbors have a clear shot from this space, and what the closest wall overall is
            for (int raycastDivisionStep = 0; raycastDivisionStep < CurrentInstance.raycastDivisionSteps; raycastDivisionStep++)
            {
                Vector2 angleToCheck = CurrentInstance.raycastAnglesToCheckVector2s[raycastDivisionStep];
                RaycastHit2D[] hits = Physics2D.CircleCastAll(currentSpatialCoordinate.WorldPosition, SPHERECASTNEIGHBORCHECKRADIUS, angleToCheck, MAXIMUMNEIGHBORDISTANCE, CurrentInstance.environmentLayerMask);

                Vector2 lastPoint = currentSpatialCoordinate.WorldPosition + angleToCheck * MAXIMUMNEIGHBORDISTANCE;
                float lineDistance = MAXIMUMNEIGHBORDISTANCE;

                for (int innerHitColliderIndex = 0; innerHitColliderIndex < hits.Length; innerHitColliderIndex++)
                {
                    RaycastHit2D hit = hits[innerHitColliderIndex];

                    Collider2D thisCollider = hit.collider;

                    if (thisCollider != null)
                    {
                        float distance = hit.distance;
                        if (closestWall > distance)
                        {
                            closestWall = Mathf.Min(closestWall, distance);
                            lastPoint = currentSpatialCoordinate.WorldPosition + angleToCheck * distance;
                            lineDistance = distance;
                        }
                    }
                }

                // We now know what the farthest point we can draw from this point is, in the provided direction
                // Grab all coordinates that are on that line
                // While we're doing so, we'll track direct-line-of-sight neighbors each space can connect to
                List<SpatialCoordinate> neighborsOnLine = null;

                await Task.Run(async () =>
                {
                    neighborsOnLine = await GetPositionsWithinDistanceOfLineAsync(chunks, NEIGHBORLINETOLERANCE, currentSpatialCoordinate.WorldPosition, lastPoint, angleToCheck, lineDistance);
                });
                
                // Debug.Log($"Identified {neighborsOnLine.Count} neighbors in the {raycastAnglesToCheck[ii]} angle");

                foreach (SpatialCoordinate neighborOnLine in neighborsOnLine)
                {
                    // Have we already answered this question? If so, skip
                    if (currentSpatialCoordinate.NeighborsToDistances.ContainsKey(neighborOnLine) || ReferenceEquals(neighborOnLine, currentSpatialCoordinate))
                    {
                        continue;
                    }

                    float distanceToNeighbor = Vector2.Distance(neighborOnLine.WorldPosition, currentSpatialCoordinate.WorldPosition);
                    neighborOnLine.NeighborsToDistances.Add(currentSpatialCoordinate, distanceToNeighbor);
                    currentSpatialCoordinate.NeighborsToDistances.Add(neighborOnLine, distanceToNeighbor);
                }
            }

            // Debug.Log($"{spatialCoordinate.BasedOnPosition} was determined to have {spatialCoordinate.NeighborsToDistances.Count} neighbors. Closest wall was {closestWall}.");
            currentSpatialCoordinate.ClosestWallInWorldSpace = closestWall;
            if (closestWall > roomThreshold)
            {
                foundCoordinatesWithRoom.Add(currentSpatialCoordinate);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return default(MapBakingResult);
            }
#endif
        }

        return new MapBakingResult(everyCoordinate, foundCoordinatesWithRoom, chunks);
    }


    public static async Task<MapBakingResult> BakeNeighborsGridAsync(NativeArray<Vector2Int> positions, int positionsLength, float roomThreshold)
    {
        // First build a lookup table of every spatial coordinate
        List<SpatialCoordinate> everyCoordinate = new List<SpatialCoordinate>(positionsLength);
        List<SpatialCoordinate> foundCoordinatesWithRoom = new List<SpatialCoordinate>(positionsLength);

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        Dictionary<Vector2Int, Vector2Int[]> neighborCheckTree = BuildNeighborPossiblePositions(CurrentInstance.orthogonalNeighbors, MAXIMUMNEIGHBORDISTANCE);

        Dictionary<Vector2Int, SpatialCoordinate> coordinateAtPosition = new Dictionary<Vector2Int, SpatialCoordinate>();
        // Break the coordinates in to a grid that can contain each of them in chunks
        Dictionary<Vector2Int, MapChunk> chunkCoordinateToChunk = new Dictionary<Vector2Int, MapChunk>();

        List<MapChunk> chunks = new List<MapChunk>();

        await Task.Run(() =>
        {
            for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
            {
                Vector2Int currentCoordinate = positions[positionIndex];
                min.x = Mathf.Min(min.x, currentCoordinate.x);
                min.y = Mathf.Min(min.y, currentCoordinate.y);
                max.x = Mathf.Max(max.x, currentCoordinate.x);
                max.y = Mathf.Max(max.y, currentCoordinate.y);
            }

            for (int xx = min.x; xx < max.x + CHUNKDIMENSIONSIZE; xx += CHUNKDIMENSIONSIZE)
            {
                for (int yy = min.y; yy < max.y + CHUNKDIMENSIONSIZE; yy += CHUNKDIMENSIONSIZE)
                {
                    Vector2Int chunkCoordinate = GetChunkCoordinate(xx, yy);
                    MapChunk newChunk = new MapChunk(chunkCoordinate);
                    chunkCoordinateToChunk.Add(chunkCoordinate, newChunk);
                    chunks.Add(newChunk);
                }
            }

            // Now sort each coordinate in to chunks
            for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
            {
                Vector2Int thisChunkCoordinate = GetChunkCoordinate(positions[positionIndex]);
                MapChunk thisChunk = chunkCoordinateToChunk[thisChunkCoordinate];

                SpatialCoordinate newCoordinate = new SpatialCoordinate(positions[positionIndex], thisChunk);
                thisChunk.CoordinatesInChunk.Add(newCoordinate);

                everyCoordinate.Add(newCoordinate);
                coordinateAtPosition.Add(newCoordinate.BasedOnPosition, newCoordinate);
            }

            // Clear each chunk that has no positions in it
            for (int ii = chunks.Count - 1; ii >= 0; ii--)
            {
                if (chunks[ii].CoordinatesInChunk.Count == 0)
                {
                    chunks.RemoveAt(ii);
                }
            }
        });

        // Now that we have a lookup table of each of these, we can build relationships
        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            MapChunk thisChunk = chunks[chunkIndex];
            thisChunk.CoordinatesInChunkWithLegRoom.Clear();

            for (int positionIndex = 0; positionIndex < thisChunk.CoordinatesInChunk.Count; positionIndex++)
            {
                SpatialCoordinate currentSpatialCoordinate = thisChunk.CoordinatesInChunk[positionIndex];
                SetNeighbors(currentSpatialCoordinate, neighborCheckTree, coordinateAtPosition, out float closestWall);

                // Debug.Log($"{currentSpatialCoordinate.BasedOnPosition} was determined to have {currentSpatialCoordinate.NeighborsToDistances.Count} neighbors. Closest wall was {closestWall}.");
                currentSpatialCoordinate.ClosestWallInWorldSpace = closestWall;
                if (closestWall > roomThreshold)
                {
                    foundCoordinatesWithRoom.Add(currentSpatialCoordinate);
                    thisChunk.CoordinatesInChunkWithLegRoom.Add(currentSpatialCoordinate);
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    return default(MapBakingResult);
                }
            }
#endif
        }

        return new MapBakingResult(everyCoordinate, foundCoordinatesWithRoom, chunks);
    }

    public static void RebakeCoordinates(MapBakingResult toRebake, float distanceForRoomliness)
    {
        Dictionary<Vector2Int, SpatialCoordinate> coordinateDictionary = new Dictionary<Vector2Int, SpatialCoordinate>();
        for (int chunkIndex = 0; chunkIndex < toRebake.Chunks.Count; chunkIndex++)
        {
            MapChunk thisChunk = toRebake.Chunks[chunkIndex];
            for (int coordinateIndex = 0; coordinateIndex < thisChunk.CoordinatesInChunk.Count; coordinateIndex++)
            {
                SpatialCoordinate thisCoordinate = thisChunk.CoordinatesInChunk[coordinateIndex];
                coordinateDictionary.Add(thisCoordinate.BasedOnPosition, thisCoordinate);
            }
        }

        Dictionary<Vector2Int, Vector2Int[]> neighborCheckTree = BuildNeighborPossiblePositions(CurrentInstance.orthogonalNeighbors, MAXIMUMNEIGHBORDISTANCE);

        for (int chunkIndex = 0; chunkIndex < toRebake.Chunks.Count; chunkIndex++)
        {
            MapChunk thisChunk = toRebake.Chunks[chunkIndex];
            thisChunk.CoordinatesInChunkWithLegRoom.Clear();

            for (int coordinateIndex = 0; coordinateIndex < thisChunk.CoordinatesInChunk.Count; coordinateIndex++)
            {
                SpatialCoordinate coordinate = thisChunk.CoordinatesInChunk[coordinateIndex];
                SetNeighbors(coordinate, neighborCheckTree, coordinateDictionary, out float closestWall);
                coordinate.ClosestWallInWorldSpace = closestWall;

                if (closestWall >= distanceForRoomliness)
                {
                    thisChunk.CoordinatesInChunkWithLegRoom.Add(coordinate);
                }
            }
        }
    }

    #region Old Synchronous Methods

    [Obsolete]
    public static IReadOnlyList<SpatialCoordinate> BakeNeighbors(NativeArray<Vector2Int> positions, int positionsLength, float roomThreshold, out IReadOnlyList<SpatialCoordinate> coordinatesWithRoom)
    {
        // Generate the spatial awareness raycasts by dividing them by the step count
        CurrentInstance.raycastAnglesToCheckVector2s = new Vector2[CurrentInstance.raycastDivisionSteps];
        int raycastAnglesLength = CurrentInstance.raycastDivisionSteps;
        CurrentInstance.raycastAnglesToCheckVector2s = new Vector2[raycastAnglesLength];
        for (int ii = 0; ii < raycastAnglesLength; ii++)
        {
            float angle = Mathf.Lerp(0, 360f, (float)ii / (float)raycastAnglesLength);
            CurrentInstance.raycastAnglesToCheckVector2s[ii] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }

        // First build a lookup table of every spatial coordinate
        List<SpatialCoordinate> everyCoordinate = new List<SpatialCoordinate>(positionsLength);
        List<SpatialCoordinate> foundCoordinatesWithRoom = new List<SpatialCoordinate>(positionsLength);
        for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
        {
            Vector2Int currentCoordinate = positions[positionIndex];
            everyCoordinate.Add(new SpatialCoordinate(currentCoordinate, null));
        }

        // Now that we have a lookup table of each of these, we can build relationships
        for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
        {
            SpatialCoordinate currentSpatialCoordinate = everyCoordinate[positionIndex];

            // If there's an overlap directly on this circle, then it has no neighbors
            if (Physics2D.OverlapCircle(currentSpatialCoordinate.WorldPosition, OVERLAPINITIALCHECKCIRCLERADIUS, CurrentInstance.environmentLayerMask))
            {
                continue;
            }

            float closestWall = float.MaxValue;
            // For a set of predetermined directions, project a raycast
            // We can determine what neighbors have a clear shot from this space, and what the closest wall overall is
            for (int raycastDivisionStep = 0; raycastDivisionStep < CurrentInstance.raycastDivisionSteps; raycastDivisionStep++)
            {
                Vector2 angleToCheck = CurrentInstance.raycastAnglesToCheckVector2s[raycastDivisionStep];
                RaycastHit2D[] hits = Physics2D.CircleCastAll(currentSpatialCoordinate.WorldPosition, SPHERECASTNEIGHBORCHECKRADIUS, angleToCheck, MAXIMUMNEIGHBORDISTANCE, CurrentInstance.environmentLayerMask);

                Vector2 lastPoint = currentSpatialCoordinate.WorldPosition + angleToCheck * MAXIMUMNEIGHBORDISTANCE;
                float lineDistance = MAXIMUMNEIGHBORDISTANCE;

                for (int innerHitColliderIndex = 0; innerHitColliderIndex < hits.Length; innerHitColliderIndex++)
                {
                    RaycastHit2D hit = hits[innerHitColliderIndex];

                    Collider2D thisCollider = hit.collider;

                    if (thisCollider != null)
                    {
                        float distance = hit.distance;
                        if (closestWall > distance)
                        {
                            closestWall = Mathf.Min(closestWall, distance);
                            lastPoint = currentSpatialCoordinate.WorldPosition + angleToCheck * distance;
                            lineDistance = distance;
                        }
                    }
                }

                // We now know what the farthest point we can draw from this point is, in the provided direction
                // Grab all coordinates that are on that line
                // While we're doing so, we'll track direct-line-of-sight neighbors each space can connect to
                List<SpatialCoordinate> neighborsOnLine = GetPositionsWithinDistanceOfLine(everyCoordinate, NEIGHBORLINETOLERANCE, currentSpatialCoordinate.WorldPosition, lastPoint, angleToCheck, lineDistance);

                // Debug.Log($"Identified {neighborsOnLine.Count} neighbors in the {raycastAnglesToCheck[ii]} angle");

                foreach (SpatialCoordinate neighborOnLine in neighborsOnLine)
                {
                    // Have we already answered this question? If so, skip
                    if (currentSpatialCoordinate.NeighborsToDistances.ContainsKey(neighborOnLine) || ReferenceEquals(neighborOnLine, currentSpatialCoordinate))
                    {
                        continue;
                    }

                    float distanceToNeighbor = Vector2.Distance(neighborOnLine.WorldPosition, currentSpatialCoordinate.WorldPosition);
                    neighborOnLine.NeighborsToDistances.Add(currentSpatialCoordinate, distanceToNeighbor);
                    currentSpatialCoordinate.NeighborsToDistances.Add(neighborOnLine, distanceToNeighbor);
                }
            }

            // Debug.Log($"{spatialCoordinate.BasedOnPosition} was determined to have {spatialCoordinate.NeighborsToDistances.Count} neighbors. Closest wall was {closestWall}.");
            currentSpatialCoordinate.ClosestWallInWorldSpace = closestWall;
            if (closestWall > roomThreshold)
            {
                foundCoordinatesWithRoom.Add(currentSpatialCoordinate);
            }
        }

        coordinatesWithRoom = foundCoordinatesWithRoom;
        return everyCoordinate;
    }

    [Obsolete]
    public static List<SpatialCoordinate> GetPositionsWithinDistanceOfLine(IReadOnlyList<SpatialCoordinate> coordinatesToCheck, float distancePermitted, Vector2 pointA, Vector2 pointB, Vector2 precalculatedLineDirection, float precalculatedLineLength)
    {
        List<SpatialCoordinate> spatialCoordinatesWithinDistanceOfLine = new List<SpatialCoordinate>(coordinatesToCheck.Count);

        foreach (SpatialCoordinate curCoordinate in coordinatesToCheck)
        {
            SpatialCoordinate curCoordinateHang = curCoordinate;
            if (IsPointCloseEnoughToLine(curCoordinateHang.WorldPosition, pointA, pointB, precalculatedLineDirection, distancePermitted, precalculatedLineLength))
            {
                spatialCoordinatesWithinDistanceOfLine.Add(curCoordinateHang);
            }
        }

        return spatialCoordinatesWithinDistanceOfLine.ToList();
    }


    #endregion

    public static Vector2Int GetChunkCoordinate(Vector2Int xy)
    {
        return GetChunkCoordinate(xy.x, xy.y);
    }

    public static Vector2Int GetChunkCoordinate(int x, int y)
    {
        return new Vector2Int(Mathf.FloorToInt((float)x / (float)CHUNKDIMENSIONSIZE), Mathf.FloorToInt((float)y / (float)CHUNKDIMENSIONSIZE));
    }

    public static Dictionary<Vector2Int, Vector2Int[]> BuildNeighborPossiblePositions(Vector2Int[] orthogonalNeighbors, float allowedDistance)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>() { Vector2Int.zero };
        Dictionary<Vector2Int, Vector2Int[]> neighbors = new Dictionary<Vector2Int, Vector2Int[]>();
        List<Vector2Int> nextNeighborsToCheck = new List<Vector2Int>() { Vector2Int.zero };

        while (nextNeighborsToCheck.Count > 0)
        {
            Vector2Int nextNeighbor = nextNeighborsToCheck[0];
            nextNeighborsToCheck.RemoveAt(0);

            List<Vector2Int> neighborsOfNextNeighbor = new List<Vector2Int>();
            foreach (Vector2Int orthogonal in orthogonalNeighbors)
            {
                Vector2Int nextOrthogonal = nextNeighbor + orthogonal;
                if (!visited.Contains(nextOrthogonal) && Vector2.Distance(Vector2.zero, (Vector2)(nextOrthogonal) / MapGenerator.COORDINATETOPOSITIONDIVISOR) <= allowedDistance)
                {
                    nextNeighborsToCheck.Add(nextOrthogonal);
                    visited.Add(nextOrthogonal);
                    neighborsOfNextNeighbor.Add(nextOrthogonal);
                }
            }
            neighbors.Add(nextNeighbor, neighborsOfNextNeighbor.ToArray());
        }

        return neighbors;
    }

    public static void SetNeighbors(SpatialCoordinate forCoordinate, Dictionary<Vector2Int, Vector2Int[]> neighbors, Dictionary<Vector2Int, SpatialCoordinate> allCoordinates, out float closestWall)
    {
        forCoordinate.NeighborsToDistances.Clear();
        int allCoordinatesCount = allCoordinates.Count;
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>(allCoordinatesCount);
        List<Vector2Int> frontier = new List<Vector2Int>(allCoordinatesCount) { Vector2Int.zero };
        closestWall = float.MaxValue;

        while (frontier.Count > 0)
        {
            Vector2Int currentFrontier = frontier[0];
            visited.Add(currentFrontier);
            frontier.RemoveAt(0);

            foreach (Vector2Int neighbor in neighbors[currentFrontier])
            {
                if (visited.Contains(neighbor))
                {
                    continue;
                }

                Vector2Int resultingCoordinate = currentFrontier + neighbor;

                Vector2Int combinedNeighborCoordinate = resultingCoordinate + forCoordinate.BasedOnPosition;
                float distance = Vector2.Distance(Vector2.zero, (Vector2)neighbor / MapGenerator.COORDINATETOPOSITIONDIVISOR);

                if (allCoordinates.TryGetValue(combinedNeighborCoordinate, out SpatialCoordinate foundNeighbor))
                {
                    frontier.Add(neighbor);
                    forCoordinate.NeighborsToDistances.Add(foundNeighbor, distance);
                }
                else
                {
                    closestWall = Mathf.Min(closestWall, distance);
                }
            }
        }

        forCoordinate.ClosestWallInWorldSpace = closestWall;
        // Debug.Log($"Calculation for neighbors complete, detected {forCoordinate.NeighborsToDistances.Count} neighbors");
    }

    private void OnDrawGizmosSelected()
    {
        Color colorA = Color.white;
        Color colorB = Color.red;
        double distanceForMaxColoration = 5f;

        foreach (SpatialCoordinate coordinate in this.Positions.Values)
        {
            foreach (SpatialCoordinate coordinateNeighbor in coordinate.NeighborsToDistances.Keys)
            {
                Gizmos.color = Color.Lerp(colorA, colorB, (float)(coordinate.NeighborsToDistances[coordinateNeighbor] / distanceForMaxColoration));
                Gizmos.DrawLine(coordinate.WorldPosition, coordinateNeighbor.WorldPosition);
            }
        }
    }
}
