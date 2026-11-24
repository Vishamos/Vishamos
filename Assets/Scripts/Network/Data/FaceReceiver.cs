using System;
using System.Buffers.Binary;
using UnityEngine;

// Face iOS(22) 프레임 수신·보관. 명세 01 §4.1, §4.2, §4.4, Face 규약 v1(2026-11-23판, 8903e4b).
// 앱이 이미 거울 반전해 보내므로 반전하지 않는다(4.4h, 2026-11-24 실측).

// 블렌드셰이프 번호 = FaceFrame.BlendShapes의 인덱스. ARKit 알파벳 순서(Face §4). Left/Right는 캐릭터 기준(4.4h)
public enum EFaceBlendShape
{
    browDownLeft, browDownRight, browInnerUp, browOuterUpLeft, browOuterUpRight,
    cheekPuff, cheekSquintLeft, cheekSquintRight,
    eyeBlinkLeft, eyeBlinkRight, eyeLookDownLeft, eyeLookDownRight, eyeLookInLeft, eyeLookInRight,
    eyeLookOutLeft, eyeLookOutRight, eyeLookUpLeft, eyeLookUpRight, eyeSquintLeft, eyeSquintRight,
    eyeWideLeft, eyeWideRight,
    jawForward, jawLeft, jawOpen, jawRight,
    mouthClose, mouthDimpleLeft, mouthDimpleRight, mouthFrownLeft, mouthFrownRight, mouthFunnel,
    mouthLeft, mouthLowerDownLeft, mouthLowerDownRight, mouthPressLeft, mouthPressRight, mouthPucker,
    mouthRight, mouthRollLower, mouthRollUpper, mouthShrugLower, mouthShrugUpper,
    mouthSmileLeft, mouthSmileRight, mouthStretchLeft, mouthStretchRight, mouthUpperUpLeft, mouthUpperUpRight,
    noseSneerLeft, noseSneerRight, tongueOut,
    max,
}

// 가장 최근 Face 프레임. 배열은 한 번만 만들고 덮어쓴다(3.4f). 읽는 쪽은 값을 바꾸지 않는다
public class FaceFrame
{
    public bool Tracking;   // flags bit0. 0이어도 값은 앱이 보낸 마지막 추적 값(4.4c)
    public uint Seq;
    public uint TimeMs;
    public readonly float[] BlendShapes = new float[(int)EFaceBlendShape.max];
    public Quaternion HeadRotation = Quaternion.identity; // 폰 카메라 기준
    public Vector3 HeadPosition;                          // 폰 카메라 기준, m
}

public class FaceReceiver : SingletonMonoBehaviour<FaceReceiver>
{
    private const byte From = 22;
    private const byte Kind = 0x16;
    private const byte Version = 1;
    private const int PacketLength = 250;
    private const int BlendShapeOffset = 14;
    private const int RotationOffset = 222;
    private const int PositionOffset = 238;
    private const float SignalTimeout = 1f;

    [Header("확인용 (인스펙터에만 표시)")]
    [SerializeField] private bool viewTracking;
    [SerializeField] private Vector3 viewHeadEuler;
    [SerializeField] private Vector3 viewHeadPosition;
    [SerializeField] private float viewJawOpen;
    [SerializeField] private float viewEyeBlinkLeft;
    [SerializeField] private float viewEyeBlinkRight;

    public FaceFrame Frame { get; } = new FaceFrame();
    public int FrameId { get; private set; }
    public int RestartCount { get; private set; }
    public float LastReceivedTime { get; private set; }

    public EReceiveState State
    {
        get
        {
            if (FrameId == 0) return EReceiveState.None;
            if (disconnected) return EReceiveState.NoSignal;
            return Time.realtimeSinceStartup - LastReceivedTime > SignalTimeout
                ? EReceiveState.NoSignal
                : EReceiveState.Receiving;
        }
    }

    private bool disconnected;
    private bool loggedDrop;

    public void Receive(byte[] buffer, int length)
    {
        if (!IsValid(buffer, length))
            return;

        ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(buffer, 0, length);
        uint seq = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(6));
        if (FrameId > 0 && seq < Frame.Seq)
            RestartCount++; // 4.4d

        Frame.Tracking = (data[4] & 0x01) != 0;
        Frame.Seq = seq;
        Frame.TimeMs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(10));

        // 4.4e: 4바이트 경계가 아니라 값마다 읽는다
        for (int i = 0; i < (int)EFaceBlendShape.max; i++)
            Frame.BlendShapes[i] = ReadFloat(data, BlendShapeOffset + i * 4);

        float qx = ReadFloat(data, RotationOffset);
        float qy = ReadFloat(data, RotationOffset + 4);
        float qz = ReadFloat(data, RotationOffset + 8);
        float qw = ReadFloat(data, RotationOffset + 12);
        float len = Mathf.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
        if (len >= 0.5f) // 4.4i: 아니면 이전 회전 유지
            Frame.HeadRotation = new Quaternion(qx / len, qy / len, qz / len, qw / len);

        Frame.HeadPosition = new Vector3(
            ReadFloat(data, PositionOffset),
            ReadFloat(data, PositionOffset + 4),
            ReadFloat(data, PositionOffset + 8));

        LastReceivedTime = Time.realtimeSinceStartup;
        disconnected = false;
        FrameId++;

#if UNITY_EDITOR
        viewTracking = Frame.Tracking;
        viewHeadEuler = Frame.HeadRotation.eulerAngles;
        viewHeadPosition = Frame.HeadPosition;
        viewJawOpen = Frame.BlendShapes[(int)EFaceBlendShape.jawOpen];
        viewEyeBlinkLeft = Frame.BlendShapes[(int)EFaceBlendShape.eyeBlinkLeft];
        viewEyeBlinkRight = Frame.BlendShapes[(int)EFaceBlendShape.eyeBlinkRight];
#endif
    }

    public void OnDisconnected()
    {
        disconnected = true;
    }

    private bool IsValid(byte[] buffer, int length)
    {
        if (length == PacketLength && buffer[1] == From && buffer[2] == Kind && buffer[3] == Version)
            return true;

        if (!loggedDrop) // 4.1c
        {
            loggedDrop = true;
            string got = length >= 4 ? $"종류 {buffer[2]}, 버전 {buffer[3]}, " : "";
            Debug.LogWarning($"[Face] 버린 메시지: {got}길이 {length} (기대: 종류 {Kind}, 버전 {Version}, 길이 {PacketLength})");
        }
        return false;
    }

    private static float ReadFloat(ReadOnlySpan<byte> data, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset)));
}
