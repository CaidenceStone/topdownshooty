using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropManager : MonoBehaviour
{
    [SerializeField]
    private List<Weapon> weaponDrops = new List<Weapon>();
    [SerializeField]
    private List<HealPickup> otherPickups = new List<HealPickup>();
    private List<IPickup> pickups { get; set; } = new List<IPickup>();

    [SerializeField]
    private float dropChance = .1f;
    [SerializeReference]
    private SingleLevelDirector singleLevelDirector;

    private void Start()
    {
        this.singleLevelDirector.OnEnemyDefeated += OnEnemyDefeated;

        foreach (Weapon curWeapon in weaponDrops)
        {
            int tickets = curWeapon.GetSpawnTickets();
            for (int ii = 0; ii < tickets; ii++)
            {
                this.pickups.Add(curWeapon);
            }
        }

        foreach (HealPickup curOther in otherPickups)
        {
            int tickets = curOther.GetSpawnTickets();
            for (int ii = 0; ii < tickets; ii++)
            {
                this.pickups.Add(curOther);
            }
        }
    }
    
    public void OnEnemyDefeated(Entity enemy)
    {
        this.ConsiderDrop(enemy.Body.position);
    }

    public void ConsiderDrop(Vector2 position)
    {
        if (weaponDrops.Count == 0 && otherPickups.Count == 0)
        {
            return;
        }

        if (Random.Range(0f, 1f) > dropChance)
        {
            return;
        }

        this.DoDrop(position);
    }

    public IPickup DoDrop(Vector2 position)
    {
        if (weaponDrops.Count == 0 && otherPickups.Count == 0)
        {
            return null;
        }

        IPickup pfToUse = this.pickups[Random.Range(0, this.pickups.Count)].InstantiateInstance(position);
        return pfToUse;
    }
}
