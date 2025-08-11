using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

// using SharedLibrary.Requests;

[Serializable]
class PlayerPositionMessage
{
    public float X { get; set; }
    public float Y { get; set; }
}

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

    private string serverAddress = "localhost:5140"; // Set your server address
    private string sessionId;
    private string playerId;
    public float positionUpdateRate = 0.2f; // How often to send position data

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;
    private bool isMovementDisabled;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;

    private void Awake()
    {
        // Get the Rigidbody2D component attached to this GameObject.
        rb = GetComponent<Rigidbody2D>();

        // Find the "Move" action from the default input actions.
        // Using "Player/Move" is more specific in case you have other "Move" actions in other maps.
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    private async void Start()
    {
        LoadCredentials();
        await ConnectToServer();
    }

    private void OnEnable()
    {
        // Enable the move action when this component is enabled.
        moveAction.Enable();
        // Subscribe to the countdown finished event to stop movement.
        CountdownTimer.OnCountdownFinished += DisableMovement;
    }

    private async void OnDisable()
    {
        // Disable the move action when this component is disabled to prevent errors.
        moveAction.Disable();
        // Unsubscribe to prevent memory leaks.
        CountdownTimer.OnCountdownFinished -= DisableMovement;

        CancelInvoke(nameof(SendPosition));
        if (webSocket?.State == WebSocketState.Open)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Player leaving",
                CancellationToken.None
            );
        }
        cancellationTokenSource?.Cancel();
        webSocket?.Dispose();
    }

    private void Update()
    {
        if (isMovementDisabled)
        {
            moveInput = Vector2.zero;
            return;
        }

        // Read the input value from the "Move" action.
        // This returns a Vector2 with values from -1 to 1 for X and Y.
        moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Apply the movement to the Rigidbody2D in FixedUpdate for smooth physics.
        // We multiply the normalized input by the move speed.
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void DisableMovement()
    {
        isMovementDisabled = true;
        // Immediately stop the player's movement.
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

    /// <summary>
    /// Call this from your game manager or auth script after logging in.
    /// </summary>
    public async Task ConnectToServer()
    {
        Debug.Log("Attempting to connect to WebSocket server...");
        Debug.Log($"Server Address: {serverAddress}");
        Debug.Log($"Session ID: {sessionId}");
        Debug.Log($"Player ID: {playerId}");

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("SessionId or PlayerId is not set. Cannot connect to the server.");
            return;
        }

        webSocket = new ClientWebSocket();
        cancellationTokenSource = new CancellationTokenSource();

        try
        {
            Uri uri = new Uri($"ws://{serverAddress}/ws?sessionId={sessionId}");
            Debug.Log($"Connecting to URI: {uri}");
            await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);
            Debug.Log("Connection successful!");

            // Start sending position updates to the server
            InvokeRepeating(nameof(SendPosition), positionUpdateRate, positionUpdateRate);
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket connection failed: {e.Message}");
        }
    }

    /// <summary>
    /// Sends the player's current position to the server.
    /// </summary>
    private async void SendPosition()
    {
        if (webSocket?.State != WebSocketState.Open)
        {
            return; // Don't send if not connected
        }

        var positionMessage = new PlayerPositionMessage
        {
            X = transform.position.x,
            Y = transform.position.y,
        };

        var jsonMessage = JsonConvert.SerializeObject(positionMessage);
        var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage));

        try
        {
            await webSocket.SendAsync(
                bytesToSend,
                WebSocketMessageType.Text,
                true,
                cancellationTokenSource.Token
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send position: {e.Message}");
        }
    }
}
