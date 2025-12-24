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

    private Vector2 firingAngle { get; set; }

    public void StartProjectile(Vector2 startFiringAngle)
    {
        this.firingAngle = startFiringAngle;
        float angleToLook = Vector2.SignedAngle(startFiringAngle, Vector2.up);
        this.rotationPoint.transform.rotation = Quaternion.Euler(0, 0, -angleToLook);
    }

    private void FixedUpdate()
    {
        this.body.position += firingAngle * movementSpeed * Time.deltaTime;
    }
}
