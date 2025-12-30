using System.Collections.Generic;
using UnityEngine;

public struct MapBakingResult
{
    public readonly IReadOnlyList<SpatialCoordinate> AllCoordinates;
    public readonly IReadOnlyList<SpatialCoordinate> CoordinatesWithLegroom;

    public MapBakingResult(IReadOnlyList<SpatialCoordinate> allCoordinates, IReadOnlyList<SpatialCoordinate> coordinatesWithLegroom)
    {
        this.AllCoordinates = allCoordinates;
        this.CoordinatesWithLegroom = coordinatesWithLegroom;
    }
}
