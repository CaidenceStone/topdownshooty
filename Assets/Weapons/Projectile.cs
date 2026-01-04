using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Projectile : Mob
{
    const float MAXIMUMPROJECTILEVELOCITY = 100f;

    private float movementSpeed { get; set; }

    [SerializeField]
    public double Damage = 1.0;
    [SerializeField]
    public float Impact = 5f;
    [SerializeField]
    public float ImpactTime = 1f;
    [SerializeField]
    public AnimationCurve ImpactOverTime;
    [SerializeField]
    public float HitStunTime = .05f;

    [SerializeField]
    private float lifeTimeMax = 2f;
    private float curLifeTimeRemaining { get; set; }
    [SerializeField]
    private AnimationCurve speedLossOverLifetime;


    [SerializeField]
    public bool DiesOnImpact = true;

    public virtual void StartProjectile(Vector2 startFiringAngle, Faction ofFaction, float speed)
    {
        this.SetVelocity(startFiringAngle);
        float angleToLook = Vector2.SignedAngle(startFiringAngle, Vector2.up);
        this.MyFaction = ofFaction;
        this.curLifeTimeRemaining = this.lifeTimeMax;
        this.UpdateMovementSpeed(speed);
        this.currentRotationModifier = 0;
        this.Body.rotation = -angleToLook + this.currentRotationModifier;
    }

    protected override void FixedUpdate()
    {
        this.curLifeTimeRemaining -= Time.deltaTime;
        if (this.curLifeTimeRemaining <= 0)
        {
           ScheduleForDestruction();
            return;
        }

        base.FixedUpdate();

        if (!this.ScheduledForDestruction)
        {
            return;
        }

        this.UpdateMovementSpeed(Mathf.Lerp(this.movementSpeed, 0, this.speedLossOverLifetime.Evaluate(this.curLifeTimeRemaining / this.lifeTimeMax)));
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if ((this.environmentMask & (1 << collision.gameObject.layer)) != 0)
        {
            this.ScheduleForDestruction();
            return;
        }
    }

    public void UpdateMovementSpeed(float newSpeed)
    {
        this.movementSpeed = Mathf.Clamp(newSpeed, 0, MAXIMUMPROJECTILEVELOCITY);
    }
}
