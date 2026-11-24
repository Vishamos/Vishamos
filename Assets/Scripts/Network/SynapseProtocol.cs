using System;
using UnityEngine;

[Serializable]
public struct SynapseProtocol
{
    public int to;
    public int from;
    public string type;
    public string data;

    public string ToJson() => JsonUtility.ToJson(this);
}

// 서버가 보내는 JSON(Synapse §5). data가 객체라 보내는 형식과 따로 둔다
[Serializable]
public class SynapseIncoming
{
    public int to;
    public int from;
    public string type;
    public SynapseIncomingData data;
}

[Serializable]
public class SynapseIncomingData
{
    public int index;
    public int code;
    public string message;
}
