using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using UnityEngine;

// Body(31) 프레임 수신·보관. 명세 01 §4.1∼4.3, Body 규약 v2(2026-11-23판).
// 해석·보관까지만 한다. 스무딩·캐릭터 적용은 이 값을 읽는 쪽이 한다.

public enum EReceiveState
{
    None,       // 한 번도 받지 않음
    Receiving,  // 수신 중
    NoSignal,   // 마지막 프레임 후 1초 경과, 또는 Synapse 연결 끊김 (값은 유지)
}

// 몸 33점 번호 = BodyFrame.PosePosition·PoseVisibility의 인덱스. MediaPipe Pose 순서 그대로, 왼쪽/오른쪽은 사람 기준(Body §2.1, §3.1)
public enum EBodyParts
{
    nose,
    left_eye_inner,
    left_eye,
    left_eye_outer,
    right_eye_inner,
    right_eye,
    right_eye_outer,
    left_ear,
    right_ear,
    mouth_left,
    mouth_right,
    left_shoulder,
    right_shoulder,
    left_elbow,
    right_elbow,
    left_wrist,
    right_wrist,
    left_pinky,
    right_pinky,
    left_index,
    right_index,
    left_thumb,
    right_thumb,
    left_hip,
    right_hip,
    left_knee,
    right_knee,
    left_ankle,
    right_ankle,
    left_heel,
    right_heel,
    left_foot_index,
    right_foot_index,
    max,
}

// 손 21점 번호 = BodyFrame.LeftHand·RightHand의 인덱스. MediaPipe Hand 순서 그대로, 손가락은 뿌리 → 끝(Body §2.1, §3.2)
public enum EHandParts
{
    wrist,             // 항상 (0, 0, 0). 손 좌표의 원점
    thumb_cmc,
    thumb_mcp,
    thumb_ip,
    thumb_tip,
    index_finger_mcp,
    index_finger_pip,
    index_finger_dip,
    index_finger_tip,
    middle_finger_mcp,
    middle_finger_pip,
    middle_finger_dip,
    middle_finger_tip,
    ring_finger_mcp,
    ring_finger_pip,
    ring_finger_dip,
    ring_finger_tip,
    pinky_mcp,
    pinky_pip,
    pinky_dip,
    pinky_tip,
    max,
}

// 가장 최근 Body 프레임. 배열은 한 번만 만들고 수신할 때마다 덮어쓴다(명세 3.4f).
// 읽는 쪽은 값을 바꾸지 않는다.
public class BodyFrame
{
    public byte Flags;
    public uint Seq;
    public uint TimestampMs;
    public readonly Vector3[] PosePosition = new Vector3[(int)EBodyParts.max]; // 골반 중심 원점, m
    public readonly float[] PoseVisibility = new float[(int)EBodyParts.max];
    public readonly Vector3[] LeftHand = new Vector3[(int)EHandParts.max];      // 손목 원점, m
    public readonly Vector3[] RightHand = new Vector3[(int)EHandParts.max];
    public Quaternion LeftPalm = Quaternion.identity;
    public Quaternion RightPalm = Quaternion.identity;

    public Vector3 Root;  // 카메라 기준 골반 중심, m (z는 음수)
    public bool HasRoot;  // 한 번이라도 유효한 루트 위치를 받았나 (4.3g)

    public bool BodyDetected => (Flags & 0x01) != 0;
    public bool LeftHandDetected => (Flags & 0x02) != 0;
    public bool RightHandDetected => (Flags & 0x04) != 0;
    public bool RootValid => (Flags & 0x08) != 0; // 이 프레임의 루트 위치가 유효한가
}

public class BodyReceiver : SingletonMonoBehaviour<BodyReceiver>
{
    private const byte From = 31;
    private const byte Kind = 31;
    private const byte Version = 2;
    private const int PacketLength = 1092;
    private const int HeaderLength = 16;
    private const float SignalTimeout = 1f; // 4.2e: 코드 상수

    // float 인덱스 (오프셋 16부터, Body §2 바이트 표 기준). 몸 132 + 손 67 × 2 + 루트 3 = 269개
    private const int LeftHandIndex = 132;
    private const int RightHandIndex = 199;
    private const int RootIndex = 266; // byte 1080. Body §2 C# 예의 "f[268..270]"은 오기(명세 01 §7)

    public BodyFrame Frame { get; } = new BodyFrame();

    // 받을 때마다 +1. 읽는 쪽이 새 프레임 여부를 판단한다(seq는 재시작 때 되돌아가므로 쓰지 않음).
    private int frameId;
    public int FrameId
    {
        get => frameId;
        private set
        {
            int before = frameId;
            frameId = value;
            if(before != frameId)
                OnChangeFrameID?.Invoke(value);
        }
    }

    public event Action<int> OnChangeFrameID;

    // 트래커 재시작(seq 감소) 횟수. 바뀌면 읽는 쪽이 필터 등 상태를 초기화한다(4.2g).
    public int RestartCount { get; private set; }
    
    public float LastReceivedTime { get; private set; } // Time.realtimeSinceStartup (4.2b)

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

    // SocketManager의 수신 루프(메인 스레드)에서 호출한다. buffer는 호출이 끝나면 재사용되므로 보관하지 않는다.
    public void Receive(byte[] buffer, int length)
    {
        if (!IsValid(buffer, length))
            return;

        ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(buffer, 0, length);
        uint seq = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8));

        if (FrameId > 0 && seq < Frame.Seq)
            RestartCount++; // 4.3e. seq가 건너뛰는 것은 정상이라 무시

        Frame.Flags = data[4];
        Frame.Seq = seq;
        Frame.TimestampMs = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12));

        // Body는 float가 4바이트 경계에 정렬되어 있어 복사 없이 float로 본다(Body §2). 리틀 엔디언 PC 전제.
        ReadOnlySpan<float> f = MemoryMarshal.Cast<byte, float>(data.Slice(HeaderLength, PacketLength - HeaderLength));

        for (int i = 0; i < (int)EBodyParts.max; i++)
        {
            int o = i * 4;
            Frame.PosePosition[i] = new Vector3(f[o], f[o + 1], f[o + 2]);
            Frame.PoseVisibility[i] = f[o + 3];
        }

        ReadHand(f, LeftHandIndex, Frame.LeftHand, ref Frame.LeftPalm);
        ReadHand(f, RightHandIndex, Frame.RightHand, ref Frame.RightPalm);

        if (Frame.RootValid) // 4.3g: 무효면 이전 값 유지
        {
            Frame.Root = new Vector3(f[RootIndex], f[RootIndex + 1], f[RootIndex + 2]);
            Frame.HasRoot = true;
        }

        LastReceivedTime = Time.realtimeSinceStartup;
        disconnected = false;
        FrameId++;
    }

    // Synapse 연결이 끊기면 SocketManager가 호출한다. 바로 신호 없음, 값은 유지(4.2d).
    public void OnDisconnected()
    {
        disconnected = true;
    }

    private bool IsValid(byte[] buffer, int length)
    {
        if (length == PacketLength && buffer[1] == From && buffer[2] == Kind && buffer[3] == Version)
            return true;

        // 4.1c: 처음 한 번만 로그 (v1 트래커를 켜 두면 초당 30번 버리므로)
        if (!loggedDrop)
        {
            loggedDrop = true;
            string got = length >= 4 ? $"종류 {buffer[2]}, 버전 {buffer[3]}, " : "";
            Debug.LogWarning($"[Body] 버린 메시지: {got}길이 {length} (기대: 종류 {Kind}, 버전 {Version}, 길이 {PacketLength})");
        }
        return false;
    }

    private static void ReadHand(ReadOnlySpan<float> f, int start, Vector3[] points, ref Quaternion palm)
    {
        for (int j = 0; j < (int)EHandParts.max; j++)
        {
            int o = start + j * 3;
            points[j] = new Vector3(f[o], f[o + 1], f[o + 2]);
        }

        int q = start + (int)EHandParts.max * 3;
        // 4.3d: (0,0,0,0)이면 회전 없음 → 이전 회전 유지
        if (f[q] != 0f || f[q + 1] != 0f || f[q + 2] != 0f || f[q + 3] != 0f)
            palm = new Quaternion(f[q], f[q + 1], f[q + 2], f[q + 3]);
    }
}
