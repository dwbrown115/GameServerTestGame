using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

// using SharedLibrary.Requests;

// Removed PlayerPositionMessage and PlayerPositionResponse structs

[Serializable]
internal class PersistentPlayerData
{
    public string userId;
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Tooltip("The speed at which the player moves.")]
    [SerializeField]
    private float moveSpeed = 5f;

    private string serverAddress = "localhost:7123"; // Set your server address
    private string sessionId;
    private string playerId;
    public float positionUpdateRate = 0.2f; // How often to send position data

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;
    private bool isMovementDisabled;

    private PlayerWebSocketClient _playerWebSocketClient; // New instance of the client

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    private async void Start()
    {
        LoadCredentials();
        // Initialize the WebSocket client
        _playerWebSocketClient = new PlayerWebSocketClient(
            serverAddress,
            sessionId,
            playerId,
            positionUpdateRate,
            HandlePlayerPositionResponse, // Callback for messages (now receives PlayerPositionResponse)
            (error) => Debug.LogError($"PlayerWebSocketClient Error: {error}"), // Callback for errors
            () => Debug.Log("PlayerWebSocketClient: Connected!"), // Callback for connection
            () => Debug.Log("PlayerWebSocketClient: Disconnected!") // Callback for disconnection
        );
        await _playerWebSocketClient.ConnectAsync();
    }

    private void OnEnable()
    {
        moveAction.Enable();
        CountdownTimer.OnCountdownFinished += DisableMovement;
    }

    private async void OnDisable()
    {
        moveAction.Disable();
        CountdownTimer.OnCountdownFinished -= DisableMovement;

        // Removed CancelInvoke(nameof(SendPosition));
        if (_playerWebSocketClient != null)
        {
            await _playerWebSocketClient.DisconnectAsync();
        }
    }

    private void Update()
    {
        if (isMovementDisabled)
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
        // Send position updates via the new client
        _ = _playerWebSocketClient.SendPositionAsync(transform.position);
    }

    private void DisableMovement()
    {
        isMovementDisabled = true;
        rb.linearVelocity = Vector2.zero;
    }

    private void LoadCredentials()
    {
        string debugFolder = Path.Combine(Application.dataPath, "_DebugTokens");
        string sessionPath = Path.Combine(debugFolder, "session.dat");
        string playerDataPath = Path.Combine(debugFolder, "player_data.dat");
        string saltPath = Path.Combine(debugFolder, "jwt_salt.dat");

        try
        {
            byte[] salt = CryptoUtils.GetOrCreateSalt(saltPath);
            byte[] key = CryptoUtils.DeriveKey(DeviceUtils.GetDeviceId(), salt);

            if (File.Exists(sessionPath))
            {
                sessionId = File.ReadAllText(sessionPath);
            }
            else
            {
                Debug.LogError("Session file not found!");
            }

            if (File.Exists(playerDataPath))
            {
                byte[] encryptedData = File.ReadAllBytes(playerDataPath);
                string decryptedJson = CryptoUtils.DecryptAES(encryptedData, key);
                var persistentData = JsonConvert.DeserializeObject<PersistentPlayerData>(
                    decryptedJson
                );
                playerId = persistentData.userId;
            }
            else
            {
                Debug.LogError("Player data file not found!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load credentials: {e.Message}");
        }
    }

    public async void DisconnectWebSocket()
    {
        Debug.Log("PlayerController2D: Disconnecting WebSocket...");
        if (_playerWebSocketClient != null)
        {
            try
            {
                await _playerWebSocketClient.DisconnectAsync();
            }
            catch (System.Net.WebSockets.WebSocketException ex)
            {
                Debug.LogWarning(
                    $"PlayerController2D: Caught WebSocketException during DisconnectWebSocket: {ex.Message}"
                );
                // The WebSocket is likely already in an invalid state, so we just log and continue.
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"PlayerController2D: An unexpected error occurred during DisconnectWebSocket: {ex.Message}"
                );
            }
        }
    }

    // New method to handle deserialized PlayerPositionResponse
    private void HandlePlayerPositionResponse(PlayerPositionResponse response)
    {
        if (response != null && response.Status != null)
        {
            // Debug.Log(
            //     $"<color=cyan>Server ACK: X={response.X}, Y={response.Y}, Status='{response.Status}'</color>"
            // );
        }
    }
}
