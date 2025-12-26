using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    const float MAXIMUMPROJECTILEVELOCITY = 100f;

    [SerializeReference]
    private Rigidbody2D body;
    [SerializeReference]
    private Transform rotationPoint;


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

    /// <summary>
    /// When hitting an environment wall, reflect off the wall with this percentage of its movement speed.
    /// </summary>
    [SerializeField]
    private float bounciness = 0;

    public Faction MyFaction { get; private set; }

    public Vector2 FiringAngle { get; private set; }

    private bool shouldDestroy { get; set; } = false;

    [SerializeField]
    private LayerMask environmentMask;

    [SerializeField]
    private float lifeTimeMax = 2f;
    private float curLifeTimeRemaining { get; set; }
    [SerializeField]
    private AnimationCurve speedLossOverLifetime;

    public void StartProjectile(Vector2 startFiringAngle, Faction ofFaction, float speed)
    {
        this.FiringAngle = startFiringAngle;
        float angleToLook = Vector2.SignedAngle(startFiringAngle, Vector2.up);
        this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
        this.MyFaction = ofFaction;
        this.curLifeTimeRemaining = this.lifeTimeMax;
        this.UpdateMovementSpeed(speed);
    }

    protected virtual void FixedUpdate()
    {
        this.curLifeTimeRemaining -= Time.deltaTime;
        if (this.curLifeTimeRemaining <= 0)
        {
            SetShouldDestroy();
        }

        if (this.shouldDestroy)
        {
            Destroy(this.gameObject);
            return;
        }

        this.Slide(this.FiringAngle.normalized, this.movementSpeed * Time.deltaTime);
        this.UpdateMovementSpeed(Mathf.Lerp(this.movementSpeed, 0, this.speedLossOverLifetime.Evaluate(this.curLifeTimeRemaining / this.lifeTimeMax)));
    }

    private void Slide(Vector2 direction, float distance)
    {
        Collider2D previousIgnoredCollider = null;
        RaycastHit2D[] hits = new RaycastHit2D[1];
        bool SlideIteration(Vector2 innerDirection, float innerDistance, out Vector2 newDirection, out float remainingDistance)
        {
            if (this.body.Cast(innerDirection, new ContactFilter2D() { layerMask = this.environmentMask, useLayerMask = true }, hits, innerDistance) > 0)
            {
                if (this.bounciness <= 0)
                {
                    // If this has no bounciness, then just show up at the wall. We'll collide with it and end
                    this.FiringAngle = innerDirection;
                    this.body.position = hits[0].point;
                    newDirection = innerDirection;
                    remainingDistance = 0;

                    return false;
                }
                else
                {
                    // Ignore this collider for future checks, especially so that this bullet doesn't cease to exist immediately
                    Collider2D[] myColliders = new Collider2D[this.body.attachedColliderCount];
                    int foundColliders = this.body.GetAttachedColliders(myColliders);

                    if (!ReferenceEquals(previousIgnoredCollider, null))
                    {
                        foreach (Collider2D curCollider in myColliders)
                        {
                            Physics2D.IgnoreCollision(curCollider, previousIgnoredCollider, false);
                        }
                    }

                    previousIgnoredCollider = hits[0].collider;
                    foreach (Collider2D curCollider in myColliders)
                    {
                        Physics2D.IgnoreCollision(curCollider, hits[0].collider, true);
                    }

                    // Cut movement speed by bounciness value
                    this.UpdateMovementSpeed(Mathf.LerpUnclamped(0, this.movementSpeed, this.bounciness));

                    this.FiringAngle = hits[0].normal;
                    this.body.position += innerDirection * Mathf.MoveTowards(hits[0].distance, 0, Entity.PLANCKCOLLISIONDISTANCE);
                    newDirection = this.FiringAngle;
                    remainingDistance = Mathf.MoveTowards(innerDistance, 0, hits[0].distance - Entity.PLANCKCOLLISIONDISTANCE);

                    float angleToLook = Vector2.SignedAngle(newDirection, Vector2.up);
                    this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);

                    return true;
                }
            }
            else
            {
                if (this.body.position.magnitude > 1000)
                {
                    Debug.LogError($"?????");
                    remainingDistance = 0;
                    newDirection = innerDirection;
                    return false;
                }

                this.FiringAngle = innerDirection;
                this.body.position += FiringAngle * innerDistance;
                newDirection = innerDirection;
                remainingDistance = innerDistance;

                float angleToLook = Vector2.SignedAngle(newDirection, Vector2.up);
                this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);

                return false;
            }
        }

        if (distance < Entity.PLANCKCOLLISIONDISTANCE)
        {
            return;
        }

        Vector2 incomingDirection = direction;
        float incomingDistance = distance;
        for (int ii = 0; ii < Entity.MAXIMUMSLIDEITERATIONS; ii++)
        {
            direction = incomingDirection;
            distance = incomingDistance;
            if (!SlideIteration(direction, distance, out incomingDirection, out incomingDistance))
            {
                break;
            }
        }
    }

    public void SetShouldDestroy()
    {
        this.shouldDestroy = true;
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if ((this.environmentMask & (1 << collision.gameObject.layer)) != 0)
        {
            this.SetShouldDestroy();
            return;
        }
    }

    public void UpdateMovementSpeed(float newSpeed)
    {
        this.movementSpeed = Mathf.Clamp(newSpeed, 0, MAXIMUMPROJECTILEVELOCITY);
    }
}
