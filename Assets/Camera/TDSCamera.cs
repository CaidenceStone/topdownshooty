using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TDSCamera : MonoBehaviour
{
    [SerializeField]
    private float zDistanceBack = -10f;
    private static List<TDSCharacterController> entitiesToFollow { get; set; } = new List<TDSCharacterController>();
    [SerializeField]
    private float minimumFollowSpeed = .2f;
    [SerializeField]
    private float maximumFollowSpeed = 6f;
    [SerializeField]
    private float distanceForMaximumSpeed = 5f;
    [SerializeField]
    private AnimationCurve followSpeedAtDistance;

    public static void RegisterFollowing(TDSCharacterController toFollow)
    {
        entitiesToFollow.Add(toFollow);
    }

    public static void UnregisterFollowing(TDSCharacterController toUnregister)
    {
        entitiesToFollow.Remove(toUnregister);
    }

    private void FixedUpdate()
    {
        if (entitiesToFollow.Count == 0)
        {
            return;
        }

        Vector2 followPoint = this.GetCenterPointOfAllEntitiesToFollow();
        float distancePercentage = Vector2.Distance(transform.position, followPoint) / this.distanceForMaximumSpeed;
        float followingSpeed = Mathf.Lerp(this.minimumFollowSpeed, this.maximumFollowSpeed, this.followSpeedAtDistance.Evaluate(Mathf.Clamp(distancePercentage, 0, 1f))) * Time.deltaTime;
        Vector3 newPosition = Vector2.MoveTowards(transform.position, followPoint, followingSpeed);
        newPosition.z = zDistanceBack;
        this.transform.position = newPosition;
    }

    private Vector2 GetCenterPointOfAllEntitiesToFollow()
    {
        Vector2 totalPosition = Vector2.zero;

        foreach (TDSCharacterController curEntity in entitiesToFollow)
        {
            totalPosition += (Vector2)curEntity.VisualAimingCenter;
        }

        totalPosition /= entitiesToFollow.Count;
        return totalPosition;
    }
}
