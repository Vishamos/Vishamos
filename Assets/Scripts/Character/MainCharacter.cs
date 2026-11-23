using System;
using MyMath;
using UnityEngine;

// Body 프레임(BodyReceiver)을 필터링해 보관하고, 에디터 기즈모로 점을 그린다.
// 기즈모는 에디터 전용이라 빌드(방송 화면)에는 나오지 않는다(CLAUDE.md 안전 규칙 1).
// Game 뷰에서 보려면 Game 뷰 툴바의 Gizmos 버튼을 켠다.
public class MainCharacter : MonoBehaviour
{
    private const int BodyPointCount = (int)EBodyParts.max;
    private const int HandPointCount = (int)EHandParts.max;
    private const float VisibleThreshold = 0.4f;

    [SerializeField] [Range(0.0f, 10f)] private float beta = 0f;

    [Header("디버그 기즈모 (에디터 전용)")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float bodyGizmoRadius = 0.03f;
    [SerializeField] private float handGizmoRadius = 0.008f;

    // 필터 결과. 캐릭터 기준 좌표(미러 적용됨). 휴머노이드 적용도 이 값을 쓴다.
    private readonly EuroVector3[] bodyFilter = new EuroVector3[BodyPointCount];
    private readonly Vector3[] bodyPos = new Vector3[BodyPointCount];      // 골반 원점
    private readonly bool[] bodyVisible = new bool[BodyPointCount];
    private bool hasBody;

    private readonly EuroVector3 rootFilter = new();
    private Vector3 rootPos;                                                  // 원점 없음(plan_review 2-9)
    private uint rootPrevTimestamp;
    private bool rootNeedReset = true;

    private readonly HandTrack leftHand = new();
    private readonly HandTrack rightHand = new();

    private int lastRestart;
    private bool needReset = true;
    private uint prevTimestamp;

    private void Awake()
    {
        for (int i = 0; i < BodyPointCount; i++)
            bodyFilter[i] = new EuroVector3();
    }

    private void Start()
    {
        if (MainManager.Instance.MainCharacter)
        {
            Destroy(gameObject);
            return;
        }

        MainManager.Instance.MainCharacter = this;
        BodyReceiver.Instance.OnChangeFrameID += OnChangeBodyFrameID;
    }

    private void OnDestroy()
    {
        BodyReceiver.Instance.OnChangeFrameID -= OnChangeBodyFrameID;
    }

    private void OnChangeBodyFrameID(int value)
    {
        var receiver = BodyReceiver.Instance;
        var frame = receiver.Frame;

        if (receiver.RestartCount != lastRestart) // 트래커 재시작: 전부 초기화
        {
            lastRestart = receiver.RestartCount;
            needReset = true;
            rootNeedReset = true;
            leftHand.RequestReset();
            rightHand.RequestReset();
        }

        if (!frame.BodyDetected) // 몸을 놓치면 표시를 끄고, 다시 잡히면 초기화
        {
            hasBody = false;
            needReset = true;
            rootNeedReset = true;
            leftHand.RequestReset();
            rightHand.RequestReset();
            return;
        }

        float dt;
        if (needReset)
        {
            foreach (var f in bodyFilter) f.Reset();
            dt = 0f;
            needReset = false;
        }
        else
            dt = (frame.TimestampMs - prevTimestamp) / 1000f;
        prevTimestamp = frame.TimestampMs;

        for (int i = 0; i < BodyPointCount; i++)
        {
            bodyPos[i] = bodyFilter[i].FilterVector(frame.PosePosition[i], dt, beta);
            bodyVisible[i] = frame.PoseVisibility[i] > VisibleThreshold;
        }
        hasBody = true;

        UpdateRoot(frame);
        leftHand.Update(frame.LeftHandDetected, frame.LeftHand, frame.TimestampMs, beta);
        rightHand.Update(frame.RightHandDetected, frame.RightHand, frame.TimestampMs, beta);
    }

    // 루트 위치: 유효한 프레임만 필터에 넣고, 무효면 이전 위치 유지(명세 01 4.3g)
    private void UpdateRoot(BodyFrame frame)
    {
        if (!frame.RootValid)
            return;

        float dt;
        if (rootNeedReset)
        {
            rootFilter.Reset();
            dt = 0f;
            rootNeedReset = false;
        }
        else
            dt = (frame.TimestampMs - rootPrevTimestamp) / 1000f;
        rootPrevTimestamp = frame.TimestampMs;

        rootPos = rootFilter.FilterVector(frame.Root, dt, beta);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying || !hasBody)
            return;

        // 캐릭터 트랜스폼(위치·y 180° 회전) + 루트 위치를 한 번에 적용해 캐릭터 기준 좌표를 그대로 그린다
        Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.Translate(rootPos);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < BodyPointCount; i++)
        {
            if (bodyVisible[i])
                Gizmos.DrawSphere(bodyPos[i], bodyGizmoRadius);
        }

        DrawHand(leftHand, bodyPos[(int)EBodyParts.left_wrist], Color.yellow);
        DrawHand(rightHand, bodyPos[(int)EBodyParts.right_wrist], Color.magenta);

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawHand(HandTrack hand, Vector3 wrist, Color color)
    {
        if (!hand.Visible)
            return;

        // 손 좌표는 손목 원점, 축은 캐릭터 기준이라 손목 위치를 더하기만 한다(회전 없음, Body §3.2)
        Gizmos.color = color;
        for (int i = 0; i < HandPointCount; i++)
            Gizmos.DrawSphere(wrist + hand.Pos[i], handGizmoRadius);
    }

    // 손 하나의 필터 상태. 검출 여부·초기화·dt를 손마다 따로 관리한다.
    private class HandTrack
    {
        public readonly Vector3[] Pos = new Vector3[HandPointCount]; // 손목 원점, 필터 후
        public bool Visible { get; private set; }

        private readonly EuroVector3[] filter = new EuroVector3[HandPointCount];
        private readonly Vector3[] lastRaw = new Vector3[HandPointCount];
        private uint prevTimestamp;
        private bool needReset = true;

        public HandTrack()
        {
            for (int i = 0; i < HandPointCount; i++)
                filter[i] = new EuroVector3();
        }

        public void RequestReset()
        {
            needReset = true;
            Visible = false;
        }

        public void Update(bool detected, Vector3[] raw, uint timestamp, float beta)
        {
            Visible = detected;
            if (!detected) // 놓치면 숨기고, 다시 잡히면 이 손만 초기화
            {
                needReset = true;
                return;
            }

            float dt;
            if (needReset)
            {
                foreach (var f in filter) f.Reset();
                dt = 0f;
                needReset = false;
            }
            else
            {
                // 손은 몸보다 느리게 갱신되어 같은 값이 반복해서 온다. 바뀐 프레임만 필터에 넣는다(Body §3.2)
                if (SameAsLast(raw))
                    return;
                dt = (timestamp - prevTimestamp) / 1000f;
            }

            Array.Copy(raw, lastRaw, HandPointCount);
            prevTimestamp = timestamp;

            for (int i = 0; i < HandPointCount; i++)
                Pos[i] = filter[i].FilterVector(raw[i], dt, beta);
        }

        private bool SameAsLast(Vector3[] raw)
        {
            for (int i = 0; i < HandPointCount; i++)
            {
                if (raw[i] != lastRaw[i])
                    return false;
            }
            return true;
        }
    }
}
