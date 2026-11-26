using UnityEngine;

// 시선 계산: Face 시선 값 → 시선 2축 → 자식의 EyeGazeDriver 전부. 명세 04 §1a 1e-1∼1e-5
public class EyeGaze : MonoBehaviour
{
    [SerializeField] [Range(0f, 0.2f)] private float smoothTime = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float blinkHoldThreshold = 0.5f;
    [SerializeField] [Min(0f)] private float holdTime = 0.5f;
    [SerializeField] [Min(0.01f)] private float returnDuration = 0.5f;

    [Header("확인용 (인스펙터에만 표시)")]
    [SerializeField] private Vector2 viewGaze;

    private EyeGazeDriver[] drivers;
    private Vector2 gazeLast;   // 마지막으로 추적한 시선 (감는 동안은 갱신하지 않음)
    private Vector2 gazeCurrent;
    private float lostTime;
    private float returnWeight; // 0 = 추적 시선, 1 = 가운데

    private void Start()
    {
        drivers = GetComponentsInChildren<EyeGazeDriver>(true);
        if (drivers.Length == 0)
            Debug.LogWarning("[EyeGaze] EyeGazeDriver가 없습니다. 캐릭터 아래 Eye 메시에 EyeGazeUvDriver를 붙이세요.");
    }

    private void LateUpdate()
    {
        FaceReceiver face = FaceReceiver.Instance;
        if (!face || face.State == EReceiveState.None)
            return;

        float dt = Time.deltaTime;
        if (face.State == EReceiveState.Receiving && face.Frame.Tracking)
        {
            float[] s = face.Frame.BlendShapes;
            float blink = (s[(int)EFaceBlendShape.eyeBlinkLeft] + s[(int)EFaceBlendShape.eyeBlinkRight]) * 0.5f;
            if (blink <= blinkHoldThreshold)
                gazeLast = ReadGaze(s);
            lostTime = 0f;
            returnWeight = Mathf.MoveTowards(returnWeight, 0f, dt / returnDuration);
        }
        else
        {
            lostTime += dt;
            if (lostTime > holdTime)
                returnWeight = Mathf.MoveTowards(returnWeight, 1f, dt / returnDuration);
        }

        Vector2 target = Vector2.Lerp(gazeLast, Vector2.zero, Mathf.SmoothStep(0f, 1f, returnWeight));
        float lerp = smoothTime > 0f ? 1f - Mathf.Exp(-dt / smoothTime) : 1f;
        gazeCurrent = Vector2.Lerp(gazeCurrent, target, lerp);
        viewGaze = gazeCurrent;

        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i] && drivers[i].isActiveAndEnabled)
                drivers[i].Apply(gazeCurrent);
        }
    }

    // 가로 + = 캐릭터 오른쪽(저장값 기준, 4.4h). 왼눈은 In이, 오른눈은 Out이 오른쪽이다
    private static Vector2 ReadGaze(float[] s)
    {
        float left = s[(int)EFaceBlendShape.eyeLookInLeft] - s[(int)EFaceBlendShape.eyeLookOutLeft];
        float right = s[(int)EFaceBlendShape.eyeLookOutRight] - s[(int)EFaceBlendShape.eyeLookInRight];
        float upLeft = s[(int)EFaceBlendShape.eyeLookUpLeft] - s[(int)EFaceBlendShape.eyeLookDownLeft];
        float upRight = s[(int)EFaceBlendShape.eyeLookUpRight] - s[(int)EFaceBlendShape.eyeLookDownRight];
        return new Vector2((left + right) * 0.5f, (upLeft + upRight) * 0.5f);
    }
}
