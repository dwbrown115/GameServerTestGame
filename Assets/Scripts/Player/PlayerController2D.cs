using System;
using System.IO;
using System.Threading.Tasks;
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
    private bool _isGameOver = false;
    private TaskCompletionSource<bool> _lastPingResponseReceived = new TaskCompletionSource<bool>();

    private PlayerWebSocketClient _playerWebSocketClient;
    public MonoBehaviour spawnResponseHandler; // Assign a component that implements ISpawnItemResponseHandler

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    private async void Start()
    {
        // Apply saved color to the player's sprite if available
        TryApplySavedColor();
        LoadCredentials();
        System.Action<SpawnItemResponse> onSpawn = null;

        // Auto-wire a handler if none assigned (find any component with HandleSpawnResponse)
        if (spawnResponseHandler == null)
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                var mbType = mb.GetType();
                var miCheck = mbType.GetMethod(
                    "HandleSpawnResponse",
                    new Type[] { typeof(SpawnItemResponse) }
                );
                if (miCheck != null)
                {
                    spawnResponseHandler = mb;
                    Debug.Log(
                        $"Auto-wired spawnResponseHandler to {mb.GetType().Name} on {mb.gameObject.name}"
                    );
                    break;
                }
            }
        }
        if (spawnResponseHandler != null)
        {
            var type = spawnResponseHandler.GetType();
            var mi = type.GetMethod(
                "HandleSpawnResponse",
                new Type[] { typeof(SpawnItemResponse) }
            );
            if (mi != null)
            {
                onSpawn = (SpawnItemResponse r) =>
                {
                    mi.Invoke(spawnResponseHandler, new object[] { r });
                };
            }
            else
            {
                Debug.LogWarning(
                    $"spawnResponseHandler set to {type.Name} but no HandleSpawnResponse(SpawnItemResponse) method was found."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "No spawnResponseHandler assigned or auto-wired. Spawns will not be handled."
            );
        }

        if (!GameMode.Offline)
        {
            _playerWebSocketClient = new PlayerWebSocketClient(
                serverAddress,
                sessionId,
                playerId,
                positionUpdateRate,
                playerRadius,
                _lastSpawnAttempt,
                HandlePlayerPingResponse,
                onSpawn,
                HandleClaimObjectResponse,
                (error) => Debug.LogError($"PlayerWebSocketClient Error: {error}"),
                () => Debug.Log("PlayerWebSocketClient: Connected!"),
                () => Debug.Log("PlayerWebSocketClient: Disconnected!")
            );
            ValidatedObjectsManager.Initialize(_playerWebSocketClient);
            await _playerWebSocketClient.ConnectAsync();
        }
        else
        {
            ValidatedObjectsManager.Initialize(null);
        }
    }

    private void TryApplySavedColor()
    {
        try
        {
            string hex =
                PlayerManager.Instance != null ? PlayerManager.Instance.GetSavedSkinHex() : null;
            if (!string.IsNullOrEmpty(hex))
            {
                var sr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    if (ColorUtils.TryApply(sr, hex))
                    {
                        Debug.Log($"Applied player color from saved hex {hex}.");
                    }
                    else
                    {
                        Debug.LogWarning($"TryApplySavedColor: Invalid hex format: {hex}");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"TryApplySavedColor: No SpriteRenderer found on player or children. Hex={hex}"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"TryApplySavedColor failed: {ex.Message}");
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
        GameOverController.OnCountdownFinished += DisableMovement;
    }

    private async void OnDisable()
    {
        moveAction.Disable();
        GameOverController.OnCountdownFinished -= DisableMovement;

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
        if (_isGameOver)
            return;

        rb.linearVelocity = moveInput * moveSpeed;
        if (!GameMode.Offline && _playerWebSocketClient != null)
        {
            _ = _playerWebSocketClient.SendPositionAsync(transform.position, _lastSpawnAttempt);
        }
    }

    private void DisableMovement()
    {
        _isGameOver = true;
        isMovementDisabled = true;
        rb.linearVelocity = Vector2.zero;
        _ = DisconnectOnGameOver();
    }

    private async Task DisconnectOnGameOver()
    {
        if (_playerWebSocketClient != null)
        {
            await _playerWebSocketClient.SendFinalPingAsync(transform.position, _lastSpawnAttempt);
            await _lastPingResponseReceived.Task;
            await _playerWebSocketClient.DisconnectAsync();
        }
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
        if (response != null && response.Status == "Bad")
        {
            Debug.LogWarning(
                $"Score mismatch detected. Client score will be updated to server's score."
            );
            PlayerPrefs.SetInt("PlayerScore", response.ServerScore);
            PlayerPrefs.Save();
            TryInvokeScoreChanged(response.ServerScore);
        }

        if (_isGameOver)
        {
            _lastPingResponseReceived.TrySetResult(true);
        }
    }

    private void HandleClaimObjectResponse(ClaimObjectResponse response)
    {
        if (response != null && response.Status == "Ok")
        {
            Debug.Log(
                $"<color=green>Claim Object Response: {JsonConvert.SerializeObject(response)}</color>"
            );

            int currentScore = PlayerPrefs.GetInt("PlayerScore", 0);
            int newScore = currentScore + 1;
            PlayerPrefs.SetInt("PlayerScore", newScore);
            PlayerPrefs.Save();

            TryInvokeScoreChanged(newScore);
        }
    }

    public void SetLastSpawnAttempt(DateTime spawnTime)
    {
        _lastSpawnAttempt = spawnTime;
    }

    // Use reflection to avoid a hard compile-time dependency on Collectible while still
    // notifying any listeners if that class exists in this build.
    private void TryInvokeScoreChanged(int newScore)
    {
        try
        {
            Type targetType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                targetType = asm.GetType("Collectible");
                if (targetType != null)
                {
                    break;
                }
            }
            if (targetType == null)
            {
                return;
            }

            var mi = targetType.GetMethod(
                "InvokeOnScoreChanged",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );
            mi?.Invoke(null, new object[] { newScore });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"TryInvokeScoreChanged failed: {ex.Message}");
        }
    }

    public void RequestSpawn(float spawnRadius)
    {
        if (_isGameOver)
            return;

        var request = new SpawnItemRequest
        {
            RequestType = "spawn_item_request",
            SessionId = sessionId,
            PlayerPosition = new Position { X = transform.position.x, Y = transform.position.y },
            SpawnAttemptTimestamp = DateTime.UtcNow,
            SpawnRadius = spawnRadius,
        };
        if (!GameMode.Offline)
        {
            _ = _playerWebSocketClient.SendSpawnRequestAsync(request);
        }
        else
        {
            var offline = FindAnyObjectByType<OfflineSpawnService>();
            if (offline == null)
            {
                var go = new GameObject("OfflineSpawnService");
                offline = go.AddComponent<OfflineSpawnService>();
            }
            offline.RequestSpawn(request);
        }
    }
}
