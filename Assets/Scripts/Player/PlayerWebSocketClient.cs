using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

// Data models for player position messages
[Serializable]
public class PlayerPositionMessage
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

public class PlayerWebSocketClient
{
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private string _serverAddress;
    private string _sessionId;
    private string _playerId;
    private float _positionUpdateRate;
    private Action<PlayerPositionResponse> _onMessageReceived; // Changed to PlayerPositionResponse
    private Action<string> _onError;
    private Action _onConnected;
    private Action _onDisconnected;

    public PlayerWebSocketClient(
        string serverAddress,
        string sessionId,
        string playerId,
        float positionUpdateRate,
        Action<PlayerPositionResponse> onMessageReceived, // Changed to PlayerPositionResponse
        Action<string> onError,
        Action onConnected,
        Action onDisconnected
    )
    {
        _serverAddress = serverAddress;
        _sessionId = sessionId;
        _playerId = playerId;
        _positionUpdateRate = positionUpdateRate;
        _onMessageReceived = onMessageReceived;
        _onError = onError;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
    }

    public async Task ConnectAsync()
    {
        // Debug.Log("PlayerWebSocketClient: Attempting to connect to WebSocket server...");
        if (string.IsNullOrEmpty(_sessionId) || string.IsNullOrEmpty(_playerId))
        {
            _onError?.Invoke("SessionId or PlayerId is not set. Cannot connect to the server.");
            // Debug.LogWarning(
            // "PlayerWebSocketClient: Connection aborted due to missing credentials."
            // );
            return;
        }

        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            Uri uri = new Uri($"wss://{_serverAddress}/ws?sessionId={_sessionId}");
            // Debug.Log($"PlayerWebSocketClient: Connecting to URI: {uri}");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            // Debug.Log("PlayerWebSocketClient: Connection successful!");
            _onConnected?.Invoke();
            // Debug.Log("PlayerWebSocketClient: Starting message receiver.");

            _ = ReceiveMessagesAsync(_cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            _onError?.Invoke($"WebSocket connection failed: {e.Message}");
            // Debug.LogError($"PlayerWebSocketClient: Connection failed with exception: {e.Message}");
            _webSocket?.Dispose();
        }
    }

    public async Task DisconnectAsync()
    {
        // Debug.Log("PlayerWebSocketClient: Attempting to disconnect WebSocket.");
        if (_webSocket != null)
        {
            if (
                _webSocket.State == WebSocketState.Open
                || _webSocket.State == WebSocketState.CloseReceived
                || _webSocket.State == WebSocketState.CloseSent
            )
            {
                // Debug.Log(
                // $"PlayerWebSocketClient: Closing WebSocket from state '{_webSocket.State}'."
                // );
                _cancellationTokenSource?.Cancel();

                // Initiate graceful close from client side
                // Debug.Log("PlayerWebSocketClient: Initiating graceful WebSocket close...");
                await _webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client leaving",
                    CancellationToken.None
                );

                // Wait for the server to acknowledge the close
                try
                {
                    var buffer = new byte[1024];
                    var receiveResult = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        // Debug.Log($"PlayerWebSocketClient: Server acknowledged close. Status: {receiveResult.CloseStatus}, Description: {receiveResult.CloseStatusDescription}");
                    }
                }
                catch (WebSocketException ex)
                {
                    // Debug.LogWarning($"PlayerWebSocketClient: Caught WebSocketException while waiting for server close: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Debug.LogError($"PlayerWebSocketClient: An unexpected error occurred while waiting for server close: {ex.Message}");
                }

                // Ensure the WebSocket is fully closed
                if (_webSocket.State != WebSocketState.Closed)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client exiting",
                        CancellationToken.None
                    );
                }
                // Debug.Log("PlayerWebSocketClient: WebSocket closed gracefully.");
            }
            else if (
                _webSocket.State == WebSocketState.Aborted
                || _webSocket.State == WebSocketState.Closed
            )
            {
                // Debug.LogWarning(
                // $"PlayerWebSocketClient: WebSocket is already in state '{_webSocket.State}'. Disposing without calling CloseAsync."
                // );
            }
            else
            {
                // Debug.LogWarning(
                // $"PlayerWebSocketClient: WebSocket in unexpected state '{_webSocket.State}'. Disposing."
                // );
            }

            _onDisconnected?.Invoke();
            _webSocket?.Dispose();
            // Debug.Log("PlayerWebSocketClient: WebSocket disposed.");
        }
        else
        {
            // Debug.LogWarning(
            // "PlayerWebSocketClient: DisconnectAsync called but _webSocket is null."
            // );
        }
    }

    public async Task SendPositionAsync(Vector2 position)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            // // Debug.LogWarning(
            //     $"PlayerWebSocketClient: Not sending position. WebSocket state is '{_webSocket?.State}'."
            // );
            return;
        }

        var positionMessage = new PlayerPositionMessage { X = position.x, Y = position.y };

        var jsonMessage = JsonConvert.SerializeObject(positionMessage);
        var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage));

        try
        {
            // // Debug.Log($"PlayerWebSocketClient: Sending position: {jsonMessage}");
            await _webSocket.SendAsync(
                bytesToSend,
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
            // // Debug.Log("PlayerWebSocketClient: Position sent successfully.");
        }
        catch (Exception e)
        {
            _onError?.Invoke($"Failed to send position: {e.Message}");
            // Debug.LogError(
            // $"PlayerWebSocketClient: Failed to send position with exception: {e.Message}"
            // );
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
        // Debug.Log("PlayerWebSocketClient: Message receiver started.");
        var buffer = new ArraySegment<byte>(new byte[2048]);
        while (!token.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        // Debug.Log("PlayerWebSocketClient: Waiting to receive message part.");
                        result = await _webSocket.ReceiveAsync(buffer, token);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                        // Debug.Log(
                        // $"PlayerWebSocketClient: Received {result.Count} bytes. EndOfMessage: {result.EndOfMessage}"
                        // );
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Debug.Log("PlayerWebSocketClient: Received close message from server.");
                        break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        string message = await reader.ReadToEndAsync();
                        // // Debug.Log($"PlayerWebSocketClient: Full message received: {message}");
                        // Deserialize before invoking the callback
                        var response = JsonConvert.DeserializeObject<PlayerPositionResponse>(
                            message
                        );
                        _onMessageReceived?.Invoke(response);
                        // // Debug.Log("PlayerWebSocketClient: Message processed and callback invoked.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Debug.Log("PlayerWebSocketClient: Message reception cancelled.");
                break;
            }
            catch (Exception e)
            {
                _onError?.Invoke($"Error receiving message: {e.Message}");
                // Debug.LogError($"PlayerWebSocketClient: Error receiving message: {e.Message}");
                break;
            }
        }
        // Debug.Log("PlayerWebSocketClient: Message receiver stopped.");
        _onDisconnected?.Invoke();
    }
}
