using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField]
    public string WeaponName;
    [SerializeField]
    private float timeBetweenShots = .5f;
    [SerializeField]
    private float maximumAdditionalRandomTimeBetweenShots = 0;
    private float curTimeBetweenShots { get; set; } = 0f;
    [SerializeReference]
    private Projectile projectilePF;

    public Faction MyFaction { get; private set; }
    [SerializeField]
    private float randomStartSleepTimeModifierMax = 0;

    [SerializeField]
    private int minimumBullets = 1;
    [SerializeField]
    private int maximumBullets = 1;

    [SerializeField]
    private float minimumMovementSpeed = 10f;
    [SerializeField]
    private float maximumMovementSpeed = 12f;
    [SerializeField]
    private AnimationCurve movementSpeedCurve;

    /// <summary>
    /// Inaccuracy.
    /// When determining the firing angle, take one unit towards the enemy, then multiply a random unit circle by this value.
    /// </summary>
    [SerializeField]
    private float maximumSpread = 1f;

    private void Start()
    {
        this.curTimeBetweenShots = Random.Range(this.timeBetweenShots, this.timeBetweenShots + this.randomStartSleepTimeModifierMax);
    }

    public bool ReadyToFire
    {
        get
        {
            return this.curTimeBetweenShots <= 0;
        }
    }

    public void InitializeWeapon(Faction forFaction)
    {
        this.MyFaction = forFaction;
    }

    public void FireInDirection(Vector2 towardsPosition)
    {
        if (this.curTimeBetweenShots > 0)
        {
            return;
        }

        this.curTimeBetweenShots = this.timeBetweenShots + Random.Range(0, maximumAdditionalRandomTimeBetweenShots);
        int randomNumberOfBullets = Random.Range(this.minimumBullets, this.maximumBullets);
        for (int ii = 0; ii < randomNumberOfBullets; ii ++)
        {
            Vector2 firingDirection = (towardsPosition.normalized + Random.insideUnitCircle * this.maximumSpread).normalized;
            Projectile newProjectile = Instantiate(this.projectilePF);
            newProjectile.transform.position = this.transform.position;

            float projectileSpeed = Mathf.Lerp(this.minimumMovementSpeed, this.maximumMovementSpeed, this.movementSpeedCurve.Evaluate(Random.Range(0, 1f)));
            newProjectile.StartProjectile(firingDirection, this.MyFaction, projectileSpeed);
        }
    }

    public void TickDownTimers()
    {
        if (this.curTimeBetweenShots > 0)
        {
            this.curTimeBetweenShots -= Time.deltaTime;
        }
    }
    public void AddCooldown(float time)
    {
        this.curTimeBetweenShots += time;
    }
}
