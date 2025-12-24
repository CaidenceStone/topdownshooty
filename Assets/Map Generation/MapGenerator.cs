using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [SerializeReference]
    private MapGenerationPlan plan;
    public static bool MapReady { get; private set; } = false;

    private void Start()
    {
        GenerateWorld();
    }

    public async void GenerateWorld()
    {
        await this.plan.GenerateMapAsync();
        MapReady = true;
    }
}
