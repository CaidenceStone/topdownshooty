using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : Entity
{
    private Vector2 movingTowards { get; set; }
    [SerializeField]
    private float movementSpeedPerSecond = 5f;

    protected override void Start()
    {
        this.movingTowards = this.body.position;
        base.Start();
    }

    private void FixedUpdate()
    {
        if (this.shouldDestroy)
        {
            Destroy(this.gameObject);
            return;
        }

        // TODO: This is a hacky quick way of determining closest player, do something more elegant
        float? closestDistance = null;
        foreach (TDSCharacterController controller in ConfirmPlayerWatcher.GetCharacters())
        {
            float distanceToPoint = Vector2.Distance(this.body.position, controller.Body.position);
            if (!closestDistance.HasValue || distanceToPoint < closestDistance.Value)
            {
                closestDistance = distanceToPoint;
                this.movingTowards = controller.Body.position;
            }
        }

        Vector2 distanceBetweenPoints = movingTowards - body.position;
        Vector2 distanceToMove = Vector2.ClampMagnitude(distanceBetweenPoints, Time.deltaTime * this.movementSpeedPerSecond);
        this.body.position += distanceToMove;
    }
}
