using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
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

    public Path(Vector2 startingPosition, IEnumerable<SpatialCoordinate> spatialCoordinates)
    {
        this.StartingPosition = startingPosition;
        Vector2 previousPosition = startingPosition;
        foreach (SpatialCoordinate position in spatialCoordinates)
        {
            this.InitialLength += Vector2.Distance(position.WorldPosition, previousPosition);
            previousPosition = position.WorldPosition;
            this.Destination = position.WorldPosition;
            this.PathPoints.Add(position.WorldPosition);
        }

        this.NextWaypoint = this.PathPoints[0];
    }

    public Vector2 ApproachWaypointByDistance(Vector2 currentPosition, float byDistance, out float remainingDistance)
    {
        if (this.IsComplete)
        {
            remainingDistance = 0;
            return currentPosition;
        }

        float distanceToNextWaypoint = Vector2.Distance(currentPosition, this.NextWaypoint);

        if (distanceToNextWaypoint <= byDistance)
        {
            remainingDistance = byDistance - distanceToNextWaypoint;
            Vector2 goTo = NextWaypoint;
            this.Advance();
            return goTo;
        }

        remainingDistance = 0;
        return currentPosition + (this.NextWaypoint - currentPosition).normalized * byDistance;
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
