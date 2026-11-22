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