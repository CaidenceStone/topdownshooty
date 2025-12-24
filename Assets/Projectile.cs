using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeReference]
    private Rigidbody2D body;
    [SerializeReference]
    private Transform rotationPoint;
    [SerializeField]
    private float movementSpeed = 10f;

    [SerializeField]
    public decimal Damage = 1.0M;
    [SerializeField]
    public float Impact = 5f;
    [SerializeField]
    public float ImpactTime = 1f;
    [SerializeField]
    public AnimationCurve ImpactOverTime;
    [SerializeField]
    public float HitStunTime = .05f;

    public Faction MyFaction { get; private set; }

    public Vector2 FiringAngle { get; private set; }

    private bool shouldDestroy { get; set; } = false;

    [SerializeField]
    private LayerMask environmentMask;

    [SerializeField]
    private float lifeTimeMax = 2f;
    private float curLifeTimeRemaining { get; set; }

    public void StartProjectile(Vector2 startFiringAngle, Faction ofFaction)
    {
        this.FiringAngle = startFiringAngle;
        float angleToLook = Vector2.SignedAngle(startFiringAngle, Vector2.up);
        this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
        this.MyFaction = ofFaction;
        this.curLifeTimeRemaining = this.lifeTimeMax;
    }

    private void FixedUpdate()
    {
        this.curLifeTimeRemaining -= Time.deltaTime;
        if (this.curLifeTimeRemaining <= 0)
        {
            Destroy();
        }

        if (this.shouldDestroy)
        {
            Destroy(this.gameObject);
            return;
        }

        this.body.position += FiringAngle * movementSpeed * Time.deltaTime;
    }

    public void Destroy()
    {
        this.shouldDestroy = true;
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if ((this.environmentMask & (1 << collision.gameObject.layer)) != 0)
        {
            this.Destroy();
            return;
        }
    }
}
