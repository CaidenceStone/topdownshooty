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

    public static SpatialReasoningCalculator CurrentInstance { get; private set; }
    public static List<SpatialCoordinate> NegativeSpaceWithLegRoom { get; private set; } = new List<SpatialCoordinate>();

    private bool baked { get; set; } = false;
    public Dictionary<Vector2Int, SpatialCoordinate> Positions { get; set; } = new Dictionary<Vector2Int, SpatialCoordinate>();

    [SerializeField]
    private LayerMask environmentLayerMask;
    [SerializeField]
    private int raycastDivisionSteps = 4 * 4 * 4;
    private Vector2[] raycastAnglesToCheckVector2s { get; set; } = new Vector2[] { };
    [SerializeReference]
    public Transform MapGeneratorTransform;

    private static List<SpatialCoordinate> spatialCoordinatesReusableList { get; set; } = new List<SpatialCoordinate>();

    public Path GetPath(Vector2 startingPosition, Vector2 goal)
    {
        Vector2 worldspacedStartingCoordinate = startingPosition * MapGenerator.COORDINATETOPOSITIONDIVISOR;
        Vector2Int startingCoordinate = new Vector2Int(Mathf.RoundToInt(worldspacedStartingCoordinate.x), Mathf.RoundToInt(worldspacedStartingCoordinate.y));

        if (!this.Positions.TryGetValue(startingCoordinate, out SpatialCoordinate startingSpatialCoordinate))
        {
            Debug.Log($"Cannot find path from startingPosition {startingPosition} {startingCoordinate}, because that coordinate is not on the map");
            return null;
        }

        Vector2 worldspacedGoalCoordinate = goal * MapGenerator.COORDINATETOPOSITIONDIVISOR;
        Vector2Int goalCoordinate = new Vector2Int(Mathf.RoundToInt(worldspacedGoalCoordinate.x), Mathf.RoundToInt(worldspacedGoalCoordinate.y));

        if (!this.Positions.TryGetValue(goalCoordinate, out SpatialCoordinate goalSpatialCoordinate))
        {
            Debug.Log($"Cannot find path from goal {goal} {goalCoordinate}, because that coordinate is not on the map");
            return null;
        }

        HashSet<SpatialCoordinate> visited = new HashSet<SpatialCoordinate>();
        PriorityQueue<SpatialCoordinate> coordinatesToVisit = new PriorityQueue<SpatialCoordinate>();
        coordinatesToVisit.AddEntry(new PriorityQueueEntry<SpatialCoordinate>(startingSpatialCoordinate));

        while (coordinatesToVisit.TryGetHighestScoringValue(out PriorityQueueEntry<SpatialCoordinate> currentEntry))
        {
            // For each neighbor relationship this coordinate has, determine which one is the closest to the goal
            foreach (SpatialCoordinate coord in currentEntry.Value.NeighborsToDistances.Keys)
            {
                if (ReferenceEquals(coord, goalSpatialCoordinate))
                {
                    // Found our goal!
                    return new Path(startingPosition, currentEntry.AllEntriesToList());
                }

                if (visited.Contains(coord))
                {
                    continue;
                }

                float distanceToGoal = Vector2.Distance(coord.WorldPosition, goal);
                double distanceTowardsNode = currentEntry.TravelCost - currentEntry.Value.NeighborsToDistances[coord];
                coordinatesToVisit.AddEntry(new PriorityQueueEntry<SpatialCoordinate>(coord, -distanceToGoal + currentEntry.TravelCost, currentEntry.TravelCost - distanceTowardsNode, currentEntry));
                visited.Add(coord);
            }
        }

        Debug.Log($"I couldn't find a path from {startingPosition} to {goal}!");
        return null;
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

    public static float GetClosestDistanceFromLineSegment(Vector2 positionToCheck, Vector2 linePointA, Vector2 linePointB, Vector2 precalculatedTrustedLineDirection, float precalculatedLineLength)
    {
        Vector2 differenceFromLine = positionToCheck - linePointA;
        float t = Mathf.Clamp(Vector2.Dot(differenceFromLine, precalculatedTrustedLineDirection), 0, precalculatedLineLength);
        return Vector2.Distance(positionToCheck, linePointA + precalculatedTrustedLineDirection * t);
    }

    public static IEnumerable<SpatialCoordinate> FindLargestIsland(IReadOnlyList<SpatialCoordinate> negativeSpaceList, out List<SpatialCoordinate> wallsToRefill, out int largestIslandSize)
    {
        if (negativeSpaceList.Count == 0)
        {
            Debug.Log($"The negative space list is empty, nothing to refill.");
            wallsToRefill = new List<SpatialCoordinate>();
            largestIslandSize = 0;
            return new List<SpatialCoordinate>();
        }

        Debug.Log($"Asked to parse {negativeSpaceList.Count} negative spaces into islands");

        wallsToRefill = new List<SpatialCoordinate>(negativeSpaceList.Count);
        List<SpatialCoordinate[]> islands = new List<SpatialCoordinate[]>();
        List<int> islandSizes = new List<int>();

        HashSet<SpatialCoordinate> remainingCoordinatesToCheck = new HashSet<SpatialCoordinate>(negativeSpaceList);
        SpatialCoordinate placeToStart = negativeSpaceList[0];
        while (remainingCoordinatesToCheck.Count > 0)
        {
            placeToStart = remainingCoordinatesToCheck.First();
            remainingCoordinatesToCheck.Remove(placeToStart);

            int currentCoordinateIndex = 1;
            SpatialCoordinate[] thisIslandCoordinates = new SpatialCoordinate[negativeSpaceList.Count];
            thisIslandCoordinates[0] = placeToStart;

            Queue<SpatialCoordinate> nextCheckList = new Queue<SpatialCoordinate>(negativeSpaceList.Count);
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

            Debug.Log($"Island identified with {currentCoordinateIndex} coordinates");

            // If we've reached here, we couldn't find any neighbors-of-neighbors
            islandSizes.Add(currentCoordinateIndex);
            islands.Add(thisIslandCoordinates);
        }

        if (islands.Count == 0)
        {
            Debug.Log($"Couldn't find any islands in the negative space of {negativeSpaceList.Count}");
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

    public static List<SpatialCoordinate> LimitTo(IReadOnlyList<SpatialCoordinate> coordinatesToLimit, HashSet<SpatialCoordinate> limitingSubset, HashSet<SpatialCoordinate> inCoordinatesWithRoom, out IReadOnlyList<SpatialCoordinate> outCoordinatesWithRoom)
    {
        List<SpatialCoordinate> limitedTo = new List<SpatialCoordinate>(limitingSubset.Count);
        List<SpatialCoordinate> putOutCoordinatesWithRoom = new List<SpatialCoordinate>(limitingSubset.Count);
        int positionsToConsiderLength = coordinatesToLimit.Count;
        for (int ii = 0; ii < positionsToConsiderLength; ii++)
        {
            SpatialCoordinate coordinate = coordinatesToLimit[ii];
            if (!limitingSubset.Contains(coordinate))
            {
                foreach (SpatialCoordinate neighboringCoordinate in coordinate.NeighborsToDistances.Keys)
                {
                    neighboringCoordinate.NeighborsToDistances.Remove(coordinate);
                }
                coordinate.NeighborsToDistances.Clear();
            }
            else
            {
                limitedTo.Add(coordinate);

                if (inCoordinatesWithRoom.Contains(coordinate))
                {
                    putOutCoordinatesWithRoom.Add(coordinate);
                }
            }
        }
        outCoordinatesWithRoom = putOutCoordinatesWithRoom;
        return limitedTo;
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

        await Task.Run(() =>
        {
            for (int positionIndex = 0; positionIndex < positionsLength; positionIndex++)
            {
                Vector2Int currentCoordinate = positions[positionIndex];
                SpatialCoordinate newCoordinate = new SpatialCoordinate(currentCoordinate);
                everyCoordinate.Add(newCoordinate);
                everyCoordinateVector2[positionIndex] = newCoordinate.WorldPosition;
            }
        });

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
                List<SpatialCoordinate> neighborsOnLine = null;

                await Task.Run(async () =>
                {
                    neighborsOnLine = await GetPositionsWithinDistanceOfLineAsync(everyCoordinate, everyCoordinateVector2, NEIGHBORLINETOLERANCE, currentSpatialCoordinate.WorldPosition, lastPoint, angleToCheck, lineDistance);
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

        return new MapBakingResult(everyCoordinate, foundCoordinatesWithRoom);
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
            everyCoordinate.Add(new SpatialCoordinate(currentCoordinate));
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
