using UnityEngine;

public class Mob : MonoBehaviour
{
    public const int MAXIMUMSLIDEITERATIONS = 10;
    public const float PLANCKCOLLISIONDISTANCE = .01f;
    public const float MINIMUMITERATIONTRAVELDISTANCE = .02f;

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

    /// <summary>
    /// Can we split the movement sliding in to vertical and horizontal components?
    /// The downside of doing so is that collision normals can't be meaningfully determined this way, since we're moving horizontal and vertical separately
    /// This is only a fall back if we're trying to move, but can't get any distance, such as running directly diagonally in to a wall.
    /// </summary>
    protected bool canSplitMovementVectors
    {
        get
        {
            return this.bounciness <= 0;
        }
    }

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
        bool SlideIteration(Vector2 innerDirection, float innerDistance, out Vector2 newDirection, out float remainingDistance, out float distanceTraveled)
        {
            if (innerDistance <= PLANCKCOLLISIONDISTANCE)
            {
                newDirection = innerDirection;
                remainingDistance = innerDistance;
                distanceTraveled = 0;
                return false;
            }

            if (this.body.Cast(innerDirection, new ContactFilter2D() { layerMask = this.environmentMask, useLayerMask = true }, hits, innerDistance) > 0)
            {
                if (this.bounciness <= 0)
                {
                    // If this has no bounciness, then just show up at the wall. We'll collide with it and end
                    this.body.position += innerDirection * Mathf.MoveTowards(hits[0].distance, 0, Entity.PLANCKCOLLISIONDISTANCE);
                    newDirection = innerDirection;
                    remainingDistance = 0;
                    distanceTraveled = hits[0].distance;
                    return true;
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

                    // This object is bouncy! First, move to the point of contact; subtract the distance we moved, and determine how much more we should move based
                    // on the bounciness
                    this.body.position += innerDirection * Mathf.MoveTowards(hits[0].distance, 0, Entity.PLANCKCOLLISIONDISTANCE);
                    remainingDistance = Mathf.LerpUnclamped(0, innerDistance - hits[0].distance, this.bounciness);

                    // Set the new direction to the "normal" of the collision, which is the angle we should bounce from
                    // and look towards it
                    newDirection = hits[0].normal;
                    VelocityPerSecond = newDirection * Vector2.LerpUnclamped(Vector2.zero, this.VelocityPerSecond, this.bounciness);

                    float angleToLook = Vector2.SignedAngle(newDirection, Vector2.up);
                    this.rotationPoint.rotation = Quaternion.Euler(0, 0, -angleToLook + this.currentRotationModifier);
                    distanceTraveled = hits[0].distance;
                    return true;
                }
            }
            else
            {
                // We hit nothing, so just move to that spot
                this.body.position += innerDirection * innerDistance;
                newDirection = innerDirection;
                remainingDistance = 0;

                float angleToLook = Vector2.SignedAngle(newDirection, Vector2.up);
                this.rotationPoint.rotation = Quaternion.Euler(0, 0, -angleToLook + this.currentRotationModifier);

                distanceTraveled = innerDistance;
                return false;
            }
        }

        if (distance < Entity.PLANCKCOLLISIONDISTANCE)
        {
            return;
        }

        Vector2 resultingDirection = direction;
        float remainingDistance = distance;
        for (int ii = 0; ii < Entity.MAXIMUMSLIDEITERATIONS; ii++)
        {
            direction = resultingDirection;
            distance = remainingDistance;
            float traveledDistance;

            // If we're out of travel distance, we should stop
            if (Mathf.Approximately(remainingDistance, 0))
            {
                break;
            }

            if (!SlideIteration(direction, distance, out resultingDirection, out remainingDistance, out traveledDistance))
            {
                // If we traveled without hitting anything, we should stop, because we have moved our maximum distance
                // Our velocity should remain unaffected
                break;
            }

            // If we didn't travel very far at all, handle that;
            // If we are able to split movement vectors, then try a slide using vertical and horizontal separately
            // Otherwise, accept our fate
            if (traveledDistance < distance && traveledDistance < MINIMUMITERATIONTRAVELDISTANCE)
            {
                if (!this.canSplitMovementVectors)
                {
                    break;
                }

                distance -= traveledDistance;
                SlideIteration(Vector2.right * Mathf.Sign(direction.x), distance * Mathf.Cos(Mathf.Abs(direction.x)),
                    out Vector2 resultingHorizontalDirection, out float resultingHorizontalRemainingDistance, 
                    out float resultingHorizontalTraveledDistance);
                SlideIteration(Vector2.up * Mathf.Sign(direction.y), distance * Mathf.Sin(Mathf.Abs(direction.y)),
                    out Vector2 resultingVerticalDirection, out float resultingVerticalRemainingDistance,
                    out float resultingVerticalTraveledDistance);

                if (resultingHorizontalTraveledDistance + resultingVerticalTraveledDistance < MINIMUMITERATIONTRAVELDISTANCE)
                {
                    // We tried, but didn't go anywhere. Give up!
                    break;
                }

                resultingDirection = ((resultingHorizontalDirection * resultingHorizontalTraveledDistance) +
                    (resultingVerticalDirection * resultingVerticalTraveledDistance)).normalized;
                remainingDistance = resultingHorizontalRemainingDistance + resultingVerticalRemainingDistance;
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
