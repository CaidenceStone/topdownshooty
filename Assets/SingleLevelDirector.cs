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

    [SerializeReference]
    private Enemy enemyPF;
    [SerializeField]
    private float minimumEnemySpawnStartDistance = 20;

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

    [SerializeField]
    private int EnemyCountToSpawn = 30;

    public delegate void CountUpdatedDelegate(int newCount);
    public event CountUpdatedDelegate OnEnemyCountUpdated;

    private void Awake()
    {
        this.nextLevelEventCircle.gameObject.SetActive(false);
    }

    private async void Start()
    {
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
        }
        else
        {
            PlayerIdentity newIdentity = new PlayerIdentity(fromDevices);
            StaticLevelDirector.RegisterInputPlayer(fromDevices, newController);
        }

        this.PlayerStartingPosition = MapGenerator.GetRandomNegativeSpaceNearPoint(this.PlayerStartingPosition, this.minimumDistanceFromStartCircle, this.maximumDistanceFromStartCircle);
        Vector2Int spawnPositionBasedOn = this.PlayerStartingPosition;
        newController.Body.position = this.PlayerStartingPosition;

        newController.SetDevices(fromDevices);
        this.hudManager.TryRegisterCanvas(newController, out _);
    }

    public void Begin()
    {
        StaticLevelDirector.BeginLevel();

        IReadOnlyList<Vector2> currentCharacterPositions = StaticLevelDirector.CurrentLevelDirector.GetCharacterPositions();

        for (int ii = 0; ii < this.EnemyCountToSpawn; ii++)
        {
            Vector2 positionToSpawn = MapGenerator.GetRandomNegativeSpaceAwayFromPoints(currentCharacterPositions, this.minimumEnemySpawnStartDistance, float.MaxValue);
            Enemy newEnemy = Instantiate(this.enemyPF, this.transform);
            newEnemy.transform.position = positionToSpawn;
        }
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

    public void AdvanceLevel()
    {
        StaticLevelDirector.AdvanceLevel();
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
}
