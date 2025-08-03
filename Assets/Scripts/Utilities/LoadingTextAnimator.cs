using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// A utility component to create an animated "Loading..." text effect.
/// It cycles through dot patterns on a target TextMeshPro component.
/// </summary>
public class LoadingTextAnimator : MonoBehaviour
{
    [Tooltip("The TextMeshPro component to animate. This is required.")]
    [SerializeField]
    private TMP_Text targetText;

    [Tooltip("The base text to display before the dots, e.g., 'Loading'")]
    [SerializeField]
    private string baseText = "Loading";

    [Tooltip("The speed of the dot animation in seconds per step.")]
    [SerializeField]
    private float animationSpeed = 0.4f;

    /// <summary>
    /// Returns true if the animation coroutine is currently running.
    /// </summary>
    public bool IsAnimating { get; private set; }

    private Coroutine _animationCoroutine;
    private readonly string[] _dotSequence = { " ", ".", "..", "..." };

    private void OnDisable()
    {
        // Ensure the coroutine is stopped if the object is disabled.
        StopAnimation();
    }

    /// <summary>
    /// Starts the loading text animation.
    /// </summary>
    /// <param name="newBaseText">Optional: A new base text to use for this animation cycle.</param>
    public void StartAnimation(string newBaseText = null)
    {
        if (IsAnimating)
            return;

        if (targetText == null)
        {
            Debug.LogError(
                "LoadingTextAnimator: Target Text is not assigned. Animation cannot start.",
                this
            );
            return;
        }

        if (newBaseText != null)
        {
            this.baseText = newBaseText;
        }

        targetText.enabled = true; // Make sure text is visible
        IsAnimating = true;
        _animationCoroutine = StartCoroutine(AnimateText());
    }

    /// <summary>
    /// Stops the loading text animation. The text on the target component is not cleared.
    /// </summary>
    public void StopAnimation()
    {
        if (!IsAnimating)
            return;

        IsAnimating = false;
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        // The calling script is responsible for setting the final text.
    }

    private IEnumerator AnimateText()
    {
        int sequenceIndex = 0;
        while (IsAnimating)
        {
            if (targetText != null)
                targetText.text = baseText + _dotSequence[sequenceIndex];

            sequenceIndex = (sequenceIndex + 1) % _dotSequence.Length;
            yield return new WaitForSeconds(animationSpeed);
        }
    }
}
