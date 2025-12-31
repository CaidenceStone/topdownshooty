using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MapChunk
{
    public readonly Vector2Int ChunkCoordinate;
    public readonly Vector2 VisualCenter;

    public readonly List<SpatialCoordinate> CoordinatesInChunk = new List<SpatialCoordinate>();
    public readonly List<SpatialCoordinate> CoordinatesInChunkWithLegRoom = new List<SpatialCoordinate>();
    public readonly float ChunkHalfWidth;

    public MapChunk(Vector2Int chunkCoordinate)
    {
        this.ChunkCoordinate = chunkCoordinate;
        this.VisualCenter = new Vector2(
            (chunkCoordinate.x + ((float)SpatialReasoningCalculator.CHUNKDIMENSIONSIZE / 2f)) / MapGenerator.COORDINATETOPOSITIONDIVISOR,
            (chunkCoordinate.y + ((float)SpatialReasoningCalculator.CHUNKDIMENSIONSIZE / 2f)) / MapGenerator.COORDINATETOPOSITIONDIVISOR);
        this.ChunkHalfWidth = (float)(SpatialReasoningCalculator.CHUNKDIMENSIONSIZE * MapGenerator.COORDINATETOPOSITIONDIVISOR) / 2f;
    }
}
