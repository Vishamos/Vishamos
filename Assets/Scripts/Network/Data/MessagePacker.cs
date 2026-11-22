using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public enum EFaceData
{
    None = 0,
    Lip_Up,
    Lip_Down,
    Lip_L_Corner ,//왼쪽 입꼬리
    Lip_R_Corner,
    Glabella  , //미간
    Eye_L_End,  //왼쪽
    Eye_R_End,
    max
}
public struct FaceData_Adroid
{
    Vector3 FacePos;
    Quaternion FaceRot;
    Vector3 vertexPos;
}
static class MessagePacker
{
    public static byte[] InitSocketBytes()
    {
        byte[] bytes = new byte[3];
        byte TargetType = 0x00;
        byte messageType = 0x01;
        byte initType = 0x0a; //10 - Vishamos
        Buffer.BlockCopy(BitConverter.GetBytes(TargetType), 0, bytes, 0, 1);
        Buffer.BlockCopy(BitConverter.GetBytes(messageType), 0, bytes, 1, 1);
        Buffer.BlockCopy(BitConverter.GetBytes(initType), 0, bytes, 2, 1);
        return bytes;
    }

    public static List<Vector3> ConvertBytesToVector3(byte[] bytes) //메세지 수신 이후 처리? 이건 본진쪽으로.
    {
        int defaultSize = 3;
        List<Vector3> v = new List<Vector3>();
        if (bytes[2] == 0)
        {
            //todo : face not detected;
        }
        else
        {
            defaultSize++;

            for (int i = 0; i < bytes.Length - 3; i++)
            {
                int pointer = defaultSize + (i * 12);
                float x = BitConverter.ToSingle(bytes, pointer);
                float y = BitConverter.ToSingle(bytes, pointer + 4);
                float z = BitConverter.ToSingle(bytes, pointer + 8);
                v.Add(new Vector3(x, y, z));
            }
        }
        return v;
    }
}