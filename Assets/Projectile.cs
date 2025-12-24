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

    public Faction MyFaction { get; private set; }

    private Vector2 firingAngle { get; set; }

    private bool shouldDestroy { get; set; } = false;

    public void StartProjectile(Vector2 startFiringAngle, Faction ofFaction)
    {
        this.firingAngle = startFiringAngle;
        float angleToLook = Vector2.SignedAngle(startFiringAngle, Vector2.up);
        this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
        this.MyFaction = ofFaction;
    }

    private void FixedUpdate()
    {
        if (this.shouldDestroy)
        {
            Destroy(this.gameObject);
            return;
        }

        this.body.position += firingAngle * movementSpeed * Time.deltaTime;
    }

    public void Destroy()
    {
        this.shouldDestroy = true;
    }
}
