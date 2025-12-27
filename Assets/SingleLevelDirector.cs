using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
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

    public Vector2Int PlayerStartingPosition { get; private set; }

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
        await this.mapGenerator.GenerateWorld();

        Vector2Int startPlayCirclePosition = MapGenerator.GetAnyRandomNegativeSpace();
        this.startPlayCircle.transform.position = (Vector2)startPlayCirclePosition;
        this.gameplayCamera.SnapPosition(this.startPlayCircle.transform.position);

        this.PlayerStartingPosition = MapGenerator.GetRandomNegativeSpaceNearPoint(startPlayCirclePosition, this.minimumDistanceFromStartCircle, this.maximumDistanceFromStartCircle);
    
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

        this.PlayerStartingPosition = MapGenerator.GetRandomNegativeSpaceNearPoint(this.PlayerStartingPosition, this.minimumDistanceFromStartCircle, this.maximumDistanceFromStartCircle);
        Vector2Int spawnPositionBasedOn = this.PlayerStartingPosition;
        newController.Body.position = this.PlayerStartingPosition;

        newController.SetDevices(fromDevices);
        this.hudManager.TryRegisterCanvas(newController, out _);
    }

    public void Begin()
    {
        IReadOnlyList<Vector2> currentCharacterPositions = StaticLevelDirector.CurrentLevelDirector.GetCharacterPositions();

        Debug.Log($"Spawning {this.enemySpawnSettings.EnemyCountToSpawn} enemies");
        for (int ii = 0; ii < this.enemySpawnSettings.EnemyCountToSpawn; ii++)
        {
            if (!this.enemySpawnSettings.TryGetEntityAndTakeTicket(out Entity entityPF))
            {
                Debug.Log($"No more spawn tickets are active");
                break;
            }
            Vector2 positionToSpawn = MapGenerator.GetRandomNegativeSpaceAwayFromPoints(currentCharacterPositions, this.minimumEnemySpawnStartDistance, float.MaxValue);
            Entity newEntity = Instantiate(entityPF, this.transform);
            newEntity.transform.position = positionToSpawn;
        }

        int weaponDropCount = Random.Range(this.minWeaponDropsToStart, this.maxWeaponDropsToStart);
        Debug.Log($"Spawning {weaponDropCount} initial weapon drops");
        for (int ii = 0; ii < weaponDropCount; ii++)
        {
            Vector2 positionToSpawn = MapGenerator.GetRandomNegativeSpaceAwayFromPoints(currentCharacterPositions, this.minimumDistanceFromStartCircle, float.MaxValue);
            this.dropManager.DoDropWeapon(positionToSpawn);
        }

        StaticLevelDirector.BeginLevel();
    }

    public IReadOnlyList<Vector2> GetCharacterPositions()
    {
        List<Vector2> positions = new List<Vector2>();

        foreach (TDSCharacterController character in AlivePlayers)
        {
            positions.Add(character.Body.position);
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
        Vector2 warpPosition = MapGenerator.GetRandomNegativeSpaceNearPoint(new Vector2Int(Mathf.RoundToInt(playerPosition.x), Mathf.RoundToInt(playerPosition.y)), this.minimumDistanceFromStartCircle, this.maximumDistanceFromStartCircle);
        this.nextLevelEventCircle.transform.position = warpPosition;
        this.nextLevelEventCircle.gameObject.SetActive(true);
    }

    public void GoNextLevel()
    {
        StaticLevelDirector.AdvanceLevel();
    }
}
