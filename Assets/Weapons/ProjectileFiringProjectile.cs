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

    [SerializeField]
    private float minAngleForSubProjectile = 0;

    [SerializeField]
    private float maxAngleForSubProjectile = 0;

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
                float progress = this.currentRotationModifier +
                    Mathf.Lerp(this.minAngleForSubProjectile, this.maxAngleForSubProjectile, Mathf.InverseLerp(0, this.subProjectilesToFire, ii)) * Mathf.Deg2Rad;
                Vector2 baseFiringDirection = this.VelocityPerSecond.normalized;
                Vector2 firingDirectionWithDirectionality = new Vector2(
                    baseFiringDirection.x * Mathf.Cos(progress) - baseFiringDirection.y * Mathf.Sin(progress),
                    baseFiringDirection.x * Mathf.Sin(progress) + baseFiringDirection.y * Mathf.Cos(progress));

                Projectile newProjectile = Instantiate(this.subProjectile);
                newProjectile.transform.position = this.transform.position;

                float projectileSpeed = Mathf.Lerp(this.subProjectileMinimumMovementSpeed, this.subProjectileMaximumMovementSpeed, Random.Range(0f, 1f));
                newProjectile.StartProjectile(firingDirectionWithDirectionality, this.MyFaction, projectileSpeed);
                newProjectile.SetVelocity(firingDirectionWithDirectionality * projectileSpeed);

                if (this.dieAfterFiring)
                {
                    this.ScheduleForDestruction();
                }
            }
        }

        base.FixedUpdate();
    }
}
