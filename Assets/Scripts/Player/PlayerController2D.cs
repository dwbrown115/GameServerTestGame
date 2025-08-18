using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Tooltip("The speed at which the player moves.")]
    [SerializeField]
    private float moveSpeed = 5f;

    private string serverAddress = "localhost:7123";
    private string sessionId;
    private string playerId;
    public float positionUpdateRate = 0.2f;
    public float playerRadius = 5.0f;
    private DateTime _lastSpawnAttempt = DateTime.MinValue;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;
    private bool isMovementDisabled;

    private PlayerWebSocketClient _playerWebSocketClient;
    public PrefabSpawner prefabSpawner; // Reference to PrefabSpawner

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    private async void Start()
    {
        LoadCredentials();
        _playerWebSocketClient = new PlayerWebSocketClient(
            serverAddress,
            sessionId,
            playerId,
            positionUpdateRate,
            playerRadius,
            _lastSpawnAttempt,
            HandlePlayerPingResponse,
            prefabSpawner.HandleSpawnResponse, // Pass the handler from PrefabSpawner
            (error) => Debug.LogError($"PlayerWebSocketClient Error: {error}"),
            () => Debug.Log("PlayerWebSocketClient: Connected!"),
            () => Debug.Log("PlayerWebSocketClient: Disconnected!")
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

        if (_playerWebSocketClient != null)
        {
            ValidatedObjectsManager.DeleteFile();
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
        _ = _playerWebSocketClient.SendPositionAsync(transform.position, _lastSpawnAttempt);
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
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"PlayerController2D: An unexpected error occurred during DisconnectWebSocket: {ex.Message}"
                );
            }
        }
    }

    private void HandlePlayerPingResponse(PlayerPingResponse response)
    {
        if (response != null && response.Status != null)
        {
            Debug.Log(
                $"<color=cyan>Server Response: {JsonConvert.SerializeObject(response)}</color>"
            );
        }
    }

    public void SetLastSpawnAttempt(DateTime spawnTime)
    {
        _lastSpawnAttempt = spawnTime;
    }

    public void RequestSpawn(float spawnRadius)
    {
        var request = new SpawnItemRequest
        {
            SessionId = sessionId,
            PlayerPosition = new Position { X = transform.position.x, Y = transform.position.y },
            SpawnAttemptTimestamp = DateTime.UtcNow,
            SpawnRadius = spawnRadius,
        };
        _ = _playerWebSocketClient.SendSpawnRequestAsync(request);
    }
}
