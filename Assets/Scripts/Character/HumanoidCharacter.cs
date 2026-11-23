using MyMath;
using UnityEngine;

// Body 프레임(BodyReceiver)을 직접 받아 필터링하고, 휴머노이드 뼈를 돌린다. MainCharacter와 독립.
// 점 사이 "방향"만 쓰므로 사람과 모델의 체형·크기 차이와 무관하다(Body §4).
//
// 배치: Humanoid 리그 모델의 루트(Animator가 있는 오브젝트)에 붙인다.
//       데이터는 캐릭터가 +z를 바라보는 기준이고 모델도 +z를 보므로, 이 오브젝트를 y 180° 돌리면 카메라를 마주 본다.
//       Animator Controller는 비워 둔다(있으면 매 프레임 자세를 덮어씀).
// 1단계: 팔 4개 뼈. 두 점 중 하나라도 보이지 않으면 그 뼈는 이전 회전을 유지한다.
[RequireComponent(typeof(Animator))]
public class HumanoidCharacter : MonoBehaviour
{
    private const int BodyPointCount = (int)EBodyParts.max;
    private const float VisibleThreshold = 0.4f;

    [SerializeField] [Range(0.0f, 10f)] private float beta = 0f;

    private readonly struct BoneLink
    {
        public readonly HumanBodyBones Bone;   // 돌릴 뼈
        public readonly HumanBodyBones Child;  // 기본 자세의 뼈 방향을 잴 자식 뼈
        public readonly EBodyParts From;       // 트래킹 방향 = 점[To] − 점[From]
        public readonly EBodyParts To;

        public BoneLink(HumanBodyBones bone, HumanBodyBones child, EBodyParts from, EBodyParts to)
        {
            Bone = bone;
            Child = child;
            From = from;
            To = to;
        }
    }

    // 부모 → 자식 순서. BodyFrame은 미러 후 캐릭터 기준이라 Left 뼈에 left_ 점을 그대로 짝짓는다.
    private static readonly BoneLink[] Links =
    {
        new(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  EBodyParts.left_shoulder,  EBodyParts.left_elbow),
        new(HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,      EBodyParts.left_elbow,     EBodyParts.left_wrist),
        new(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, EBodyParts.right_shoulder, EBodyParts.right_elbow),
        new(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,     EBodyParts.right_elbow,    EBodyParts.right_wrist),
    };

    // 뼈
    private Transform[] bones;
    private Vector3[] restDir;    // 기본 자세의 뼈 방향 (이 오브젝트 기준)
    private Quaternion[] restRot; // 기본 자세의 뼈 회전 (이 오브젝트 기준)

    // 필터 (캐릭터 기준 좌표, 골반 원점)
    private readonly EuroVector3[] bodyFilter = new EuroVector3[BodyPointCount];
    private readonly Vector3[] bodyPos = new Vector3[BodyPointCount];
    private readonly bool[] bodyVisible = new bool[BodyPointCount];
    private bool hasBody;
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
        Animator animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            Debug.LogError("[Humanoid] Animator 아바타가 Humanoid가 아닙니다. FBX 임포트 설정에서 Rig를 Humanoid로 바꾸세요.");
            enabled = false;
            return;
        }

        CaptureRestPose(animator);

        if (!BodyReceiver.Instance)
        {
            Debug.LogError("[Humanoid] 씬에 BodyReceiver가 없습니다.");
            enabled = false;
            return;
        }
        BodyReceiver.Instance.OnChangeFrameID += OnBodyFrame;
    }

    private void OnDestroy()
    {
        if (BodyReceiver.Instance)
            BodyReceiver.Instance.OnChangeFrameID -= OnBodyFrame;
    }

    // 기본 자세를 이 오브젝트 기준으로 저장한다. 오브젝트를 옮기거나 돌려도 그대로 맞는다.
    private void CaptureRestPose(Animator animator)
    {
        Quaternion toLocal = Quaternion.Inverse(transform.rotation);

        bones = new Transform[Links.Length];
        restDir = new Vector3[Links.Length];
        restRot = new Quaternion[Links.Length];

        for (int i = 0; i < Links.Length; i++)
        {
            Transform bone = animator.GetBoneTransform(Links[i].Bone);
            Transform child = animator.GetBoneTransform(Links[i].Child);
            if (!bone || !child)
            {
                Debug.LogWarning($"[Humanoid] 뼈 없음: {Links[i].Bone} 또는 {Links[i].Child}. 이 뼈는 움직이지 않습니다.");
                continue;
            }

            bones[i] = bone;
            restDir[i] = toLocal * (child.position - bone.position);
            restRot[i] = toLocal * bone.rotation;
        }
    }

    // 프레임을 받을 때마다 필터만 갱신한다. 뼈 적용은 LateUpdate.
    private void OnBodyFrame(int frameId)
    {
        BodyReceiver receiver = BodyReceiver.Instance;
        BodyFrame frame = receiver.Frame;

        if (receiver.RestartCount != lastRestart) // 트래커 재시작
        {
            lastRestart = receiver.RestartCount;
            needReset = true;
        }

        if (!frame.BodyDetected) // 놓치면 뼈를 그대로 두고, 다시 잡히면 초기화
        {
            hasBody = false;
            needReset = true;
            return;
        }

        float dt;
        if (needReset)
        {
            foreach (EuroVector3 f in bodyFilter) f.Reset();
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
    }

    // Animator가 자세를 계산한 뒤에 덮어쓰도록 LateUpdate에서 적용한다.
    private void LateUpdate()
    {
        if (!hasBody)
            return;

        Quaternion space = transform.rotation;

        for (int i = 0; i < Links.Length; i++)
        {
            if (!bones[i])
                continue;

            int from = (int)Links[i].From;
            int to = (int)Links[i].To;
            if (!bodyVisible[from] || !bodyVisible[to])
                continue; // 보이지 않는 점은 추측값이라 쓰지 않음

            Vector3 dir = bodyPos[to] - bodyPos[from]; // 캐릭터 기준 좌표
            if (dir.sqrMagnitude < 1e-8f)
                continue;

            // 기본 자세 방향을 트래킹 방향으로 돌리는 최소 회전. 뼈 축 비틀림(roll)은 반영되지 않는다.
            bones[i].rotation = space * (Quaternion.FromToRotation(restDir[i], dir) * restRot[i]);
        }
    }
}
