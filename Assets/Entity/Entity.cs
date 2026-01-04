using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class Entity : Mob
{
    public const float TIMETOIGNORECOLLIDERSAFTERHURTBOX = .2f;

    public SpatialCoordinate LastStoodCoordinate { get; set; }
    public Vector2 LastStoodVector2 { get; set; }

    [SerializeField]
    protected LayerMask projectileMask;
    [SerializeField]
    public double MaximumHP = 5.0;
    [SerializeField]
    protected PersonalHealthCanvas ownPersonalHealthCanvas = null;
    public double CurrentHP { get; protected set; }

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
    [SerializeField]
    private float secondsToShowHealthCanvasOnDamage = .25f;

    [SerializeReference]
    private float timeBetweenRethinks;
    private float curTimeBetweenRethinks { get; set; } = 0;

    public EntityModifier Modifiers { get; set; } = new EntityModifier();
    protected List<CollisionIgnorance> collisionIgnorances { get; set; } = new List<CollisionIgnorance>();

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
        this.CurrentHP = this.MaximumHP + this.Modifiers.MaximumHealthFlatAdditionModifier;
        this.HealthChanged?.Invoke(this.CurrentHP, this.MaximumHP, this.MaximumHP);

        if (this.ownPersonalHealthCanvas != null)
        {
            // TODO
            // We might want to do something with that here...
        }

        if (this.ownPersonalHealthCanvas != null)
        {
            this.ownPersonalHealthCanvas.Clear();
        }

        Vector2Int roundedCoordinate = new Vector2Int(Mathf.FloorToInt(this.Body.position.x * MapGenerator.COORDINATETOPOSITIONDIVISOR), Mathf.FloorToInt(this.Body.position.y * MapGenerator.COORDINATETOPOSITIONDIVISOR));
        if (SpatialReasoningCalculator.CurrentInstance.Positions.TryGetValue(roundedCoordinate, out SpatialCoordinate onTile))
        {
            this.LastStoodCoordinate = onTile;
            this.LastStoodVector2 = this.Body.position;
        }
        else
        {
            Debug.LogError($"This entity is starting on {roundedCoordinate}, but that coordinate is not in the database!");
        }

        StaticLevelDirector.CurrentLevelDirector.RegisterEntity(this);
    }

    protected virtual void Update()
    {
        this.InputUpdates();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (this.ScheduledForDestruction)
        {
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

        for (int ii = this.collisionIgnorances.Count - 1; ii >= 0; ii--)
        {
            CollisionIgnorance thisIgnorance = this.collisionIgnorances[ii];
            thisIgnorance.SecondsLeft -= Time.deltaTime;
            if (thisIgnorance.SecondsLeft <= 0)
            {
                thisIgnorance.Remove();
                this.collisionIgnorances.RemoveAt(ii);
            }
        }
    }

    protected virtual void BehaviourUpdate()
    {
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
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

        this.RegisterImpactEvent(new ImpactEvent(projectile.VelocityPerSecond.normalized, projectile.ImpactTime, projectile.Impact, projectile.ImpactOverTime));

        Physics2D.IgnoreCollision(collision.collider, collision.otherCollider);

        if (projectile.DiesOnImpact)
        {
            projectile.ScheduleForDestruction();
        }
    }

    public void TakeDamage(double damageAmount)
    {
        double previousHP = this.CurrentHP;
        this.CurrentHP -= damageAmount;

        if (this.CurrentHP <= 0)
        {
            this.ScheduleForDestruction();
        }
        else
        {
            this.ownPersonalHealthCanvas?.Show(this.CurrentHP, this.MaximumHP, this.secondsToShowHealthCanvasOnDamage);
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
        float fixedTimeStep = Time.fixedDeltaTime;
        for (int ii = this.processingImpactEvents.Count - 1; ii >= 0; ii--)
        {
            ImpactEvent thisEvent = this.processingImpactEvents[ii];
            thisEvent.RemainingTime -= fixedTimeStep;

            float currentImpact = thisEvent.GetImpactAtCurrentTime();
            this.AddForceToVelocity(thisEvent.OriginalDirection.normalized * currentImpact, fixedTimeStep);

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

    protected virtual void InputUpdates()
    {

    }

    public void ProcessHurtbox(EnemyHurtBox hurtBox)
    {
        Vector2 positionDifference = this.Body.position - (Vector2)hurtBox.transform.position;
        this.TakeDamage(hurtBox.Damage);
        this.RegisterImpactEvent(hurtBox.GetImpactEvent(positionDifference));
        List<Collider2D> attachedColliders = new List<Collider2D>(this.Body.attachedColliderCount);
        this.Body.GetAttachedColliders(attachedColliders);
        CollisionIgnorance ignorance = new CollisionIgnorance(attachedColliders, hurtBox.Colliders, TIMETOIGNORECOLLIDERSAFTERHURTBOX);
        this.collisionIgnorances.Add(ignorance);
        ignorance.Apply();
    }
}
