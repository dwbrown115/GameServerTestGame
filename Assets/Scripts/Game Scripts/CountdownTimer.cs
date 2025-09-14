using TMPro; // Added for TMP_Text
using UnityEngine;
using UnityEngine.EventSystems; // Required for controlling UI focus
using UnityEngine.InputSystem;
using UnityEngine.UI; // Required for the Selectable class

public class GameOverController : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("The duration of the countdown in seconds.")]
    [SerializeField]
    private float countdownDuration = 60f;

    [Header("References")]
    [Tooltip("The Game Over modal to enable when the timer reaches zero.")]
    [SerializeField]
    private GameObject gameOverModal;

    public PlayerController2D playerController2D; // Reference to the PlayerController2D
    public PlayerHealth playerHealth; // Reference to PlayerHealth for health-based game over

    // Event to notify listeners when the countdown finishes.
    public static event System.Action OnCountdownFinished;

    // Event to notify listeners of the time remaining.
    public static event System.Action<float> OnTimeChanged;

    private float currentTime;
    private bool isRunning = true;
    private InputActionMap playerActionMap;
    private InputActionMap uiActionMap;

    private void Awake()
    {
        // Find the action maps from the project-wide input actions asset.
        var inputActions = InputSystem.actions;
        if (inputActions == null)
        {
            Debug.LogError(
                "No project-wide Input Actions found. Please set one in 'Edit -> Project Settings -> Input System Package'.",
                this
            );
            return;
        }

        playerActionMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
        uiActionMap = inputActions.FindActionMap("UI", throwIfNotFound: true);
    }

    private void Start()
    {
        // Ensure the game over modal is disabled at the start.
        if (gameOverModal != null)
        {
            gameOverModal.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Game Over Modal is not set in the CountdownTimer.", this);
        }

        // Enable player controls and disable UI controls at the start.
        playerActionMap?.Enable();
        uiActionMap?.Disable();

        currentTime = countdownDuration;

        if (playerHealth == null && playerController2D != null)
        {
            playerHealth = playerController2D.GetComponent<PlayerHealth>();
        }
        if (playerHealth == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponent<PlayerHealth>();
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied += TriggerGameOver;
        }
    }

    private void Update()
    {
        if (!isRunning)
            return;

        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            OnTimeChanged?.Invoke(currentTime);
        }
        else
        {
            currentTime = 0;
            OnTimeChanged?.Invoke(currentTime);
            isRunning = false;
            ShowGameOver();
            OnCountdownFinished?.Invoke();
        }
    }

    private void TriggerGameOver()
    {
        if (!isRunning)
            return;
        isRunning = false;
        currentTime = 0;
        OnTimeChanged?.Invoke(currentTime);
        ShowGameOver();
        OnCountdownFinished?.Invoke();
    }

    private void ShowGameOver()
    {
        if (gameOverModal != null)
        {
            gameOverModal.SetActive(true);
            var selectable = gameOverModal.GetComponentInChildren<Selectable>();
            if (selectable != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        // Switch to the UI action map to enable menu navigation.
        playerActionMap?.Disable();
        uiActionMap?.Enable();
    }
}
