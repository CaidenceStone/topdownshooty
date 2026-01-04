using UnityEngine;

public class Mob : MonoBehaviour
{
    public const int MAXIMUMSLIDEITERATIONS = 10;
    public const float PLANCKCOLLISIONDISTANCE = .01f;

    [SerializeField]
    public Faction MyFaction;

    [SerializeReference]
    private Rigidbody2D body;

    public Rigidbody2D Body
    {
        get
        {
            return this.body;
        }
    }

    [SerializeField]
    protected float bounciness = 0;
    [SerializeField]
    protected LayerMask environmentMask;

    public Vector2 VelocityPerSecond { get; private set; } = Vector2.zero;
    public bool ScheduledForDestruction { get; private set; } = false;

    [SerializeReference]
    protected Transform rotationPoint;

    [SerializeField]
    protected float rotationalDegreesPerSecond = 0;
    protected float currentRotationModifier { get; set; } = 0;

    public void SetVelocity(Vector2 amount)
    {
        this.VelocityPerSecond = amount;
    }

    public void AddForceToVelocity(Vector2 forcePerSecond)
    {
        this.VelocityPerSecond += forcePerSecond * Time.fixedDeltaTime;
    }

    public void AddForceToVelocity(Vector2 forcePerSecond, float seconds)
    {
        this.VelocityPerSecond += forcePerSecond * seconds;
    }

    public void InfluenceVelocityTowards(Vector2 newTarget, float influencePerSecond)
    {
        this.InfluenceVelocityTowards(newTarget, influencePerSecond, Time.fixedDeltaTime);
    }

    public void InfluenceVelocityTowards(Vector2 newTarget, float influencePerSeconds, float seconds)
    {
        this.VelocityPerSecond = Vector3.MoveTowards(this.VelocityPerSecond, newTarget, influencePerSeconds * seconds);
    }

    protected virtual void FixedUpdate()
    {
        if (this.ScheduledForDestruction)
        {
            this.HandleDestroy();
            return;
        }

        this.Slide(this.VelocityPerSecond.normalized, this.VelocityPerSecond.magnitude * Time.fixedDeltaTime);
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
                    remainingDistance = Mathf.LerpUnclamped(0, innerDistance - hits[0].distance, this.bounciness);
                    newDirection = hits[0].normal.normalized;
                    this.body.position = hits[0].point + innerDirection * Mathf.MoveTowards(hits[0].distance, 0, Entity.PLANCKCOLLISIONDISTANCE);

                    float angleToLook = Vector2.SignedAngle(newDirection, Vector2.up);
                    this.body.rotation = -angleToLook + this.currentRotationModifier;

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

                this.body.position += innerDirection * innerDistance;
                newDirection = innerDirection;
                remainingDistance = innerDistance;

                float angleToLook = Vector2.SignedAngle(newDirection, Vector2.up);
                this.body.rotation = -angleToLook + this.currentRotationModifier;

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

    public virtual void ScheduleForDestruction()
    {
        this.ScheduledForDestruction = true;
    }

    public virtual void HandleDestroy()
    {
        Destroy(this.gameObject);
    }
}
