using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour, IPickup
{
    [SerializeReference]
    public Entity User;
    [SerializeField]
    public string WeaponName;
    [SerializeField]
    public float TimeBetweenShots = .5f;
    [SerializeField]
    public float MaximumAdditionalRandomTimeBetweenShots = 0;
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

    [SerializeReference]
    private GameObject fieldRoot;
    public bool IsInWorld { get; private set; }

    private void Start()
    {
        this.curTimeBetweenShots = Random.Range(this.TimeBetweenShots, this.TimeBetweenShots + this.randomStartSleepTimeModifierMax);
    }

    public bool ReadyToFire
    {
        get
        {
            return this.curTimeBetweenShots <= 0;
        }
    }

    public void InitializeWeapon(Faction forFaction, Entity user)
    {
        this.MyFaction = forFaction;
        this.User = user;
    }

    public void FireInDirection(Vector2 towardsPosition)
    {
        if (this.curTimeBetweenShots > 0)
        {
            return;
        }

        this.curTimeBetweenShots = this.TimeBetweenShots + Random.Range(0, MaximumAdditionalRandomTimeBetweenShots);
        int randomNumberOfBullets = Random.Range(this.minimumBullets, this.maximumBullets);
        for (int ii = 0; ii < randomNumberOfBullets; ii ++)
        {
            Vector2 firingDirection = (towardsPosition.normalized + Random.insideUnitCircle * this.maximumSpread).normalized;
            Projectile newProjectile = Instantiate(this.projectilePF);
            newProjectile.transform.position = this.transform.position;

            float projectileSpeed = Mathf.Lerp(this.minimumMovementSpeed, this.maximumMovementSpeed, this.movementSpeedCurve.Evaluate(Random.Range(0, 1f)));
            newProjectile.StartProjectile(firingDirection, this.MyFaction, projectileSpeed);
            newProjectile.AddForceToVelocity(this.User.VelocityPerSecond, 1f);
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

    public void SetIsInWorld(bool toValue)
    {
        this.IsInWorld = toValue;

        if (this.IsInWorld)
        {
            this.fieldRoot.SetActive(true);
        }
        else
        {
            this.fieldRoot.SetActive(false);
            this.transform.localPosition = Vector3.zero;
        }
    }

    public void ApplyToCharacter(TDSCharacterController toApplyTo)
    {
        toApplyTo.OwnWeaponCollection.AddWeapon(this);
    }
}
