using System;
using MyMath;
using UnityEngine;

public class MainCharacter : MonoBehaviour
{
    [SerializeField] private GameObject bodyDummy;
    [SerializeField] private GameObject[] bodyParts = new GameObject[BodyPointCount];
    private EuroVector3[] bodyVector = new EuroVector3[BodyPointCount];
    [SerializeField] [Range(0.0f, 10f)] private float beta = 0f;

    private const int BodyPointCount = (int)EBodyParts.max;


    private void Awake()
    {
        if (bodyParts.Length != BodyPointCount)
            Array.Resize(ref bodyParts, BodyPointCount);
        if (bodyDummy)
        {
            for (int i = 0; i < BodyPointCount; i++)
            {
                if (bodyParts[i]) continue;
                GameObject obj = Instantiate(bodyDummy, transform);
                obj.name = Enum.GetName(typeof(EBodyParts), i) ?? string.Empty;
                bodyParts[i] = obj;
            }
        }

        for (int i = 0; i < BodyPointCount; i++)
        {
            bodyVector[i] = new EuroVector3();
        }
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

    private int lastRestart;
    private bool needReset = true;
    private uint prevTimestamp;

    private void OnChangeBodyFrameID(int value)
    {
        var BR = BodyReceiver.Instance;
        var bf = BR.Frame; //프레임 => 바디 트래킹 구조 전체
        float dt;
        if (BR.RestartCount != lastRestart)
        {
            lastRestart = BR.RestartCount;
            needReset = true;
        }

        if (!bf.BodyDetected) //바디 감지 안되면
        {
            needReset = true; //리셋 필요
            return;
        }

        if (needReset) //리셋해야하면
        {
            ResetFilters(); //원유로 필터 초기화
            dt = 0;
            needReset = false;
        }
        else
            dt = (bf.TimestampMs - prevTimestamp) / 1000f;

        prevTimestamp = bf.TimestampMs; //타임 스탬프 갱신
        SetBody(dt, bf);
    }

    private void SetBody(float dt, BodyFrame bodyFrame)
    {
        for (int i = 0; i < (int)EBodyParts.max; i++)
        {
            Vector3 pos = bodyVector[i].FilterVector(bodyFrame.PosePosition[i], dt, beta); //
            float visible = bodyFrame.PoseVisibility[i];

            bodyParts[i].SetActive(visible > 0.4f);
            if (bodyParts[i].activeSelf)
                bodyParts[i].transform.localPosition = pos;
        }
    }

    private void ResetFilters()
    {
        foreach (var oe in bodyVector)
        {
            oe.Reset();
        }
    }
}

public class EuroVector3
{
    private OneEuro x = new();
    private OneEuro y = new();
    private OneEuro z = new();

    public Vector3 FilterVector(float _x, float _y, float _z, float dt, float beta)
    {
        return new Vector3(x.Filter(_x, dt, beta), y.Filter(_y, dt, beta), z.Filter(_z, dt, beta));
    }

    public Vector3 FilterVector(Vector3 pos, float dt, float beta)
    {
        return FilterVector(pos.x, pos.y, pos.z, dt, beta);
    }

    public void Reset()
    {
        x.Reset();
        y.Reset();
        z.Reset();
    }
}