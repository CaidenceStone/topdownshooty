using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : Entity
{
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
    protected Path currentPath { get; set; } = null;

    protected override void Start()
    {
        base.Start();
    }

    protected override void BehaviourUpdate()
    {
        base.BehaviourUpdate();

        if (this.primaryTarget == null || this.primaryTarget.ShouldDestroy)
        {
            return;
        }

        if (this.currentPath != null && !this.currentPath.IsComplete)
        {
            Vector2 destination = this.currentPath.ApproachWaypointByDistance(this.Body.position, this.movementSpeedPerSecond * Time.deltaTime, out float remainingDistance);
            this.MoveEntity(destination - this.Body.position);
        }
    }

    protected override void Rethink()
    {
        base.Rethink();

        // First, pick a player to stand near
        // TODO: This is a hacky quick way of determining closest player, do something more elegant
        TDSCharacterController closestCharacter = null;
        float? closestDistance = null;
        foreach (TDSCharacterController controller in StaticLevelDirector.CurrentLevelDirector.AlivePlayers)
        {
            if (controller.ShouldDestroy)
            {
                continue;
            }

            float distanceToPoint = Vector2.Distance(this.Body.position, controller.Body.position);
            if (!closestDistance.HasValue || distanceToPoint < closestDistance.Value)
            {
                closestDistance = distanceToPoint;
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
        this.currentPath = SpatialReasoningCalculator.CurrentInstance.GetPath(this.Body.position, playerPosition);
        if (this.currentPath == null)
        {
            this.currentPath = null;
            Debug.Log($"I couldn't find a path from {this.Body.position} to {playerPosition}");
            return;
        }

        // Debug.Log($"I made a path going from {this.Body.position} to {playerPosition} that is {this.currentPath.PathPointsCount} path points");
    }

    private void OnDrawGizmosSelected()
    {
        if (primaryTarget != null && this.currentPath != null && !this.currentPath.IsComplete)
        {
            Vector2 previousNode = this.Body.position;

            for (int ii = 0; ii < this.currentPath.PathPointsCount; ii++)
            {
                Debug.DrawLine(currentPath.PathPoints[ii], previousNode, Color.green);
                previousNode = currentPath.PathPoints[ii];
            }
        }
    }
}
