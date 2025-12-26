using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileFiringProjectile : Projectile
{
    [SerializeReference]
    private Projectile subProjectile;

    [SerializeField]
    private float subProjectileMinimumMovementSpeed = 5f;
    [SerializeField]
    private float subProjectileMaximumMovementSpeed = 10f;

    [SerializeField]
    private float timeBetweenFiring = 2f;

    private float curTimeBetweenFiring { get; set; } = 0;
    [SerializeField]
    private int subProjectilesToFire = 1;

    [SerializeField]
    private bool dieAfterFiring = false;

    private void Awake()
    {
        this.curTimeBetweenFiring = this.timeBetweenFiring;
    }

    protected override void FixedUpdate()
    {
        curTimeBetweenFiring -= Time.deltaTime;
        if (curTimeBetweenFiring < 0)
        {
            curTimeBetweenFiring += timeBetweenFiring;

            for (int ii = 0; ii < subProjectilesToFire; ii++)
            {
                Vector2 firingDirection = (Random.insideUnitCircle.normalized).normalized;
                Projectile newProjectile = Instantiate(this.subProjectile);
                newProjectile.transform.position = this.transform.position;

                float projectileSpeed = Mathf.Lerp(this.subProjectileMinimumMovementSpeed, this.subProjectileMaximumMovementSpeed, Random.Range(0f, 1f));
                newProjectile.StartProjectile(firingDirection, this.MyFaction, projectileSpeed);
            }

            if (this.dieAfterFiring)
            {
                this.SetShouldDestroy();
            }
        }

        base.FixedUpdate();
    }
}
