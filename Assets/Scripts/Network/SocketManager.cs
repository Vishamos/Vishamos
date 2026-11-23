using System;
using System.Text;
using System.Threading;
using System.Net.WebSockets;
using UnityEngine;


public class SocketManager : SingletonMonoBehaviour<SocketManager>
{
    private ClientWebSocket client;

    protected override void OnStart()
    {
        _ = RunAsync();
    }

    private async Awaitable RunAsync()
    {
        while (!destroyCancellationToken.IsCancellationRequested) //유니티 제공 토큰
        {
            client = new ClientWebSocket();

            try
            {
                byte[] bytes;
                string json;
                Uri uri = new Uri($"ws://{FileIO.GetServer}:{FileIO.GetPort}/");
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken)) //매 시도시 생성해야할 토큰
                {
                    cts.CancelAfter(5000);
                    await client.ConnectAsync(uri, cts.Token);
                    SynapseProtocol protocol = new SynapseProtocol { to = 0, from = 11, type = "client.register", data = "" };
                    json = protocol.ToJson();
                    bytes = Encoding.UTF8.GetBytes(json);
                }

                await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, destroyCancellationToken);
                Debug.Log($"연결 체크 / {json}");
                await ReceiveLoopAsync(); //루프가 종료될 때 까지 기다려라.
            }
            catch (OperationCanceledException) when (destroyCancellationToken.IsCancellationRequested) //제네릭의 where과 유사하나 swtich문의 위치 이동과 더 가까움. 
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
            finally
            {
                client.Dispose();
                BodyReceiver.Instance.OnDisconnected();
            }

            try
            {
                Debug.Log("연결이 끊겨 3초 후 재접속을 시도합니다.");
                await Awaitable.WaitForSecondsAsync(3f, destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Awaitable ReceiveLoopAsync()
    {
        byte[] buffer = new byte[65536];
        while (client.State == WebSocketState.Open)
        {
            int offset = 0;
            WebSocketReceiveResult result;
            do
            {
                result = await client.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), destroyCancellationToken);
                offset += result.Count;
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (result.MessageType == WebSocketMessageType.Text)
                Debug.Log($"Receive : {Encoding.UTF8.GetString(buffer, 0, offset)}");
            else
                HandleBinary(buffer, offset);
        }
    }

    private void HandleBinary(byte[] buffer, int length)
    {
        if (length < 4) return;
        switch (buffer[1])
        {
            case 31:
                try
                {
                    BodyReceiver.Instance.Receive(buffer, length);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message);
                }
                break;
            case 22:
                break;
            default:
                Debug.Log($"Receive : {length} / to {buffer[0]} from {buffer[1]} type {buffer[2]} data {buffer[3]}");
                break;
        }

    }
}