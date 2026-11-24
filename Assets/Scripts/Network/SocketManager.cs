using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;


public class SocketManager : SingletonMonoBehaviour<SocketManager>
{
    public enum EState { Disconnected, Connecting, Registering, Connected }

    private const int MySignature = 11;
    private const int MaxMessageBytes = 65536;
    private const int ConnectTimeoutMs = 5000;
    private const int CloseTimeoutMs = 1000;

    public EState State { get; private set; } = EState.Disconnected;

    private ClientWebSocket client;
    private CancellationTokenSource lifeCts;
    private readonly byte[] buffer = new byte[MaxMessageBytes];
    private readonly HashSet<int> loggedUnknownFrom = new HashSet<int>();
    private readonly HashSet<int> loggedReceiveError = new HashSet<int>();

    protected override void OnStart()
    {
        if (!FileIO.HasServer)
            return; // FileIO가 에러 로그를 남김

        lifeCts = new CancellationTokenSource();
        _ = RunAsync(lifeCts.Token);
    }

    private void OnDestroy()
    {
        if (lifeCts == null)
            return;

        // 3.5: 정상 종료(1000)를 최대 1초 보낸 뒤 수신 대기를 취소한다.
        // 메인 스레드에서 기다리므로 Task.Run으로 동기화 컨텍스트를 피한다(교착 방지)
        ClientWebSocket ws = client;
        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                Task.Run(() => ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None))
                    .Wait(CloseTimeoutMs);
            }
            catch (Exception) { }
        }

        lifeCts.Cancel(); // Dispose하지 않는다: 남은 await가 이 토큰을 확인한 뒤 끝나야 함
        lifeCts = null;
    }

    private async Awaitable RunAsync(CancellationToken token)
    {
        Uri uri = new Uri($"ws://{FileIO.GetServer}:{FileIO.GetPort}/");
        byte[] register = Encoding.UTF8.GetBytes(
            new SynapseProtocol { to = 0, from = MySignature, type = "client.register", data = "" }.ToJson());

        while (!token.IsCancellationRequested)
        {
            client = new ClientWebSocket();
            string reason = null;

            try
            {
                SetState(EState.Connecting);
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    cts.CancelAfter(ConnectTimeoutMs);
                    try
                    {
                        await client.ConnectAsync(uri, cts.Token);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        throw new TimeoutException($"접속 시간 초과({ConnectTimeoutMs / 1000}초)");
                    }
                }

                SetState(EState.Registering);
                await client.SendAsync(new ArraySegment<byte>(register), WebSocketMessageType.Text, true, token);
                await ReceiveLoopAsync(token);
                if (token.IsCancellationRequested)
                    return; // 종료 신호를 보낸 뒤 수신 루프가 정상으로 빠져나온 경우

                reason = client.CloseStatus.HasValue
                    ? $"{(int)client.CloseStatus.Value} {client.CloseStatusDescription}"
                    : "종료 신호 없음";
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return; // 3.5c: 종료 중에는 로그를 남기지 않는다
            }
            catch (Exception e)
            {
                reason = e.Message;
            }
            finally
            {
                client.Dispose();
                client = null;
                if (BodyReceiver.Instance)
                    BodyReceiver.Instance.OnDisconnected();
                if (FaceReceiver.Instance)
                    FaceReceiver.Instance.OnDisconnected();
            }

            SetState(EState.Disconnected, reason);

            try
            {
                await Awaitable.WaitForSecondsAsync(FileIO.GetReconnectSeconds, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Awaitable ReceiveLoopAsync(CancellationToken token)
    {
        while (client.State == WebSocketState.Open)
        {
            int offset = 0;
            bool overflow = false;
            ValueWebSocketReceiveResult result;
            do
            {
                // 3.4c: 64KB를 넘으면 나머지는 버퍼 앞에 덮어쓰며 끝까지 읽고 메시지를 버린다
                if (offset == buffer.Length)
                {
                    overflow = true;
                    offset = 0;
                }
                result = await client.ReceiveAsync(buffer.AsMemory(offset), token);
                offset += result.Count;
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (overflow)
            {
                Debug.LogWarning($"[Synapse] {MaxMessageBytes / 1024}KB를 넘는 메시지를 버렸습니다.");
                continue;
            }

            if (result.MessageType == WebSocketMessageType.Text)
                HandleText(offset);
            else if (State == EState.Connected) // 3.2c
                HandleBinary(offset);
        }
    }

    private void HandleText(int length)
    {
        SynapseIncoming msg;
        try
        {
            msg = JsonUtility.FromJson<SynapseIncoming>(Encoding.UTF8.GetString(buffer, 0, length));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Synapse] JSON을 읽지 못했습니다: {e.Message}");
            return;
        }

        switch (msg?.type)
        {
            case "client.registered":
                SetState(EState.Connected);
                break;
            case "error":
                Debug.LogWarning($"[Synapse] 에러 {msg.data?.code} {msg.data?.message}");
                break;
            default:
                Debug.Log($"[Synapse] 처리하지 않는 메시지: {msg?.type} (from {msg?.from})");
                break;
        }
    }

    private void HandleBinary(int length)
    {
        if (length < 4)
            return;

        int from = buffer[1];
        try
        {
            switch (from)
            {
                case 31:
                    if (BodyReceiver.Instance)
                        BodyReceiver.Instance.Receive(buffer, length);
                    break;
                case 22:
                    if (FaceReceiver.Instance)
                        FaceReceiver.Instance.Receive(buffer, length);
                    break;
                default:
                    if (loggedUnknownFrom.Add(from)) // 4.1c: 송신원마다 처음 한 번
                        Debug.LogWarning($"[Synapse] 버린 메시지: from {from} 종류 {buffer[2]} 버전 {buffer[3]} 길이 {length}");
                    break;
            }
        }
        catch (Exception e)
        {
            if (loggedReceiveError.Add(from))
                Debug.LogWarning($"[Synapse] from {from} 해석 중 예외(처음 한 번만 표시): {e}");
        }
    }

    private void SetState(EState next, string reason = null)
    {
        if (State == next)
            return;
        Debug.Log($"[Synapse] {Label(State)} → {Label(next)}{(reason != null ? $" ({reason})" : "")}");
        State = next;
    }

    private static string Label(EState s) => s switch
    {
        EState.Disconnected => "연결 안 됨",
        EState.Connecting => "접속 중",
        EState.Registering => "등록 대기",
        _ => "연결됨",
    };
}
