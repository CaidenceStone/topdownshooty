using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public static int MostLeft = int.MaxValue;
    public static int MostRight = int.MinValue;
    public static int MostTop = int.MinValue;
    public static int MostBottom = int.MaxValue;

    [SerializeReference]
    private MapGenerationPlan plan;
    [SerializeReference]
    private TDSCamera gameCamera;

    public static bool MapReady { get; private set; } = false;

    [SerializeReference]
    private StartPlayCircle startPlayCircle;

    private void Start()
    {
        MostLeft = int.MaxValue;
        MostRight = int.MinValue;
        MostTop = int.MinValue;
        MostBottom = int.MaxValue;

        GenerateWorld();
    }

    public async void GenerateWorld()
    {
        await this.plan.GenerateMapAsync();

        this.startPlayCircle.transform.position = new Vector2((MostRight + MostLeft) / 2, (MostTop + MostBottom) / 2) + Vector2.down * 5f;
        this.gameCamera.SnapPosition(new Vector2((MostRight + MostLeft) / 2, (MostTop + MostBottom) / 2));

        MapReady = true;
    }
}
