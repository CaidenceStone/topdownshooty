using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SpatialCoordinate
{
    public readonly Vector2Int BasedOnPosition;
    public readonly Vector2 WorldPosition;
    public double ClosestWallInWorldSpace { get; set; }
    public readonly Dictionary<SpatialCoordinate, double> NeighborsToDistances = new Dictionary<SpatialCoordinate, double>();

    public readonly MapChunk FromChunk;

    public SpatialCoordinate(Vector2Int basedOnPosition, MapChunk fromChunk)
    {
        this.BasedOnPosition = basedOnPosition;
        this.WorldPosition = ((Vector2)basedOnPosition) / MapGenerator.COORDINATETOPOSITIONDIVISOR;
        this.FromChunk = fromChunk;
    }

    public override int GetHashCode()
    {
        return this.BasedOnPosition.GetHashCode();
    }
}
