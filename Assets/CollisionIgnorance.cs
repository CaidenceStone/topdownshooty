using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionIgnorance
{
    public IReadOnlyCollection<Collider2D> CollidersA { get; set; }
    public IReadOnlyCollection<Collider2D> CollidersB { get; set; }
    public float SecondsLeft { get; set; }

    public CollisionIgnorance(IReadOnlyCollection<Collider2D> collidersA, IReadOnlyCollection<Collider2D> collidersB, float durationLeft)
    {
        this.CollidersA = collidersA;
        this.CollidersB = collidersB;
        this.SecondsLeft = durationLeft;
    }

    public void Apply()
    {
        foreach (Collider2D colliderA in this.CollidersA)
        {
            foreach (Collider2D colliderB in this.CollidersB)
            {
                Physics2D.IgnoreCollision(colliderA, colliderB, true);
            }
        }
    }

    public void Remove()
    {
        foreach (Collider2D colliderA in this.CollidersA)
        {
            foreach (Collider2D colliderB in this.CollidersB)
            {
                Physics2D.IgnoreCollision(colliderA, colliderB, false);
            }
        }
    }
}
