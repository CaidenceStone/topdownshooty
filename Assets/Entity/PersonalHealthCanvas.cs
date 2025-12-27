using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PersonalHealthCanvas : MonoBehaviour
{
    [SerializeField]
    private List<Image> imagesToAlpha = new List<Image>();
    [SerializeField]
    private List<TMP_Text> textsToAlpha = new List<TMP_Text>();

    public float CurShowDuration { get; set; } = 0;

    [SerializeField]
    private AnimationCurve TransparencyAtRemainingDuration;

    private float incomingValue { get; set; } = -1;
    private float currentInterpolatedValue { get; set; } = -1;

    [SerializeField]
    private float graphicalValueChangePerSecond = 10;

    public void Show(double curValue, double maxValue, float forTime)
    {
        if (currentInterpolatedValue < 0)
        {
            currentInterpolatedValue = (float)curValue;
        }
        incomingValue = (float)curValue;

        this.gameObject.SetActive(true);
        this.CurShowDuration = forTime;

        foreach (Image curImage in this.imagesToAlpha)
        {
            if (curImage.fillMethod == Image.FillMethod.Radial90)
            {
                curImage.fillAmount = (float)(curValue / maxValue);
            }
        }

        foreach (TMP_Text text in this.textsToAlpha)
        {
            text.text = currentInterpolatedValue.ToString("F0");
        }

        this.UpdateAlpha();
    }

    private void Update()
    {
        if (this.CurShowDuration > 0)
        {
            this.CurShowDuration -= Time.deltaTime;

            if (this.CurShowDuration <= 0)
            {
                this.Clear();
                return;
            }

            this.currentInterpolatedValue = Mathf.MoveTowards(this.currentInterpolatedValue, this.incomingValue, this.graphicalValueChangePerSecond * Time.deltaTime);

            // Oops this should be dead!
            if (this.currentInterpolatedValue <= 0)
            {
                this.Clear();
                return;
            }

            foreach (TMP_Text text in this.textsToAlpha)
            {
                text.text = this.currentInterpolatedValue.ToString("F0");
            }

            this.UpdateAlpha();
        }
    }

    void UpdateAlpha()
    {
        foreach (Image curImage in imagesToAlpha)
        {
            curImage.color = new Color(curImage.color.r, curImage.color.g, curImage.color.b, this.TransparencyAtRemainingDuration.Evaluate(this.CurShowDuration));
        }

        foreach (TMP_Text text in textsToAlpha)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, this.TransparencyAtRemainingDuration.Evaluate(this.CurShowDuration));
        }
    }

    public void Clear()
    {
        this.gameObject.SetActive(false);
    }
}
