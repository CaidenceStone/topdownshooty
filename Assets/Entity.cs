using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Entity : MonoBehaviour
{
    const float PLANCKCOLLISIONDISTANCE = .001f;

    [SerializeReference]
    public Rigidbody2D Body;
    [SerializeField]
    protected LayerMask projectileMask;
    [SerializeField]
    protected LayerMask environmentMask;
    [SerializeField]
    public decimal MaximumHP = 5.0M;
    public decimal CurrentHP { get; protected set; }

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

    protected virtual void Start()
    {
        this.CurrentHP = this.MaximumHP;
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

        projectile.Destroy();
    }

    public virtual void MarkForDestruction()
    {
        this.ShouldDestroy = true;
    }

    public void TakeDamage(decimal damageAmount)
    {
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
        Destroy(this.gameObject);
    }

    protected virtual void MoveEntity(Vector2 movement)
    {
        // If there is a wall in that direction, you can only move up to the wall
        RaycastHit2D hit = Physics2D.Raycast(this.Body.position, movement.normalized, movement.magnitude, this.environmentMask.value);
        if (hit.collider != null)
        {
            Vector2 differenceInPosition = (hit.point - this.Body.position).normalized;
            this.Body.position = hit.point - differenceInPosition * PLANCKCOLLISIONDISTANCE;
            return;
        }

        // Otherwise, move the full distance
        this.Body.position += movement;
    }
}
