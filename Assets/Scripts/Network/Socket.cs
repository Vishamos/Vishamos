using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using UnityEngine;
using System.Threading.Tasks;
using System.Linq;


public class Socket : MonoBehaviour
{
    private ClientWebSocket client;
    private CancellationTokenSource cts;

    Coroutine ReconectCoroutine;

    bool StartServer = true;
    public bool hasShakeHand = false;

    private void Start()
    {
        ConnectToServer();
    }
    private void Update()
    {
        if (client == null) return;
        if (StartServer &&
            client.State == WebSocketState.Closed &&
            ReconectCoroutine == null)
        {
            ReconectCoroutine = StartCoroutine(ReconnectServer());
        }
        if (client.State == WebSocketState.Closed)
        {
            Disconnect();
            ReconectCoroutine = StartCoroutine(ReconnectServer());
        }
    }

    private async void ConnectToServer()
    {
        if (ReconectCoroutine != null)
        {
            StopCoroutine(ReconectCoroutine);
            ReconectCoroutine = null;
        }
        client = new ClientWebSocket();
        cts = new CancellationTokenSource();
        try
        {
            await client.ConnectAsync(new Uri("ws://192.168.100.3:4000/ws/"), cts.Token);
            Debug.Log("Try connect to VishaSynapse");

            ReceiveMessage();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"VishasFace WebSocket connection error : {e.Message}");
        }
    }


    private async void ReceiveMessage()
    {
        var buffer = new byte[1024];
        while (client.State == WebSocketState.Open)
        {
            try
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Debug.Log("WebSocket closed by VishasSynapse.");
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log($"Message from VishasSynapse: {message}");
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    byte[] receivedData = buffer.Take(result.Count).ToArray();
                    Debug.Log("receive binary message : " + receivedData[0].ToString() + receivedData[1].ToString() + " / ");
                    if (receivedData[0] == 0x00 && receivedData[1] == 0x00)
                    {
                        byte[] initbyte = MessagePacker.InitSocketBytes();
                        SendBinaryData(initbyte);
                    }
                    else if (receivedData[0] == 0x00 && receivedData[1] == 0x01)
                    {
                        hasShakeHand = true;
                    }
                    else if (receivedData[0] == 0x0a)
                    {
                        Debug.Log(receivedData.Length);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Receive error to VishasSynapse : {e.Message}");
                break;
            }
        }
    }
    public async void SendJsonMessage(string message)
    {
        if (client?.State == WebSocketState.Open)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"Vishamos Sending message {message}");
        }
        
    }
    public async void SendBinaryData(byte[] data)
    {
        if (client?.State == WebSocketState.Open)
        {
            try
            {
                await client.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
                Debug.Log($"Vishamos Sending message {data}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"VishasFace Error while Sending data {e}");
            }
        }
    }
    public void Disconnect()
    {
        if (client != null)
        {
            client.Abort();
            client.Dispose();
            client = null;
            hasShakeHand = false;
            Debug.Log("Disconnect from VishasCharacter Server.");
        }
    }

    IEnumerator ReconnectServer()
    {
        while (true)
        {
            yield return new WaitForSeconds(10);
            Disconnect();
            ConnectToServer();
        }
    }
}
