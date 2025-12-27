using NUnit.Framework.Internal;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    const float MAXIMUMNEIGHBORDISTANCE = 5f;

    /// <summary>
    /// When determining if a coordinate is close enough to a line, it needs to be no farther than
    /// this much worldspace units away to count.
    /// Higher numbers result in larger, fuzzier areas.
    /// Lower numbers result in faster computation time, but risks false negatives.
    /// </summary>
    const float NEIGHBORLINETOLERANCE = .5f;
    public static SpatialReasoningCalculator CurrentInstance { get; private set; }
    public static List<SpatialCoordinate> NegativeSpaceWithLegRoom { get; private set; } = new List<SpatialCoordinate>();

    private bool baked { get; set; } = false;
    private Dictionary<Vector2Int, SpatialCoordinate> positions { get; set; } = new Dictionary<Vector2Int, SpatialCoordinate>();

    [SerializeField]
    private LayerMask environmentLayerMask;
    [SerializeField]
    private float[] raycastAnglesToCheck = new float[] { 0f, 90f, 180f, 270f };
    private Vector2[] raycastAnglesToCheckVector2s { get; set; } = new Vector2[] { };

    private static List<SpatialCoordinate> spatialCoordinatesReusableList { get; set; } = new List<SpatialCoordinate>();

    public Path GetPath(Vector2 startingPosition, Vector2 goal)
    {
        Vector2 worldspacedStartingCoordinate = startingPosition * MapGenerator.COORDINATETOPOSITIONDIVISOR;
        Vector2Int startingCoordinate = new Vector2Int(Mathf.RoundToInt(worldspacedStartingCoordinate.x), Mathf.RoundToInt(worldspacedStartingCoordinate.y));

        if (!this.positions.TryGetValue(startingCoordinate, out SpatialCoordinate startingSpatialCoordinate))
        {
            Debug.Log($"Cannot find path from startingPosition {startingPosition} {startingCoordinate}, because that coordinate is not on the map");
            return null;
        }

        Vector2 worldspacedGoalCoordinate = goal * MapGenerator.COORDINATETOPOSITIONDIVISOR;
        Vector2Int goalCoordinate = new Vector2Int(Mathf.RoundToInt(worldspacedGoalCoordinate.x), Mathf.RoundToInt(worldspacedGoalCoordinate.y));

        if (!this.positions.TryGetValue(goalCoordinate, out SpatialCoordinate goalSpatialCoordinate))
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
                coordinatesToVisit.AddEntry(new PriorityQueueEntry<SpatialCoordinate>(coord, -distanceToGoal + currentEntry.TravelCost, currentEntry.TravelCost - currentEntry.Value.NeighborsToDistances[coord], currentEntry));
                visited.Add(coord);
            }
        }

        Debug.Log($"I couldn't find a path from {startingPosition} to {goal}!");
        return null;
    }

    public async Task<IReadOnlyList<SpatialCoordinate>> BakeWithAsync(IReadOnlyCollection<Vector2Int> negativeSpace)
    {
        Debug.Log($"Baking with {negativeSpace.Count} negative spaces");

        // Intentional null check in case this is a new scene
        if (CurrentInstance == null)
        {
            CurrentInstance = GameObject.FindAnyObjectByType<SpatialReasoningCalculator>();

            if (CurrentInstance == null)
            {
                Debug.Log($"Could not find a SpatialReasoningCalculator in the scene");
            }
        }

        this.baked = true;
        NegativeSpaceWithLegRoom.Clear();

        this.positions.Clear();
        this.positions.EnsureCapacity(negativeSpace.Count);

        int raycastAnglesLength = this.raycastAnglesToCheck.Length;
        this.raycastAnglesToCheckVector2s = new Vector2[raycastAnglesLength];
        for (int ii = 0; ii < raycastAnglesLength; ii++)
        {
            raycastAnglesToCheckVector2s[ii] = new Vector2(Mathf.Cos(raycastAnglesToCheck[ii]), Mathf.Sin(raycastAnglesToCheck[ii]));
        }

        // Build a lookup table of positions to saturated spatial coordinates
        // Go through each position and create a spot in memory for it
        this.positions = new Dictionary<Vector2Int, SpatialCoordinate>();

        foreach (Vector2Int position in negativeSpace)
        {
            this.positions.Add(position, new SpatialCoordinate(position));
        }

        Debug.Log($"{positions.Count} positions have been generated for the initial batch");

        // Now that we have a lookup table of each of these, we can build relationships
        foreach (SpatialCoordinate spatialCoordinate in this.positions.Values)
        {
            float closestWall = float.MaxValue;
            // For a set of predetermined directions, project a raycast
            // We can determine what neighbors have a clear shot from this space, and what the closest wall overall is
            for (int ii = 0; ii < raycastAnglesLength; ii++)
            {
                Vector2 angleToCheck = raycastAnglesToCheckVector2s[ii];
                RaycastHit2D hit = Physics2D.Raycast(spatialCoordinate.WorldPosition, angleToCheck, MAXIMUMNEIGHBORDISTANCE, environmentLayerMask);

                Vector2 lastPoint;

                if (hit.collider != null)
                {
                    closestWall = Mathf.Min(closestWall, hit.distance);
                    lastPoint = hit.point;
                }
                else
                {
                    lastPoint = spatialCoordinate.WorldPosition + angleToCheck * MAXIMUMNEIGHBORDISTANCE;
                }

                // We now know what the farthest point we can draw from this point is, in the provided direction
                // Grab all coordinates that are on that line
                // While we're doing so, we'll track direct-line-of-sight neighbors each space can connect to
                List<SpatialCoordinate> neighborsOnLine = await GetPositionsWithinDistanceOfLineAsync(NEIGHBORLINETOLERANCE, spatialCoordinate.WorldPosition, lastPoint, angleToCheck);

                // Debug.Log($"Identified {neighborsOnLine.Count} neighbors in the {raycastAnglesToCheck[ii]} angle");

                foreach (SpatialCoordinate neighborOnLine in neighborsOnLine)
                {
                    // Have we already answered this question? If so, skip
                    if (spatialCoordinate.NeighborsToDistances.ContainsKey(neighborOnLine) || ReferenceEquals(neighborOnLine, spatialCoordinate))
                    {
                        continue;
                    }

                    float distanceToNeighbor = Vector2.Distance(neighborOnLine.WorldPosition, spatialCoordinate.WorldPosition);
                    neighborOnLine.NeighborsToDistances.Add(spatialCoordinate, distanceToNeighbor);
                    spatialCoordinate.NeighborsToDistances.Add(neighborOnLine, distanceToNeighbor);
                }

                spatialCoordinate.ClosestWallInWorldSpace = closestWall;
            }

            // Debug.Log($"{spatialCoordinate.BasedOnPosition} was determined to have {spatialCoordinate.NeighborsToDistances.Count} neighbors");
            if (closestWall > 2f)
            {
                NegativeSpaceWithLegRoom.Add(spatialCoordinate);
            }
        }

        return new List<SpatialCoordinate>(this.positions.Values);
    }

    private void OnEnable()
    {
        CurrentInstance = this;
    }

    private void OnDisable()
    {
        CurrentInstance = null;
    }

    public static async Task<List<SpatialCoordinate>> GetPositionsWithinDistanceOfLineAsync(float distancePermitted, Vector2 pointA, Vector2 pointB, Vector2 precalculatedLineDirection)
    {
        ConcurrentBag<SpatialCoordinate> spatialCoordinatesWithinDistanceOfLine = new ConcurrentBag<SpatialCoordinate>();

        int negativeSpaceWithLegRoomCount = NegativeSpaceWithLegRoom.Count;
        List<Task> tasksToWaitFor = new List<Task>(negativeSpaceWithLegRoomCount);
        IReadOnlyList<SpatialCoordinate> negativeSpace = NegativeSpaceWithLegRoom;

        if (negativeSpaceWithLegRoomCount == 0)
        {
            negativeSpace = MapGenerator.NegativeSpace;
        }

        foreach (SpatialCoordinate curCoordinate in negativeSpace)
        {
            SpatialCoordinate curCoordinateHang = curCoordinate;
            if (IsClosestDistanceToLineWithinThreshold(curCoordinateHang.WorldPosition, pointA, pointB, precalculatedLineDirection, distancePermitted))
            {
                spatialCoordinatesWithinDistanceOfLine.Add(curCoordinateHang);
            }
        }

        // Debug.Log($"{spatialCoordinatesWithinDistanceOfLine.Count} detected");

        return spatialCoordinatesWithinDistanceOfLine.ToList();
    }

    public static float GetClosestDistanceFromLineSegment(Vector2 positionToCheck, Vector2 linePointA, Vector2 linePointB, Vector2? precalculatedTrustedLineDirection = null)
    {
        // note: we're just trusting that the line direction is correct
        if (!precalculatedTrustedLineDirection.HasValue)
        {
            precalculatedTrustedLineDirection = linePointB - linePointA;
        }
        Vector2 differenceFromLine = positionToCheck - linePointA;
        float t = Vector2.Dot(differenceFromLine, precalculatedTrustedLineDirection.Value);

        if (t < 0)
        {
            return Vector2.Distance(positionToCheck, linePointA);
        }

        if  (t > differenceFromLine.magnitude)
        {
            return Vector2.Distance(positionToCheck, linePointB);
        }

        return Vector2.Distance(positionToCheck, linePointA + precalculatedTrustedLineDirection.Value * t);
    }

    public static List<SpatialCoordinate> FindLargestIsland(IReadOnlyList<SpatialCoordinate> negativeSpaceList, out List<SpatialCoordinate> wallsToRefill, float minimumWallProximity)
    {
        if (negativeSpaceList.Count == 0)
        {
            Debug.Log($"The negative space list is empty, nothing to refill.");
            wallsToRefill = new List<SpatialCoordinate>();
            return new List<SpatialCoordinate>();
        }

        Debug.Log($"Asked to parse {negativeSpaceList.Count} negative spaces into islands");

        wallsToRefill = new List<SpatialCoordinate>(negativeSpaceList.Count);
        List<List<SpatialCoordinate>> islands = new List<List<SpatialCoordinate>>();

        HashSet<SpatialCoordinate> remainingCoordinatesToCheck = new HashSet<SpatialCoordinate>(negativeSpaceList);
        SpatialCoordinate placeToStart = negativeSpaceList[0];
        remainingCoordinatesToCheck.Add(placeToStart);
        while (remainingCoordinatesToCheck.Count > 0)
        {
            placeToStart = remainingCoordinatesToCheck.First();
            remainingCoordinatesToCheck.Remove(placeToStart);

            if (placeToStart.ClosestWallInWorldSpace < minimumWallProximity)
            {
                continue;
            }

            List<SpatialCoordinate> thisIslandCoordinates = new List<SpatialCoordinate>(negativeSpaceList.Count);
            List<SpatialCoordinate> nextCheckList = new List<SpatialCoordinate>(negativeSpaceList.Count);
            nextCheckList.Add(placeToStart);

            do
            {
                SpatialCoordinate thisCheck = nextCheckList[0];
                nextCheckList.RemoveAt(0);
                foreach (SpatialCoordinate neighbor in thisCheck.NeighborsToDistances.Keys)
                {
                    if (remainingCoordinatesToCheck.Contains(neighbor))
                    {
                        thisIslandCoordinates.Add(neighbor);
                        remainingCoordinatesToCheck.Remove(neighbor);
                        nextCheckList.Add(neighbor);
                    }
                }
            } while (nextCheckList.Count > 0) ;

            Debug.Log($"Island identified with {thisIslandCoordinates.Count} coordinates");

            // If we've reached here, we couldn't find any neighbors-of-neighbors
            islands.Add(thisIslandCoordinates);

            // Try to find the next coordinate to use by arbitrarily grabbing the 'first'
            while (remainingCoordinatesToCheck.Count > 0)
            {
                // Keep looking until we find valid coordinates
                SpatialCoordinate nextPositionToCheck = remainingCoordinatesToCheck.First();
                remainingCoordinatesToCheck.Remove(nextPositionToCheck);
                // Back to the outer loop

                break;
            }
        }

        if (islands.Count == 0)
        {
            Debug.Log($"Couldn't find any islands in the negative space of {negativeSpaceList.Count}");
            return null;
        }

        // Now to find the largest island
        int largestIslandIndex = 0;
        int largestIslandCount = 0;

        for (int ii = 0; ii < islands.Count; ii ++)
        {
            int islandSize = islands[ii].Count;

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
                wallsToRefill.AddRange(islands[ii]);
            }
        }

        Debug.Log($"The largest island was size {largestIslandCount}, index {largestIslandIndex}, with {wallsToRefill.Count} walls to refill");

        return islands[largestIslandIndex];
    }

    public void LimitTo(HashSet<SpatialCoordinate> limitingSubset)
    {
        List<SpatialCoordinate> limitingTo = new List<SpatialCoordinate>(this.positions.Count);

        foreach (SpatialCoordinate coordinate in new List<SpatialCoordinate>(this.positions.Values))
        {
            if (!limitingSubset.Contains(coordinate))
            {
                foreach (SpatialCoordinate neighboringCoordinate in coordinate.NeighborsToDistances.Keys)
                {
                    neighboringCoordinate.NeighborsToDistances.Remove(coordinate);
                }
                coordinate.NeighborsToDistances.Clear();
                positions.Remove(coordinate.BasedOnPosition);
            }
            else
            {
                limitingTo.Add(coordinate);
            }
        }

        MapGenerator.NegativeSpace = limitingTo;
    }

    public static bool IsClosestDistanceToLineWithinThreshold(Vector2 position, Vector2 linePointA, Vector2 linePointB, Vector2 precalculatedTrustedLineDirection, float threshold)
    {
        return GetClosestDistanceFromLineSegment(position, linePointA, linePointB, precalculatedTrustedLineDirection) <= threshold;
    }
}
