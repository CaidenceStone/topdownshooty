using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TDSCamera : MonoBehaviour
{
    [SerializeField]
    private Camera MyCamera;
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

    [SerializeField]
    public float MinimumZoomLevel = 8;
    [SerializeField]
    public float MaximumZoomLevel = 24;
    private float curZoomLevel { get; set; } = 0;
    [SerializeField]
    public float ZoomLevelChangeSpeedPerSecond = 5f;
    [SerializeField]
    public float ZoomLevelLerpPerSecond = .1f;
    [SerializeField]
    public float DistanceForMaximumZoom = 15f;
    [SerializeField]
    public float DistanceForMinimumZoom = 5f;

    public static void RegisterFollowing(TDSCharacterController toFollow)
    {
        entitiesToFollow.Add(toFollow);
    }

    public static void UnregisterFollowing(TDSCharacterController toUnregister)
    {
        entitiesToFollow.Remove(toUnregister);
    }

    private void Awake()
    {
        this.curZoomLevel = this.MaximumZoomLevel;
    }

    private void FixedUpdate()
    {
        if (entitiesToFollow.Count == 0)
        {
            return;
        }

        float highestDistanceBetweenActors = 0;
        
        if (entitiesToFollow.Count > 1)
        {
            for (int ii = 0; ii < entitiesToFollow.Count; ii++)
            {
                for (int otherIndex = 0; otherIndex < ii; otherIndex++)
                {
                    if (ii == otherIndex)
                    {
                        continue;
                    }

                    float currentDistance = Vector2.Distance(entitiesToFollow[ii].Body.position, entitiesToFollow[otherIndex].Body.position);
                    highestDistanceBetweenActors = Mathf.Max(highestDistanceBetweenActors, currentDistance);
                }
            }
        }

        float newTargetZoom = Mathf.Lerp(MinimumZoomLevel, MaximumZoomLevel, Mathf.InverseLerp(this.DistanceForMinimumZoom, DistanceForMaximumZoom, highestDistanceBetweenActors));
        curZoomLevel = Mathf.MoveTowards(curZoomLevel, newTargetZoom, ZoomLevelChangeSpeedPerSecond * Time.deltaTime);
        MyCamera.orthographicSize = curZoomLevel;

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
    public void SnapPosition(Vector2 position)
    {
        this.transform.position = position;
    }
}
