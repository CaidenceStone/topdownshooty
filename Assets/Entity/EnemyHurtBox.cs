using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHurtBox : MonoBehaviour
{
    [SerializeField]
    public double Damage;
    [SerializeField]
    public float Impact;
    [SerializeField]
    public float ImpactDuration;
    [SerializeField]
    public AnimationCurve ImpactModifierOverTime;
    [SerializeField]
    public Collider2D[] Colliders;

    public ImpactEvent GetImpactEvent(Vector2 direction)
    {
        return new ImpactEvent(direction, this.ImpactDuration, this.Impact, this.ImpactModifierOverTime);
    }
}
