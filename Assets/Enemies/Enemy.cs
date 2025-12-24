using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : Entity
{
    protected Vector2 movingTowards { get; set; }
    [SerializeField]
    private float movementSpeedPerSecond = 5f;

    /// <summary>
    /// When determining where to move, pick a spot this far away from the player in a random direction.
    /// This helps make movement feel a little less robotic.
    /// </summary>
    [SerializeField]
    private float randomPlayerPositionOffset = 1f;

    /// <summary>
    /// When trying to determine where to stand, try to stand at least this far away.
    /// </summary>
    [SerializeField]
    private float minDesiredDistanceFromPlayer = 5f;

    /// <summary>
    /// When trying to determine where to stand, try to stand no mor ethan this far away.
    /// </summary>
    [SerializeField]
    private float maxDesiredDistanceFromPlayer = 10f;

    protected Entity primaryTarget { get; set; }

    protected override void Start()
    {
        this.movingTowards = this.Body.position;
        base.Start();
    }

    protected override void BehaviourUpdate()
    {
        base.BehaviourUpdate();

        if (this.primaryTarget == null || this.primaryTarget.ShouldDestroy)
        {
            return;
        }

        Vector2 distanceBetweenPoints = movingTowards - Body.position;
        Vector2 distanceToMove = Vector2.ClampMagnitude(distanceBetweenPoints, Time.deltaTime * this.movementSpeedPerSecond);
        this.MoveEntity(distanceToMove);
    }

    protected override void Rethink()
    {
        base.Rethink();

        // First, pick a player to stand near
        // TODO: This is a hacky quick way of determining closest player, do something more elegant
        TDSCharacterController closestCharacter = null;
        float? closestDistance = null;
        foreach (TDSCharacterController controller in ConfirmPlayerWatcher.GetCharacters())
        {
            float distanceToPoint = Vector2.Distance(this.Body.position, controller.Body.position);
            if (!closestDistance.HasValue || distanceToPoint < closestDistance.Value)
            {
                closestDistance = distanceToPoint;
                this.movingTowards = controller.Body.position;
                closestCharacter = controller;
            }
        }

        if (closestCharacter == null || closestCharacter.ShouldDestroy)
        {
            primaryTarget = null;
            return;
        }

        primaryTarget = closestCharacter;

        Vector2 playerPosition = closestCharacter.Body.position;

        // Add a random offset to the destination position
        playerPosition += Random.insideUnitCircle * this.randomPlayerPositionOffset;

        // Choose a point between the maximum and minimum desired distance
        Vector2 differenceInPosition = (playerPosition - this.Body.position).normalized;
        float randomDesiredDistance = Random.Range(this.minDesiredDistanceFromPlayer, this.maxDesiredDistanceFromPlayer);

        // Take the player's position, and imagine a point that random distance away towards the enemy's position
        this.movingTowards = playerPosition - differenceInPosition * randomDesiredDistance;
    }

    protected virtual void OnDrawGizmos()
    {
        if (primaryTarget != null)
        {
            Debug.DrawLine(this.Body.position, this.movingTowards, Color.green);
        }
    }
}
