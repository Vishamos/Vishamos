using System;
using System.Collections.Generic;
using MyMath;
using UnityEngine;

public class MainCharacter : MonoBehaviour
{
    [SerializeField] GameObject mouth;
    [SerializeField] SpriteRenderer body;
    [SerializeField] private GameObject bodyDummy;
    [SerializeField] private List<GameObject> bodyParts = new();
    
    
    [SerializeField] private EuroVector3[] bodyVector = new EuroVector3[BodyPointCount];
    [SerializeField] [Range(0.0f, 10f)] private float beta = 0f;
    
    
    private const int BodyPointCount = 33;
    float mouthMin = 0.2f;

    [SerializeField] public float minVolume = -90.0f;

    private byte[] bodyByte = new byte[BodyPointCount * 16];
    private uint prevSeq, prevTimestamp;
    private bool hasPrev;

    public byte[] BodyBytes
    {
        set
        {
            uint seq = BitConverter.ToUInt32(value, 8);
            uint ts  = BitConverter.ToUInt32(value, 12);

            float dt = 0f;
            if (hasPrev && seq > prevSeq)
                dt = (ts - prevTimestamp) / 1000f;   // ms → 초
            else
                ResetFilters();                       // 첫 패킷 또는 트래커 재시작(seq 감소)

            prevSeq = seq;
            prevTimestamp = ts;
            hasPrev = true;

            Buffer.BlockCopy(value, 16, bodyByte, 0, 528);
            SetBody(dt);

        }
    }

    private void Awake()
    {
        if (bodyDummy)
        {
            for (int i = 0; i < BodyPointCount; i++)
            {
                GameObject obj = Instantiate(bodyDummy, transform);
                obj.name = Enum.GetName(typeof(EBodyParts), i) ?? string.Empty;
                bodyParts.Add(obj);
                bodyVector[i] = new EuroVector3();
            }
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
    }

    private void Update()
    {
        float volume = MicManager.Instance.DbValue;
        if (volume < minVolume)
            volume = minVolume;
        else if (volume > 0)
            volume = 0.0f;

        mouth.transform.localPosition = Vector3.down * ((volume - minVolume) / -minVolume) * mouthMin;
    }

    private void SetBody(float dt)
    {
        for (int i = 0; i < BodyPointCount; i++)
        {
            int o = i * 16;
            Vector3 pos = bodyVector[i].FilterVector(
                BitConverter.ToSingle(bodyByte, o),
                BitConverter.ToSingle(bodyByte, o + 4),
                BitConverter.ToSingle(bodyByte, o + 8), dt, beta); //
            float visible = BitConverter.ToSingle(bodyByte, o + 12);

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

    public void Reset()
    {
        x.Reset();
        y.Reset();
        z.Reset();
    }
}

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
}