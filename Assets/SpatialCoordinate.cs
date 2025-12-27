using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SpatialCoordinate
{
    public readonly Vector2Int BasedOnPosition;
    public readonly Vector2 WorldPosition;
    public double ClosestWallInWorldSpace { get; set; }
    public readonly Dictionary<SpatialCoordinate, double> NeighborsToDistances = new Dictionary<SpatialCoordinate, double>();

    public SpatialCoordinate(Vector2Int basedOnPosition)
    {
        this.BasedOnPosition = basedOnPosition;
        this.WorldPosition = ((Vector2)basedOnPosition) / MapGenerator.COORDINATETOPOSITIONDIVISOR;
    }

    public override int GetHashCode()
    {
        return this.BasedOnPosition.GetHashCode();
    }
}
