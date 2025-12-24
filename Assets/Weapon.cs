using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField]
    private float timeBetweenShots = .5f;
    private float curTimeBetweenShots { get; set; } = 0f;
    [SerializeReference]
    private Projectile projectilePF;

    public Faction MyFaction { get; private set; }
    [SerializeField]
    private float randomStartSleepTimeModifierMax = 0;

    /// <summary>
    /// Inaccuracy.
    /// When determining the firing angle, take one unit towards the enemy, then multiply a random unit circle by this value.
    /// </summary>
    [SerializeField]
    private float maximumSpread = 1f;

    private void Start()
    {
        this.curTimeBetweenShots = Random.Range(this.timeBetweenShots, this.timeBetweenShots + this.randomStartSleepTimeModifierMax);
    }

    public bool ReadyToFire
    {
        get
        {
            return this.curTimeBetweenShots <= 0;
        }
    }

    public void InitializeWeapon(Faction forFaction)
    {
        this.MyFaction = forFaction;
    }

    public void FireInDirection(Vector2 towardsPosition)
    {
        if (this.curTimeBetweenShots > 0)
        {
            return;
        }

        Vector2 firingDirection = (towardsPosition.normalized + Random.insideUnitCircle * this.maximumSpread).normalized;

        this.curTimeBetweenShots = this.timeBetweenShots;
        Projectile newProjectile = Instantiate(this.projectilePF);
        newProjectile.transform.position = this.transform.position;
        newProjectile.StartProjectile(towardsPosition, this.MyFaction);
    }

    public void TickDownTimers()
    {
        if (this.curTimeBetweenShots > 0)
        {
            this.curTimeBetweenShots -= Time.deltaTime;
        }
    }
}
