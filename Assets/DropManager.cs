using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropManager : MonoBehaviour
{
    [SerializeField]
    private List<Weapon> weapons = new List<Weapon>();
    [SerializeField]
    private float dropChance = .1f;
    [SerializeReference]
    private SingleLevelDirector singleLevelDirector;

    private void Start()
    {
        this.singleLevelDirector.OnEnemyDefeated += OnEnemyDefeated;
    }
    
    public void OnEnemyDefeated(Entity enemy)
    {
        this.ConsiderDrop(enemy.Body.position);
    }

    public void ConsiderDrop(Vector2 position)
    {
        if (weapons.Count == 0)
        {
            return;
        }

        if (Random.Range(0f, 1f) > dropChance)
        {
            return;
        }

        this.DoDropWeapon(position);
    }

    public void DoDropWeapon(Vector2 position)
    {
        if (weapons.Count == 0)
        {
            return;
        }

        Weapon pfToUse = this.weapons[Random.Range(0, this.weapons.Count)];
        Weapon newWeapon = Instantiate(pfToUse, position, Quaternion.Euler(0, 0, Random.Range(0, 360f)));
        newWeapon.SetIsInWorld(true);
    }
}
