using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

public class ProjectileShootingEnemy : Enemy
{
    [SerializeReference]
    private Weapon ownWeapon;

    [SerializeField]
    private float minimumDistanceToFire = 0;
    [SerializeField]
    private float maximumDistanceToFire = float.MaxValue;
    [SerializeField]
    private float waitTimeAfterDistanceCheck = .25f;

    protected override void Start()
    {
        base.Start();
        this.ownWeapon.InitializeWeapon(this.MyFaction);
    }

    protected override void TickDownTimers()
    {
        base.TickDownTimers();
        this.ownWeapon.TickDownTimers();
    }

    protected override void BehaviourUpdate()
    {
        base.BehaviourUpdate();

        if (this.primaryTarget == null || this.primaryTarget.ShouldDestroy)
        {
            return;
        }

        if (!this.ownWeapon.ReadyToFire)
        {
            return;
        }

        Vector2 positionDifference = this.primaryTarget.Body.position - this.Body.position;

        if (positionDifference.magnitude < minimumDistanceToFire || positionDifference.magnitude > maximumDistanceToFire)
        {
            this.ownWeapon.AddCooldown(this.waitTimeAfterDistanceCheck);
            return;
        }

        this.ownWeapon.FireInDirection((this.primaryTarget.Body.position - this.Body.position).normalized);
    }
}
