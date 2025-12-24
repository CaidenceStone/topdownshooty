using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileShootingEnemy : Enemy
{
    [SerializeReference]
    private Transform firingPoint;
    [SerializeReference]
    private Projectile projectilePF;
    [SerializeField]
    private float timeBetweenProjectiles = 1f;
    [SerializeField]
    private float randomStartSleepTimeModifierMax = 5f;

    /// <summary>
    /// Inaccuracy.
    /// When determining the firing angle, take one unit towards the enemy, then multiply a random unit circle by this value.
    /// </summary>
    [SerializeField]
    private float maximumSpread = 1f;

    private float curTimeBetweenProjectiles { get; set; } = 0;

    protected override void Start()
    {
        base.Start();

        this.curTimeBetweenProjectiles = this.timeBetweenProjectiles + Random.Range(0, randomStartSleepTimeModifierMax);
    }

    protected override void TickDownTimers()
    {
        base.TickDownTimers();

        if (this.curTimeBetweenProjectiles > 0)
        {
            this.curTimeBetweenProjectiles -= Time.deltaTime;
        }
    }

    protected override void BehaviourUpdate()
    {
        base.BehaviourUpdate();

        if (this.primaryTarget == null || this.primaryTarget.ShouldDestroy)
        {
            return;
        }

        if (this.curTimeBetweenProjectiles <= 0)
        {
            this.curTimeBetweenProjectiles = this.timeBetweenProjectiles;
            Projectile newProjectile = Instantiate(this.projectilePF);
            newProjectile.transform.position = this.firingPoint.position;

            Vector3 firingDirection = ((this.primaryTarget.Body.position - this.Body.position).normalized + Random.insideUnitCircle * this.maximumSpread).normalized;

            newProjectile.StartProjectile(firingDirection, Faction.Enemy);
        }
    }
}
