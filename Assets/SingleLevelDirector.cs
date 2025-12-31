using NavMeshPlus.Components;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class SingleLevelDirector : MonoBehaviour
{
    [SerializeReference]
    private EventCircle nextLevelEventCircle;
    [SerializeReference]
    private MapGenerator mapGenerator;
    [SerializeReference]
    private EventCircle startPlayCircle;
    [SerializeReference]
    private TDSCamera gameplayCamera;
    [SerializeReference]
    private TDSCharacterController characterPF;
    [SerializeReference]
    private PlayerHUDManager hudManager;

    public Vector2 PlayerStartingPosition { get; private set; }

    [SerializeField]
    private float minimumDistanceFromStartCircle = 3;
    [SerializeField]
    private float maximumDistanceFromStartCircle = 10;

    [SerializeField]
    private float minimumEnemySpawnStartDistance = 20;

    [SerializeField]
    private int minWeaponDropsToStart = 0;
    [SerializeField]
    private int maxWeaponDropsToStart = 5;
    [SerializeField]
    private float minWeaponDropStartDistance = 5f;
    [SerializeReference]
    private DropManager dropManager;


    [SerializeReference]
    private EnemySpawnSettingsProvider enemySpawnSettingsProvider;
    private EnemySpawnSettings enemySpawnSettings { get; set; } = null;

    public IReadOnlyList<TDSCharacterController> AlivePlayers
    {
        get
        {
            return alivePlayers;
        }
    }
    private List<TDSCharacterController> alivePlayers { get; set; } = new List<TDSCharacterController>();
    public IReadOnlyList<Entity> AliveOtherEntities
    {
        get
        {
            return aliveOtherEntities;
        }
    }
    private List<Entity> aliveOtherEntities { get; set; } = new List<Entity>();

    public delegate void CountUpdatedDelegate(int newCount);
    public event CountUpdatedDelegate OnEnemyCountUpdated;
    public delegate void EntityEventDelegate(Entity forEntity);
    public event EntityEventDelegate OnEnemyDefeated;

    private void Awake()
    {
        this.nextLevelEventCircle.gameObject.SetActive(false);
    }

    private async void Start()
    {
        this.enemySpawnSettings = this.enemySpawnSettingsProvider.GenerateEnemySpawnSettings();

        await Awaitable.NextFrameAsync();
        await this.mapGenerator.GenerateWorld();

        StartCoroutine(FinishStartDelayed());
    }

    /// <summary>
    /// Delay finishing the rest of the operation so that nav meshes can bake and such
    /// </summary>
    IEnumerator FinishStartDelayed()
    {
        while (!MapGenerator.MapReady)
        {
            yield return null;
        }

        yield return null;

        if (SpatialReasoningCalculator.NegativeSpaceWithLegRoom.Count == 0)
        {
            Debug.Log($"Spawning player start position with no leg room spawn points. They'll possibly get stuck in a wall!");
        }

        Vector2Int startPlayCirclePosition = MapGenerator.GetAnyRandomNegativeSpace(SpatialReasoningCalculator.NegativeSpaceWithLegRoom);
        this.startPlayCircle.transform.position = (Vector2)(startPlayCirclePosition) / MapGenerator.COORDINATETOPOSITIONDIVISOR;
        this.gameplayCamera.SnapPosition(this.startPlayCircle.transform.position);

        this.PlayerStartingPosition = MapGenerator.GetRandomNegativeSpacePointAtDistanceRangeFromPoint(startPlayCirclePosition, this.minimumDistanceFromStartCircle, this.maximumDistanceFromStartCircle, GeometricStampMapGenerationPlan.SUFFICIENTDISTANCEFROMWALLFORROOMLINESS);

        foreach (PlayerIdentity curIdentity in StaticLevelDirector.GetPlayerIdentities())
        {
            this.SpawnPlayer(curIdentity.Devices);
        }
    }

    private void OnEnable()
    {
        StaticLevelDirector.RegisterSingleLevelDirector(this);
    }

    private void OnDisable()
    {
        StaticLevelDirector.UnregisterSingleLevelDirector(this);
    }

    public void SpawnPlayer(InputDevice[] fromDevices)
    {
        TDSCharacterController newController = Instantiate(characterPF, this.transform);
        this.alivePlayers.Add(newController);

        if (StaticLevelDirector.InputDeviceIsAlreadyRegistered(fromDevices, out PlayerIdentity existingIdentity))
        {
            existingIdentity.CurrentController = newController;
            newController.OwnWeaponCollection.InitializeWeaponCollection(newController, existingIdentity.WeaponData);
        }
        else
        {
            PlayerIdentity newIdentity = new PlayerIdentity(fromDevices);
            StaticLevelDirector.RegisterInputPlayer(fromDevices, newController);
            newController.OwnWeaponCollection.InitializeWeaponCollection(newController);
            newIdentity.WeaponData = newController.OwnWeaponCollection.Data;
        }

        if (SpatialReasoningCalculator.NegativeSpaceWithLegRoom.Count == 0)
        {
            Debug.Log($"Spawning player wiuth no leg room spawn points. They'll possibly get stuck in a wall!");
        }

        this.PlayerStartingPosition = MapGenerator.GetRandomNegativeSpacePointAtDistanceRangeFromPoint
            ((Vector2)this.PlayerStartingPosition, this.minimumDistanceFromStartCircle, this.maximumDistanceFromStartCircle, GeometricStampMapGenerationPlan.SUFFICIENTDISTANCEFROMWALLFORROOMLINESS);
        newController.Body.position = (Vector2)this.PlayerStartingPosition / MapGenerator.COORDINATETOPOSITIONDIVISOR;

        newController.SetDevices(fromDevices);
        this.hudManager.TryRegisterCanvas(newController, out _);
    }

    public void Begin()
    {
        HashSet<Vector2> currentCharacterPositions = StaticLevelDirector.CurrentLevelDirector.GetCharacterPositions();

        Debug.Log($"Spawning {this.enemySpawnSettings.EnemyCountToSpawn} enemies");
        for (int ii = 0; ii < this.enemySpawnSettings.EnemyCountToSpawn; ii++)
        {
            if (!this.enemySpawnSettings.TryGetEntityAndTakeTicket(out Entity entityPF))
            {
                Debug.Log($"No more spawn tickets are active");
                break;
            }
            Vector2 positionToSpawn = MapGenerator.GetRandomNegativeSpacePointAtDistanceRangeFromPoints(SpatialReasoningCalculator.CurrentInstance.Chunks, currentCharacterPositions, this.minimumEnemySpawnStartDistance, float.MaxValue, GeometricStampMapGenerationPlan.SUFFICIENTDISTANCEFROMWALLFORROOMLINESS);
            Entity newEntity = Instantiate(entityPF, this.transform);
            newEntity.transform.position = positionToSpawn;
        }

        int weaponDropCount = Random.Range(this.minWeaponDropsToStart, this.maxWeaponDropsToStart);
        Debug.Log($"Spawning {weaponDropCount} initial weapon drops");
        for (int ii = 0; ii < weaponDropCount; ii++)
        {
            Vector2 positionToSpawn = MapGenerator.GetRandomNegativeSpacePointAtDistanceRangeFromPoints(SpatialReasoningCalculator.CurrentInstance.Chunks, currentCharacterPositions, this.minimumDistanceFromStartCircle, float.MaxValue, GeometricStampMapGenerationPlan.SUFFICIENTDISTANCEFROMWALLFORROOMLINESS);
            this.dropManager.DoDropWeapon(positionToSpawn);
        }

        StaticLevelDirector.BeginLevel();
    }

    public HashSet<Vector2> GetCharacterPositions()
    {
        HashSet<Vector2> positions = new HashSet<Vector2>();

        foreach (TDSCharacterController character in AlivePlayers)
        {
            positions.Add(character.LastStoodVector2);
        }

        return positions;
    }

    public void RegisterEntity(Entity toRegister)
    {
        if (toRegister is TDSCharacterController)
        {
            return;
        }

        this.aliveOtherEntities.Add(toRegister);
        int newCount = this.aliveOtherEntities.Count;
        this.OnEnemyCountUpdated?.Invoke(newCount);
    }

    public void UnregisterEntity(Entity toUnregister)
    {
        this.aliveOtherEntities.Remove(toUnregister);

        int newCount = this.aliveOtherEntities.Count;

        this.OnEnemyCountUpdated?.Invoke(newCount);
        this.OnEnemyDefeated?.Invoke(toUnregister);

        if (newCount == 0)
        {
            Debug.Log($"Zero entities remaining, consider presenting next level");
            this.PresentNextLevel();
        }
    }

    public void PresentNextLevel()
    {
        if (this.alivePlayers.Count == 0)
        {
            return;
        }

        Debug.Log($"Spawn the next level portal!");

        Vector2 playerPosition = this.alivePlayers[UnityEngine.Random.Range(0, this.alivePlayers.Count)].Body.position;
        Vector2 warpPosition = MapGenerator.GetRandomNegativeSpacePointAtDistanceRangeFromPoint(new Vector2Int
            (
                Mathf.FloorToInt(playerPosition.x * MapGenerator.COORDINATETOPOSITIONDIVISOR), 
                Mathf.FloorToInt(playerPosition.y * MapGenerator.COORDINATETOPOSITIONDIVISOR)
            ),
            this.minimumDistanceFromStartCircle,
            this.maximumDistanceFromStartCircle, GeometricStampMapGenerationPlan.SUFFICIENTDISTANCEFROMWALLFORROOMLINESS);
        this.nextLevelEventCircle.transform.position = warpPosition;
        this.nextLevelEventCircle.gameObject.SetActive(true);
    }

    public void GoNextLevel()
    {
        StaticLevelDirector.AdvanceLevel();
    }
}
