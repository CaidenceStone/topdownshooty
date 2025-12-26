using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class Entity : MonoBehaviour
{
    public const int MAXIMUMSLIDEITERATIONS = 10;
    public const float PLANCKCOLLISIONDISTANCE = .01f;

    [SerializeReference]
    public Rigidbody2D Body;
    [SerializeField]
    protected LayerMask projectileMask;
    [SerializeField]
    protected LayerMask environmentMask;
    [SerializeField]
    public double MaximumHP = 5.0;
    public double CurrentHP { get; protected set; }

    [SerializeField]
    public Faction MyFaction;
    public bool ShouldDestroy { get; set; } = false;

    [SerializeField]
    private float damageFlickerTime = .2f;
    [SerializeField]
    private AnimationCurve damageFlickerColorOverTime;
    [SerializeField]
    private Color damageFlickerColorAtMinimum = Color.white;
    [SerializeField]
    private Color damageFlickerColorAtMaximum = Color.white;
    [SerializeReference]
    private SpriteRenderer[] renderersToFlickerDuringDamage;

    private readonly List<ImpactEvent> processingImpactEvents = new List<ImpactEvent>();

    private float? curHitStunTime { get; set; } = 0;

    [SerializeReference]
    private float timeBetweenRethinks;
    private float curTimeBetweenRethinks { get; set; } = 0;

    public bool IsInHitStun
    {
        get
        {
            return this.curHitStunTime.HasValue;
        }
    }

    private Coroutine curDamageFlickerCoroutine { get; set; } = null;

    public delegate void HealthChangedDelegate(double oldHealth, double newHealth, double maximumHealth);
    public event HealthChangedDelegate HealthChanged;

    protected virtual void Start()
    {
        this.CurrentHP = this.MaximumHP;
        this.HealthChanged?.Invoke(this.CurrentHP, this.MaximumHP, this.MaximumHP);
        StaticLevelDirector.CurrentLevelDirector.RegisterEntity(this);
    }

    protected virtual void Update()
    {
        this.InputUpdates();
    }

    protected void FixedUpdate()
    {
        if (this.ShouldDestroy)
        {
            this.HandleDestroy();
            return;
        }

        this.HandleImpactEvents();
        this.TickDownTimers();

        if (this.IsInHitStun)
        {
            this.curHitStunTime -= Time.deltaTime;
            if (this.curHitStunTime < 0)
            {
                this.curHitStunTime = null;
            }

            return;
        }

        if (this.curTimeBetweenRethinks < 0)
        {
            this.curTimeBetweenRethinks = this.timeBetweenRethinks;
            this.Rethink();
        }

        this.BehaviourUpdate();
    }

    protected virtual void TickDownTimers()
    {
        this.curTimeBetweenRethinks -= Time.deltaTime;
    }

    protected virtual void BehaviourUpdate()
    {
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if ((this.projectileMask & (1 << collision.gameObject.layer)) != 0)
        {
            this.HandleCollisionBullet(collision);
            return;
        }
    }

    void HandleCollisionBullet(Collision2D collision)
    {
        Projectile projectile = collision.gameObject.GetComponentInParent<Projectile>();

        if (this.MyFaction == projectile.MyFaction)
        {
            Physics2D.IgnoreCollision(collision.collider, collision.otherCollider);
            return;
        }

        this.TakeDamage(projectile.Damage);

        if (projectile.HitStunTime > 0)
        {
            this.curHitStunTime = projectile.HitStunTime;
        }

        this.RegisterImpactEvent(new ImpactEvent(projectile.FiringAngle, projectile.ImpactTime, projectile.Impact, projectile.ImpactOverTime));

        projectile.SetShouldDestroy();
    }

    public virtual void MarkForDestruction()
    {
        this.ShouldDestroy = true;
    }

    public void TakeDamage(double damageAmount)
    {
        double previousHP = this.CurrentHP;
        this.CurrentHP -= damageAmount;

        if (this.CurrentHP <= 0)
        {
            this.MarkForDestruction();
        }
        else
        {
            if (this.curDamageFlickerCoroutine != null)
            {
                this.StopCoroutine(this.curDamageFlickerCoroutine);
            }
            this.curDamageFlickerCoroutine = this.StartCoroutine(DamageCoroutine());
        }

        this.HealthChanged?.Invoke(previousHP, this.CurrentHP, this.MaximumHP);
    }

    IEnumerator DamageCoroutine()
    {
        float curDamageFlickerTime = this.damageFlickerTime;

        do
        {
            curDamageFlickerTime = Mathf.Max(curDamageFlickerTime - Time.deltaTime, 0);
            float timeRemainingY = Mathf.InverseLerp(this.damageFlickerTime, 0, (this.damageFlickerTime - curDamageFlickerTime) / this.damageFlickerTime);

            for (int ii = 0, renderersToFlickerLength = this.renderersToFlickerDuringDamage.Length; ii < renderersToFlickerLength; ii++)
            {
                Color setToColor = Color.Lerp(this.damageFlickerColorAtMinimum, this.damageFlickerColorAtMaximum, curDamageFlickerTime / this.damageFlickerTime);
                this.renderersToFlickerDuringDamage[ii].color = setToColor;
            }

            yield return new WaitForEndOfFrame();
        } while (curDamageFlickerTime > 0);

        for (int ii = 0, renderersToFlickerLength = this.renderersToFlickerDuringDamage.Length; ii < renderersToFlickerLength; ii++)
        {
            this.renderersToFlickerDuringDamage[ii].color = Color.white;
        }
    }

    public void RegisterImpactEvent(ImpactEvent impactEvent)
    {
        this.processingImpactEvents.Add(impactEvent);
    }

    void HandleImpactEvents()
    {
        for (int ii = this.processingImpactEvents.Count - 1; ii >= 0; ii--)
        {
            ImpactEvent thisEvent = this.processingImpactEvents[ii];
            thisEvent.RemainingTime -= Time.deltaTime;

            float currentImpact = thisEvent.GetImpactAtCurrentTime();
            float currentImpactAtTime = thisEvent.Impact * Time.deltaTime;

            this.MoveEntity(thisEvent.OriginalDirection * currentImpactAtTime);

            if (thisEvent.RemainingTime < 0)
            {
                this.processingImpactEvents.RemoveAt(ii);
            }
        }
    }

    protected virtual void Rethink()
    {

    } 

    protected virtual void HandleDestroy()
    {
        StaticLevelDirector.CurrentLevelDirector.UnregisterEntity(this);
        Destroy(this.gameObject);
    }

    protected virtual void MoveEntity(Vector2 movement)
    {
        RaycastHit2D[] hits = new RaycastHit2D[1];

        // Sub-function that splits movement in to separate horizontal and vertical axes
        // Returns the remaining amount of available movement
        bool MoveEntitySlideIteration(Vector2 iterationMovement, out Vector2 remainingMovement)
        {
            bool anyChanged = false;
            remainingMovement = iterationMovement;

            if (!Mathf.Approximately(iterationMovement.x, 0) && SlideAxis(Vector2.right, iterationMovement.x, out float remainingDistance))
            {
                remainingMovement.x = remainingDistance;
                anyChanged = true;
            }
            
            if (!Mathf.Approximately(iterationMovement.y, 0) && SlideAxis(Vector2.up, iterationMovement.y, out remainingDistance))
            {
                remainingMovement.y = remainingDistance;
                anyChanged = true;
            }

            return anyChanged;
        }

        bool SlideAxis(Vector2 axis, float distance, out float remainingDistance)
        {
            if (Mathf.Approximately(distance, 0))
            {
                remainingDistance = 0;
                return false;
            }

            remainingDistance = distance;            
            if (this.Body.Cast(new Vector2(axis.x * Mathf.Sign(distance), axis.y * Mathf.Sign(distance)), new ContactFilter2D() { layerMask = this.environmentMask, useLayerMask = true }, hits, Mathf.Abs(distance)) > 0)
            {
                float posDifference = hits[0].distance;

                if (posDifference > PLANCKCOLLISIONDISTANCE)
                {
                    remainingDistance = Mathf.MoveTowards(distance, 0f, posDifference);
                    this.Body.position += axis * Mathf.Sign(distance) * (Mathf.MoveTowards(posDifference, 0, PLANCKCOLLISIONDISTANCE));

                    return true;
                }

                return false;
            }
            else
            {
                this.Body.position += axis * distance;
                remainingDistance = 0;
                return true;
            }
        }

        if (this.Body.Cast(movement, new ContactFilter2D() { layerMask = this.environmentMask, useLayerMask = true }, hits, movement.magnitude) > 0)
        {
            Vector2 slideRemaining = movement;
            for (int ii = 0; ii < MAXIMUMSLIDEITERATIONS; ii++)
            {
                Vector2 initialRemainingSlide = slideRemaining;
                if (!MoveEntitySlideIteration(initialRemainingSlide, out slideRemaining))
                {
                    break;
                }
                if (Mathf.Approximately(slideRemaining.x, 0) && Mathf.Approximately(slideRemaining.y, 0))
                {
                    break;
                }
            }
            return;
        }
        else
        {
            // Otherwise, move the full distance
            this.Body.position += movement;
        }
    }

    protected virtual void InputUpdates()
    {

    }
}
