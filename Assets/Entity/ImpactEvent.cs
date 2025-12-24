using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactEvent
{
    public readonly Vector2 OriginalDirection;
    public readonly float Duration;
    public readonly float Impact;
    public readonly AnimationCurve ImpactModifierOverTime;

    public float RemainingTime { get; set; }
    public float PercentCompletion
    {
        get
        {
            return 1f - (this.RemainingTime / this.Duration);
        }
    }

    public ImpactEvent(Vector2 originalDirection, float duration, float impact, AnimationCurve impactModifierOverTime)
    {
        this.OriginalDirection = originalDirection;
        this.Duration = duration;
        this.Impact = impact;
        this.ImpactModifierOverTime = impactModifierOverTime;

        this.RemainingTime = duration;
    }

    public float GetImpactAtCurrentTime()
    {
        return this.ImpactModifierOverTime.Evaluate(this.PercentCompletion) * this.Impact;
    }
}
