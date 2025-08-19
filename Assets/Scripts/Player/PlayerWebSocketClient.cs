using System;
using System.Collections.Generic;
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
    [JsonProperty("request_type")]
    public string RequestType { get; set; }
    public string SessionId { get; set; }
    public string PlayerId { get; set; }
    public Position CurrentPosition { get; set; }
    public float Radius { get; set; }
    public DateTime LastSpawnAttempt { get; set; }
}

[Serializable]
public class PlayerPingResponse
{
    [JsonProperty("response_type")]
    public string ResponseType { get; set; }
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
    private Action<ClaimObjectResponse> _onClaimObjectResponse;
    private Action<string> _onError;
    private Action _onConnected;
    private Action _onDisconnected;
    private Dictionary<string, Action<string>> _responseHandlers;
    private TaskCompletionSource<bool> _connectionCompletion = new TaskCompletionSource<bool>();

    public PlayerWebSocketClient(
        string serverAddress,
        string sessionId,
        string playerId,
        float positionUpdateRate,
        float radius,
        DateTime lastSpawnAttempt,
        Action<PlayerPingResponse> onPlayerPingResponse,
        Action<SpawnItemResponse> onSpawnItemResponse,
        Action<ClaimObjectResponse> onClaimObjectResponse,
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
        _onClaimObjectResponse = onClaimObjectResponse;
        _onError = onError;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;

        _responseHandlers = new Dictionary<string, Action<string>>
        {
            {
                "spawn_request_response",
                (message) =>
                {
                    Debug.Log("Deserializing SpawnItemResponse...");
                    var response = JsonConvert.DeserializeObject<SpawnItemResponse>(message);
                    Debug.Log("Deserialization successful. Invoking callback...");
                    _onSpawnItemResponse?.Invoke(response);
                    Debug.Log("Callback invoked successfully.");
                }
            },
            {
                "player_ping_response",
                (message) =>
                {
                    var response = JsonConvert.DeserializeObject<PlayerPingResponse>(message);
                    _onPlayerPingResponse?.Invoke(response);
                }
            },
            {
                "object_claimed_response",
                (message) =>
                {
                    var response = JsonConvert.DeserializeObject<ClaimObjectResponse>(message);
                    _onClaimObjectResponse?.Invoke(response);
                }
            },
        };
    }

    public async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(_sessionId) || string.IsNullOrEmpty(_playerId))
        {
            _onError?.Invoke("SessionId or PlayerId is not set. Cannot connect to the server.");
            _connectionCompletion.TrySetResult(false);
            return;
        }

        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            Uri uri = new Uri($"wss://{_serverAddress}/ws?sessionId={_sessionId}");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            _onConnected?.Invoke();
            _connectionCompletion.TrySetResult(true);
            _ = ReceiveMessagesAsync(_cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            _onError?.Invoke($"WebSocket connection failed: {e.Message}");
            _connectionCompletion.TrySetResult(false);
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
        _connectionCompletion = new TaskCompletionSource<bool>();
    }

    private async Task SendRequestAsync<T>(T request)
    {
        if (!await _connectionCompletion.Task || _webSocket?.State != WebSocketState.Open)
        {
            _onError?.Invoke("WebSocket is not connected.");
            return;
        }

        var jsonMessage = JsonConvert.SerializeObject(request);
        var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage));

        try
        {
            Debug.Log($"PlayerWebSocketClient: Sending {typeof(T).Name}: {jsonMessage}");
            await _webSocket.SendAsync(
                bytesToSend,
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token
            );
        }
        catch (Exception e)
        {
            _onError?.Invoke($"Failed to send {typeof(T).Name}: {e.Message}");
        }
    }

    public async Task SendPositionAsync(Vector2 position, DateTime lastSpawnAttempt)
    {
        if (Time.time - _lastSendTime < _positionUpdateRate)
        {
            return;
        }
        _lastSendTime = Time.time;

        var playerPing = new PlayerPing
        {
            RequestType = "player_ping",
            SessionId = _sessionId,
            PlayerId = _playerId,
            CurrentPosition = new Position { X = position.x, Y = position.y },
            Radius = _radius,
            LastSpawnAttempt = lastSpawnAttempt,
        };
        await SendRequestAsync(playerPing);
    }

    public async Task SendSpawnRequestAsync(SpawnItemRequest request)
    {
        await SendRequestAsync(request);
    }

    public async Task SendClaimObjectRequestAsync(ClaimObjectRequest request)
    {
        await SendRequestAsync(request);
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
                        string responseType = jObject["response_type"]?.Value<string>();

                        if (_responseHandlers.TryGetValue(responseType, out var handler))
                        {
                            handler(message);
                        }
                        else
                        {
                            Debug.LogWarning($"Unknown response type: {responseType}");
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
