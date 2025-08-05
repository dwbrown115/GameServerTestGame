using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TimeUI : MonoBehaviour
{
    private TMP_Text timeText;

    private void Awake()
    {
        timeText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        CountdownTimer.OnTimeChanged += UpdateTimeText;
    }

    private void OnDisable()
    {
        CountdownTimer.OnTimeChanged -= UpdateTimeText;
    }

    private void UpdateTimeText(float remainingTime)
    {
        // Ensure time doesn't go below zero for display purposes.
        if (remainingTime < 0)
        {
            remainingTime = 0;
        }

        // Using CeilToInt to show whole numbers and avoid showing 0 when there's a fraction of a second left.
        timeText.text = $"Time: {Mathf.CeilToInt(remainingTime)}";
    }
}
