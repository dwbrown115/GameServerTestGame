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
public class PlayerPositionResponse
{
    public float X { get; set; }
    public float Y { get; set; }
    public string Status { get; set; }
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

    private string serverAddress = "localhost:7123"; // Set your server address
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
        rb = GetComponent<Rigidbody2D>();
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    private async void Start()
    {
        LoadCredentials();
        await ConnectToServer();
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

        CancelInvoke(nameof(SendPosition));
        if (webSocket?.State == WebSocketState.Open)
        {
            // Use the CancellationToken from the source that was used for the connection
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Player leaving", cancellationTokenSource.Token);
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
        moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
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

    public async Task ConnectToServer()
    {
        Debug.Log("Attempting to connect to WebSocket server...");
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("SessionId or PlayerId is not set. Cannot connect to the server.");
            return;
        }

        webSocket = new ClientWebSocket();
        cancellationTokenSource = new CancellationTokenSource();

        try
        {
            Uri uri = new Uri($"wss://{serverAddress}/ws?sessionId={sessionId}");
            Debug.Log($"Connecting to URI: {uri}");
            await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);
            Debug.Log("Connection successful!");

            _ = ReceiveMessages(cancellationTokenSource.Token);
            InvokeRepeating(nameof(SendPosition), positionUpdateRate, positionUpdateRate);
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket connection failed: {e.Message}");
            webSocket?.Dispose();
        }
    }

    private async void SendPosition()
    {
        if (webSocket?.State != WebSocketState.Open)
        {
            return;
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
            await webSocket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send position: {e.Message}");
        }
    }

    private async Task ReceiveMessages(CancellationToken token)
    {
        var buffer = new ArraySegment<byte>(new byte[2048]);
        while (!token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await webSocket.ReceiveAsync(buffer, token);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        string message = await reader.ReadToEndAsync();
                        HandleServerMessage(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error receiving message: {e.Message}");
                break;
            }
        }
    }

    private void HandleServerMessage(string jsonMessage)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<PlayerPositionResponse>(jsonMessage);
            if (response != null && response.Status != null)
            {
                Debug.Log(
                    $"<color=cyan>Server ACK: X={response.X}, Y={response.Y}, Status='{response.Status}'</color>"
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not process server message: {jsonMessage}. Error: {e.Message}");
        }
    }
}