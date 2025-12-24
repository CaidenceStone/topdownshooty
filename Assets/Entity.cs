using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity : MonoBehaviour
{
    [SerializeReference]
    protected Rigidbody2D body;
    [SerializeField]
    protected LayerMask projectileMask;
    [SerializeField]
    public decimal MaximumHP = 5.0M;
    public decimal CurrentHP { get; protected set; }

    [SerializeField]
    public Faction MyFaction;
    protected bool shouldDestroy { get; set; } = false;

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

    private Coroutine curDamageFlickerCoroutine { get; set; } = null;

    protected virtual void Start()
    {
        this.CurrentHP = this.MaximumHP;
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
        projectile.Destroy();

    }

    public void Destroy()
    {
        this.shouldDestroy = true;
    }

    public void TakeDamage(decimal damageAmount)
    {
        this.CurrentHP -= damageAmount;

        if (this.CurrentHP <= 0)
        {
            this.Destroy();
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
}
