using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileShootingEnemy : Enemy
{
    [SerializeReference]
    private Weapon ownWeapon;

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

        this.ownWeapon.FireInDirection((this.primaryTarget.Body.position - this.Body.position).normalized);
    }
}
