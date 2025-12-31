using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using UnityEditor;
using UnityEngine;

public class Path
{
    public Vector2 StartingPosition { get; private set; }
    public Vector2 Destination { get; private set; }

    public Vector2 NextWaypoint { get; private set; }
    public bool IsComplete { get; private set; } = false;
    public List<Vector2> PathPoints { get; set; } = new List<Vector2>();
    private float InitialLength { get; set; } = 0;

    public int PathPointsCount
    {
        get
        {
            return this.PathPoints.Count;
        }
    }

    public Path(Vector2 startingPosition, Vector2 destination)
    {
        this.StartingPosition = startingPosition;
        this.Destination = Destination;
        this.NextWaypoint = destination;
    }

    public Path(Vector2 startingPosition, IReadOnlyList<Vector2> spatialCoordinates)
    {
        this.StartingPosition = startingPosition;
        Vector2 previousPosition = startingPosition;
        foreach (Vector2 position in spatialCoordinates)
        {
            this.InitialLength += Vector2.Distance(position, previousPosition);
            previousPosition = position;
            this.Destination = position;
            this.PathPoints.Add(position);
        }
        this.NextWaypoint = this.PathPoints[0];
    }

    public Vector2 ApproachWaypointByDistance(Vector2 currentPosition, float byDistance, float closeEnoughToContinue)
    {
        Vector2 virtualPosition = currentPosition;

        while (byDistance > 0)
        {
            if (this.IsComplete)
            {
                return virtualPosition;
            }

            float distanceToNextWaypoint = Vector2.Distance(virtualPosition, this.NextWaypoint);

            if (distanceToNextWaypoint <= byDistance || distanceToNextWaypoint <= closeEnoughToContinue)
            {
                Vector2 pointTowardsVirtualPositionByGracePeriod = Vector2.MoveTowards(this.NextWaypoint, virtualPosition, closeEnoughToContinue);
                distanceToNextWaypoint = Vector2.Distance(virtualPosition, pointTowardsVirtualPositionByGracePeriod);
                byDistance -= distanceToNextWaypoint;
                virtualPosition = pointTowardsVirtualPositionByGracePeriod;
                this.Advance();
                continue;
            }

            return virtualPosition + (this.NextWaypoint - virtualPosition).normalized * byDistance;
        }

        return virtualPosition;
    }

    private void Advance()
    {
        if (this.PathPoints.Count <= 1)
        {
            this.IsComplete = true;
            return;
        }

        this.PathPoints.RemoveAt(0);
        this.NextWaypoint = this.PathPoints[0];
    }
}
