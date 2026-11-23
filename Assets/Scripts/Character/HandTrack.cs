using System;
using MyMath;
using UnityEngine;

// 손 하나(21점)의 필터 상태. 검출 여부·초기화·dt를 손마다 따로 관리한다.
// 좌표는 손목 원점, 축은 캐릭터 기준(미러 후, Body §3.2).
public class HandTrack
{
    public const int PointCount = (int)EHandParts.max;

    public readonly Vector3[] Pos = new Vector3[PointCount]; // 필터 후
    public bool Visible { get; private set; }

    private readonly EuroVector3[] filter = new EuroVector3[PointCount];
    private readonly Vector3[] lastRaw = new Vector3[PointCount];
    private uint prevTimestamp;
    private bool needReset = true;

    public HandTrack()
    {
        for (int i = 0; i < PointCount; i++)
            filter[i] = new EuroVector3();
    }

    // 트래커 재시작·몸 놓침 때 부른다. 숨기고, 다시 잡히면 초기화한다.
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
            foreach (EuroVector3 f in filter) f.Reset();
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

        Array.Copy(raw, lastRaw, PointCount);
        prevTimestamp = timestamp;

        for (int i = 0; i < PointCount; i++)
            Pos[i] = filter[i].FilterVector(raw[i], dt, beta);
    }

    private bool SameAsLast(Vector3[] raw)
    {
        for (int i = 0; i < PointCount; i++)
        {
            if (raw[i] != lastRaw[i])
                return false;
        }
        return true;
    }
}
