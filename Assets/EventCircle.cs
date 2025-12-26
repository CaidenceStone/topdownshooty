using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class EventCircle : MonoBehaviour
{
    public float TimeInCircleToProc = 2f;
    public float DistanceForCircle = 5f;
    private float curTimeInCircleToProc { get; set; } = 0;

    public UnityEvent<EventCircle> StartPlayProc;

    public Color StartColor;
    public Color EndColor;
    public SpriteRenderer CircleRenderer;

    private void FixedUpdate()
    {
        CircleRenderer.color = Color.Lerp(StartColor, EndColor, curTimeInCircleToProc / TimeInCircleToProc);

        IEnumerable<TDSCharacterController> allCharacters = StaticLevelDirector.CurrentLevelDirector.AlivePlayers;
        int count = allCharacters.Count();

        if (count == 0)
        {
            curTimeInCircleToProc = 0;
            return;
        }

        foreach (TDSCharacterController character in allCharacters)
        {
            if (Vector3.Distance(character.Body.position, transform.position) > DistanceForCircle)
            {
                curTimeInCircleToProc = 0;
                return;
            }
        }

        curTimeInCircleToProc += Time.deltaTime;
        if (curTimeInCircleToProc < this.TimeInCircleToProc)
        {
            return;
        }

        this.StartPlayProc.Invoke(this);
        this.StartPlayProc = null;
        this.enabled = false;
        Destroy(this.gameObject);
    }
}
