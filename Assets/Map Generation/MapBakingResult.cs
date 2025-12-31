using System.Collections.Generic;
using UnityEngine;

public struct MapBakingResult
{
    public readonly IReadOnlyList<SpatialCoordinate> AllCoordinates;
    public readonly IReadOnlyList<SpatialCoordinate> CoordinatesWithLegroom;
    public readonly IReadOnlyList<MapChunk> Chunks;

    public MapBakingResult(IReadOnlyList<SpatialCoordinate> allCoordinates, IReadOnlyList<SpatialCoordinate> coordinatesWithLegroom, IReadOnlyList<MapChunk> chunks)
    {
        this.AllCoordinates = allCoordinates;
        this.CoordinatesWithLegroom = coordinatesWithLegroom;
        this.Chunks = chunks;
    }
}
