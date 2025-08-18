using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Data models for player position messages
[Serializable]
public class PlayerPing
{
    public string SessionId { get; set; }
    public string PlayerId { get; set; }
    public Position CurrentPosition { get; set; }
    public float Radius { get; set; }
    public DateTime LastSpawnAttempt { get; set; }
}

[Serializable]
public class PlayerPingResponse
{
    public string SessionId { get; set; }
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
    private float _lastSendTime;
    private float _radius;
    private DateTime _lastSpawnAttempt;
    private Action<PlayerPingResponse> _onPlayerPingResponse;
    private Action<SpawnItemResponse> _onSpawnItemResponse;
    private Action<string> _onError;
    private Action _onConnected;
    private Action _onDisconnected;

    public PlayerWebSocketClient(
        string serverAddress,
        string sessionId,
        string playerId,
        float positionUpdateRate,
        float radius,
        DateTime lastSpawnAttempt,
        Action<PlayerPingResponse> onPlayerPingResponse,
        Action<SpawnItemResponse> onSpawnItemResponse,
        Action<string> onError,
        Action onConnected,
        Action onDisconnected
    )
    {
        _serverAddress = serverAddress;
        _sessionId = sessionId;
        _playerId = playerId;
        _positionUpdateRate = positionUpdateRate;
        _radius = radius;
        _lastSpawnAttempt = lastSpawnAttempt;
        _onPlayerPingResponse = onPlayerPingResponse;
        _onSpawnItemResponse = onSpawnItemResponse;
        _onError = onError;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
    }

    public async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(_sessionId) || string.IsNullOrEmpty(_playerId))
        {
            _onError?.Invoke("SessionId or PlayerId is not set. Cannot connect to the server.");
            return;
        }

        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            Uri uri = new Uri($"wss://{_serverAddress}/ws?sessionId={_sessionId}");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            _onConnected?.Invoke();
            _ = ReceiveMessagesAsync(_cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            _onError?.Invoke($"WebSocket connection failed: {e.Message}");
            _webSocket?.Dispose();
        }
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket != null)
        {
            if (
                _webSocket.State == WebSocketState.Open
                || _webSocket.State == WebSocketState.CloseReceived
                || _webSocket.State == WebSocketState.CloseSent
            )
            {
                _cancellationTokenSource?.Cancel();
                await _webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client leaving",
                    CancellationToken.None
                );
                try
                {
                    var buffer = new byte[1024];
                    await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );
                }
                catch (WebSocketException) { }
                catch (Exception) { }

                if (_webSocket.State != WebSocketState.Closed)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client exiting",
                        CancellationToken.None
                    );
                }
            }
            _onDisconnected?.Invoke();
            _webSocket?.Dispose();
        }
    }

    public async Task SendPositionAsync(Vector2 position, DateTime lastSpawnAttempt)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            return;
        }

        if (Time.time - _lastSendTime < _positionUpdateRate)
        {
            return;
        }

        _lastSendTime = Time.time;

        var playerPing = new PlayerPing
        {
            SessionId = _sessionId,
            PlayerId = _playerId,
            CurrentPosition = new Position { X = position.x, Y = position.y },
            Radius = _radius,
            LastSpawnAttempt = lastSpawnAttempt,
        };

        var jsonMessage = JsonConvert.SerializeObject(playerPing);
        var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage));

        try
        {
            Debug.Log($"PlayerWebSocketClient: Sending PlayerPing: {jsonMessage}");
            await _webSocket.SendAsync(
                bytesToSend,
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
        }
        catch (Exception e)
        {
            _onError?.Invoke($"Failed to send position: {e.Message}");
        }
    }

    public async Task SendSpawnRequestAsync(SpawnItemRequest request)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            _onError?.Invoke("WebSocket is not connected.");
            return;
        }

        var jsonMessage = JsonConvert.SerializeObject(request);
        var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage));

        try
        {
            Debug.Log($"PlayerWebSocketClient: Sending SpawnItemRequest: {jsonMessage}");
            await _webSocket.SendAsync(
                bytesToSend,
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
        }
        catch (Exception e)
        {
            _onError?.Invoke($"Failed to send spawn request: {e.Message}");
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
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
                        result = await _webSocket.ReceiveAsync(buffer, token);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        string message = await reader.ReadToEndAsync();
                        Debug.Log($"PlayerWebSocketClient: Full message received: {message}");

                        var jObject = JObject.Parse(message);
                        if (jObject.ContainsKey("Granted"))
                        {
                            try
                            {
                                Debug.Log("Deserializing SpawnItemResponse...");
                                var response = JsonConvert.DeserializeObject<SpawnItemResponse>(
                                    message
                                );
                                Debug.Log("Deserialization successful. Invoking callback...");
                                _onSpawnItemResponse?.Invoke(response);
                                Debug.Log("Callback invoked successfully.");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError(
                                    $"Error processing SpawnItemResponse: {ex.ToString()}"
                                );
                            }
                        }
                        else
                        {
                            var response = JsonConvert.DeserializeObject<PlayerPingResponse>(
                                message
                            );
                            _onPlayerPingResponse?.Invoke(response);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _onError?.Invoke($"Error receiving message: {e.Message}");
                break;
            }
        }
        _onDisconnected?.Invoke();
    }
}
