using System.Collections.Generic;
using UnityEngine;

public struct MapBakingResult
{
    public readonly List<SpatialCoordinate> AllCoordinates;
    public readonly List<SpatialCoordinate> CoordinatesWithLegroom;
    public readonly List<MapChunk> Chunks;

    public MapBakingResult(List<SpatialCoordinate> allCoordinates, List<SpatialCoordinate> coordinatesWithLegroom, List<MapChunk> chunks)
    {
        this.AllCoordinates = allCoordinates;
        this.CoordinatesWithLegroom = coordinatesWithLegroom;
        this.Chunks = chunks;
    }
}
