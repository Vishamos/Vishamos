using MyMath;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

// Body 프레임(BodyReceiver)을 직접 받아 필터링하고, 휴머노이드 뼈를 돌린다. MainCharacter와 독립.
// 점 사이 "방향"만 쓰므로 사람과 모델의 체형·크기 차이와 무관하다(Body §4).
//
// 배치: Humanoid 리그 모델의 루트(Animator가 있는 오브젝트)에 붙인다.
//       데이터는 캐릭터가 +z를 바라보는 기준이고 모델도 +z를 보므로, 이 오브젝트를 y 180° 돌리면 카메라를 마주 본다.
//       Animator Controller는 비워 둔다(있으면 매 프레임 자세를 덮어씀).
// 팔:   위팔·아래팔 4개 뼈. 두 점 중 하나라도 보이지 않으면(visibleThreshold 이하) 그 뼈는 이전 회전을 유지한다.
//       손을 놓치거나 손목이 보이지 않으면 손·손가락도 이전 회전을 유지한다.
//       armDownDelay초 넘게 보이지 않으면 팔·손·손가락이 천천히 Idle 자세(idleClip 첫 프레임의 부모 기준 회전)로 간다(plan_review 4-1).
//       idleClip이 없으면 위팔은 몸 옆으로 내리고 아래팔은 위팔을 따라 편 자세, 손·손가락은 기본 자세로 간다.
//       다시 보이면 트래킹 자세로 천천히 돌아온다. 몸 전체를 놓쳐도 같다. 시작 직후 T자세도 이 규칙으로 내려간다.
// 상체: 어깨선으로 좌우 기울기·비틀림을 구해 척추 뼈(Spine·Chest·UpperChest 중 있는 것)에 나눠 건다.
//       앞뒤 숙임은 골반이 추측값이라 넣지 않는다(Body §3.1).
// 루트: rootTarget을 지정하면 루트 위치(카메라 기준 골반 위치)의 "원점에서 움직인 양 × rootScale"만큼 옮긴다.
//       원점은 시작 후 첫 유효 값으로 자동으로 잡고, Recenter()로 다시 잡는다. 놓침·재시작 때는 유지한다(plan_review 2-9).
//       lockRoot가 켜져 있으면 이동하지 않고 배치한 자리로 돌아간다(xyz 전체). 기본은 켜짐(고정), F10으로 풀고 다시 켠다.
//       흔들림: 이동량이 목표에서 rootDeadZone(m) 넘게 벗어날 때만 목표를 옮기고, rootSmoothTime 동안 따라간다.
// 머리: Face 머리 회전을 Neck·Head에 나눠 건다. F8로 지금 자세를 정면으로 저장(config.json). 추적이 끊기면 잠시 유지 후 정면으로(plan_review 3-4∼3-6).
// 손:   손목·중지 뿌리·검지 뿌리·새끼 뿌리 점으로 손 좌표계를 만들어 손 뼈를 돌리고, 손가락 마디는 점 방향으로 돌린다.
//       손가락은 손 회전을 먼저 적용한 자세에서 남은 방향 차이만 더한다(손의 비틀림을 따라가게).
//       기본 자세와 트래킹에 같은 계산을 쓰므로 왼손·오른손의 손바닥 방향 부호를 따로 맞출 필요가 없다.
//       손바닥 회전(LeftPalm)은 쓰지 않는다. 손가락 뼈는 Humanoid 선택 뼈라 모델에 없으면 그 부분만 건너뛴다.
[RequireComponent(typeof(Animator))]
public class HumanoidCharacter : MonoBehaviour
{
    private const int BodyPointCount = (int)EBodyParts.max;

    [SerializeField] [Range(0.0f, 10f)] private float beta = 0f;
    // 이 값을 넘는 점만 쓴다. 0.5 근처는 MediaPipe 추측값이라 크게 튄다(이슈 #2, 2026-11-23 실측). 화면을 보며 맞춘다
    [SerializeField] [Range(0.0f, 1f)] private float visibleThreshold = 0.65f;

    [Header("보간")]
    // 데이터는 초당 약 30번이라 그대로 넣으면 화면이 60fps여도 30Hz 계단으로 보인다.
    // 매 화면 프레임 뼈 회전을 목표 쪽으로 이 시간 상수(초)로 따라가게 한다. 0이면 끔. 지연이 대략 이만큼 늘어난다
    [SerializeField] [Range(0f, 0.2f)] private float poseSmoothTime = 0.05f;

    [Header("팔 놓침")]
    [SerializeField] private AnimationClip idleClip;                         // Humanoid 클립. 첫 프레임만 시작할 때 한 번 읽는다
    [SerializeField] [Min(0f)] private float armDownDelay = 0.5f;            // 안 보인 뒤 내리기 시작할 때까지(초)
    [SerializeField] [Min(0.01f)] private float armDownDuration = 0.5f;      // 내리기·돌아오기에 걸리는 시간(초)
    [SerializeField] [Range(0f, 60f)] private float armDownOutAngle = 15f;  // idleClip이 없을 때 내린 팔을 몸 옆에서 바깥으로 벌리는 각도

    [Header("상체")]
    [SerializeField] [Range(0.0f, 1f)] private float chestWeight = 1f; // 상체 회전을 얼마나 반영할지 (0 = 고정)
    // 척추 뼈별 비중 (Spine, Chest, UpperChest 순). 모델에 없는 뼈는 빼고 나머지로 다시 나눈다.
    [SerializeField] private float[] spineWeights = { 0.3f, 0.35f, 0.35f };

    [Header("루트 위치")]
    [SerializeField] private Transform rootTarget; // 비우면 루트 위치를 쓰지 않는다
    [SerializeField] private Vector3 rootScale = Vector3.one; // 축별 이동 배율. 0이면 그 축 이동을 끈다
    [SerializeField] private Key recenterKey = Key.F9;        // 원점 다시 잡기. Vishamos 창에 포커스가 있을 때만 받는다
    [SerializeField] private bool lockRoot = true;            // 루트 이동 막기(xyz 전체). 기본 고정
    [SerializeField] private Key lockRootKey = Key.F10;       // lockRoot 켜고 끄기
    [SerializeField] [Min(0f)] private float rootDeadZone = 0.1f;      // 목표에서 이만큼(m, 배율 적용 후) 벗어나야 목표를 옮긴다. 작은 흔들림을 버림
    [SerializeField] [Min(0.01f)] private float rootSmoothTime = 0.3f; // 목표까지 따라가는 시간(초)

    [Header("머리 (Face)")]
    [SerializeField] private bool applyHead = true;
    [SerializeField] [Range(0f, 1f)] private float neckWeight = 0.4f; // 나머지는 Head
    [SerializeField] private Key headRecenterKey = Key.F8;
    [SerializeField] [Min(0f)] private float headHoldTime = 0.5f;          // 끊긴 뒤 마지막 각도를 유지하는 시간(초)
    [SerializeField] [Min(0.01f)] private float headReturnDuration = 0.5f; // 정면으로 돌아가는 데 걸리는 시간(초)

    [Header("손")]
    [SerializeField] [Range(0.0f, 10f)] private float handBeta = 0f;
    [SerializeField] private bool applyHands = true;   // 손 뼈(손목) 회전
    [SerializeField] private bool applyFingers = true; // 손가락 마디 회전

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

    // 아래 → 위 순서. spineWeights와 같은 순서
    private static readonly HumanBodyBones[] SpineBones =
    {
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
    };

    // 팔
    private Transform[] bones;
    private Vector3[] restDir;    // 기본 자세의 뼈 방향 (이 오브젝트 기준)
    private Quaternion[] restRot; // 기본 자세의 뼈 회전 (이 오브젝트 기준)
    private Quaternion[] trackedRot; // 마지막으로 추적한 회전 (이 오브젝트 기준). 처음엔 기본 자세
    private float[] hiddenTime;      // 연속으로 안 보인 시간(초)
    private float[] downWeight;      // 0 = 트래킹 자세, 1 = 내린 자세
    private Quaternion[] idleLocal;  // Idle 자세의 부모 기준 회전
    private bool hasIdle;

    // 상체
    private readonly Transform[] spine = new Transform[3];
    private readonly Quaternion[] spineRestRot = new Quaternion[3]; // 이 오브젝트 기준
    private Quaternion shoulderRestFrameInv;                         // 기본 자세의 어깨선 좌표계의 역회전
    private bool hasSpine;

    // 손: 손가락 마디 하나 = 뼈 하나
    private struct FingerBone
    {
        public Transform Bone;
        public Vector3 RestDir;    // 이 오브젝트 기준
        public Quaternion RestRot; // 이 오브젝트 기준
        public Quaternion IdleLocal; // 부모 기준
        public Quaternion HeldLocal; // 놓친 순간의 부모 기준 회전
        public int From, To;       // EHandParts 번호. 트래킹 방향 = Pos[To] − Pos[From]
    }

    private class HandRig
    {
        public Transform Hand;
        public Quaternion HandRestRot;      // 이 오브젝트 기준
        public Quaternion HandRestFrameInv; // 기본 자세 손 좌표계의 역회전
        public Quaternion HandIdleLocal;    // 부모 기준
        public Quaternion HandHeldLocal;    // 놓친 순간의 부모 기준 회전
        public FingerBone[] Fingers;        // 엄지·검지·중지·약지·새끼 × 뿌리·중간·끝. 없는 뼈는 Bone == null
        public bool Hidden;
        public float HiddenTime;
        public float IdleWeight;            // 0 = 트래킹 자세, 1 = Idle 자세
    }

    private HandRig leftRig, rightRig;
    private readonly HandTrack leftHand = new();
    private readonly HandTrack rightHand = new();

    // 몸 필터 (캐릭터 기준 좌표, 골반 원점)
    private readonly EuroVector3[] bodyFilter = new EuroVector3[BodyPointCount];
    private readonly Vector3[] bodyPos = new Vector3[BodyPointCount];
    private readonly bool[] bodyVisible = new bool[BodyPointCount];
    private int lastRestart;
    private bool needReset = true;
    private uint prevTimestamp;

    // 루트 필터
    private readonly EuroVector3 rootFilter = new();
    private Vector3 rootPos;          // 캐릭터 기준, 카메라 원점(z 음수)
    private bool hasRoot;
    private bool rootNeedReset = true;
    private uint rootPrevTimestamp;
    private Vector3 rootTargetBase;   // 시작할 때 rootTarget의 localPosition (씬에 배치한 자리)
    private Vector3 rootOrigin;       // 원점 (캐릭터 기준 루트 위치)
    private bool needOrigin = true;   // 다음 유효 루트 값으로 원점을 잡는다
    private Vector3 rootMoveTarget;   // 데드존을 거친 목표 이동량 (캐릭터 기준)
    private Vector3 rootMoveCurrent;  // 지금 적용 중인 이동량
    private Vector3 rootMoveVelocity; // SmoothDamp 내부 속도

    // 머리
    private Transform neckBone, headBone;
    private Quaternion neckRestRot, headRestRot; // 이 오브젝트 기준
    private Quaternion headFrontInv = Quaternion.identity;
    private Quaternion headLast = Quaternion.identity; // 마지막으로 추적한 머리 회전 (정면 보정 후)
    private float headLostTime;
    private float headReturnWeight; // 0 = 추적 회전, 1 = 정면

    private float poseLerp = 1f; // 이번 프레임의 보간 비율 (LateUpdate에서 계산)

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
        CaptureIdlePose(animator);
        if (rootTarget)
            rootTargetBase = rootTarget.localPosition;

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
        trackedRot = new Quaternion[Links.Length];
        hiddenTime = new float[Links.Length];
        downWeight = new float[Links.Length];

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
            trackedRot[i] = restRot[i];
        }

        // 상체: 척추 뼈 중 있는 것과, 기본 자세의 어깨선(왼위팔 → 오른위팔)을 저장한다.
        for (int k = 0; k < SpineBones.Length; k++)
        {
            spine[k] = animator.GetBoneTransform(SpineBones[k]); // Chest·UpperChest는 선택 뼈라 null일 수 있음
            if (spine[k])
            {
                spineRestRot[k] = toLocal * spine[k].rotation;
                hasSpine = true;
            }
        }

        Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (hasSpine && leftArm && rightArm)
            shoulderRestFrameInv = Quaternion.Inverse(ShoulderFrame(toLocal * (rightArm.position - leftArm.position)));
        else
        {
            Debug.LogWarning("[Humanoid] 척추 또는 위팔 뼈가 없어 상체는 움직이지 않습니다.");
            hasSpine = false;
        }

        neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
        headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (neckBone)
            neckRestRot = toLocal * neckBone.rotation;
        if (headBone)
            headRestRot = toLocal * headBone.rotation;
        headFrontInv = Quaternion.Inverse(FileIO.GetFaceFront);

        leftRig = CaptureHand(animator, toLocal, HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal);
        rightRig = CaptureHand(animator, toLocal, HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal);
    }

    // 기본 자세를 저장한 뒤에 불러야 한다. Idle을 읽고 나서 모든 Transform을 기본 자세로 되돌린다(명세 03 1c).
    private void CaptureIdlePose(Animator animator)
    {
        if (!idleClip)
        {
            Debug.LogWarning("[Humanoid] idleClip이 비어 있어 놓친 팔은 몸 옆으로 내리고 손·손가락은 기본 자세로 돌아갑니다. 인스펙터에 Idle 클립을 넣으세요.");
            return;
        }

        Transform[] all = GetComponentsInChildren<Transform>(true);
        var savedPos = new Vector3[all.Length];
        var savedRot = new Quaternion[all.Length];
        var savedScale = new Vector3[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            savedPos[i] = all[i].localPosition;
            savedRot[i] = all[i].localRotation;
            savedScale[i] = all[i].localScale;
        }

        Transform probe = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Quaternion probeRest = probe ? probe.localRotation : Quaternion.identity;

        idleClip.SampleAnimation(gameObject, 0f);
        if (probe && Quaternion.Angle(probe.localRotation, probeRest) < 0.01f)
        {
            PlayableGraph graph = PlayableGraph.Create("HumanoidIdleSample");
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Idle", animator);
            output.SetSourcePlayable(AnimationClipPlayable.Create(graph, idleClip));
            graph.Evaluate(0f);
            graph.Destroy();
        }

        if (probe && Quaternion.Angle(probe.localRotation, probeRest) >= 0.01f)
        {
            idleLocal = new Quaternion[Links.Length];
            for (int i = 0; i < Links.Length; i++)
            {
                if (bones[i])
                    idleLocal[i] = bones[i].localRotation;
            }
            hasIdle = true;
            CaptureHandIdle(leftRig);
            CaptureHandIdle(rightRig);
        }
        else
            Debug.LogWarning("[Humanoid] idleClip을 읽지 못해 기본 동작(팔 내리기, 손 기본 자세)을 씁니다. Humanoid 클립인지 확인하세요.");

        for (int i = 0; i < all.Length; i++)
        {
            all[i].localPosition = savedPos[i];
            all[i].localRotation = savedRot[i];
            all[i].localScale = savedScale[i];
        }
    }

    private static void CaptureHandIdle(HandRig rig)
    {
        if (rig.Hand)
            rig.HandIdleLocal = rig.Hand.localRotation;
        for (int i = 0; i < rig.Fingers.Length; i++)
        {
            if (rig.Fingers[i].Bone)
                rig.Fingers[i].IdleLocal = rig.Fingers[i].Bone.localRotation;
        }
    }

    private static void CaptureHandHeld(HandRig rig)
    {
        if (rig.Hand)
            rig.HandHeldLocal = rig.Hand.localRotation;
        for (int i = 0; i < rig.Fingers.Length; i++)
        {
            if (rig.Fingers[i].Bone)
                rig.Fingers[i].HeldLocal = rig.Fingers[i].Bone.localRotation;
        }
    }

    // HumanBodyBones에서 손가락 뼈는 엄지·검지·중지·약지·새끼 순서로 뿌리(Proximal)·중간·끝(Distal) 3개씩 이어져 있다.
    // firstFinger = LeftThumbProximal 또는 RightThumbProximal. 손가락 f, 마디 s의 뼈 = firstFinger + f*3 + s
    private static HumanBodyBones FingerBoneId(HumanBodyBones firstFinger, int f, int s) => firstFinger + f * 3 + s;

    // EHandParts에서 손가락 f의 첫 점: 엄지 1(cmc), 검지 5, 중지 9, 약지 13, 새끼 17. 마디 s = 점 (첫 점 + s) → (첫 점 + s + 1)
    private static int FingerPointBase(int f) => 1 + f * 4;

    private HandRig CaptureHand(Animator animator, Quaternion toLocal, HumanBodyBones handId, HumanBodyBones firstFinger)
    {
        var rig = new HandRig { Fingers = new FingerBone[15] };

        for (int f = 0; f < 5; f++)
        {
            for (int s = 0; s < 3; s++)
            {
                Transform bone = animator.GetBoneTransform(FingerBoneId(firstFinger, f, s));
                if (!bone)
                    continue;

                // 마디 방향: 다음 마디 뼈 → 없으면(끝 마디) 첫 자식 Transform → 없으면 부모에서 이어지는 방향
                Vector3 dir;
                Transform next = s < 2 ? animator.GetBoneTransform(FingerBoneId(firstFinger, f, s + 1)) : null;
                if (next)
                    dir = next.position - bone.position;
                else if (bone.childCount > 0)
                    dir = bone.GetChild(0).position - bone.position;
                else if (bone.parent)
                    dir = bone.position - bone.parent.position;
                else
                    continue;

                int i = f * 3 + s;
                rig.Fingers[i].Bone = bone;
                rig.Fingers[i].RestDir = toLocal * dir;
                rig.Fingers[i].RestRot = toLocal * bone.rotation;
                rig.Fingers[i].IdleLocal = bone.localRotation;
                rig.Fingers[i].From = FingerPointBase(f) + s;
                rig.Fingers[i].To = FingerPointBase(f) + s + 1;
            }
        }

        // 손 좌표계: 손목 → 중지 뿌리(앞), 새끼 뿌리 → 검지 뿌리(옆). 모델의 손가락 뿌리 뼈 위치로 잰다.
        Transform hand = animator.GetBoneTransform(handId);
        Transform index = animator.GetBoneTransform(FingerBoneId(firstFinger, 1, 0));
        Transform middle = animator.GetBoneTransform(FingerBoneId(firstFinger, 2, 0));
        Transform little = animator.GetBoneTransform(FingerBoneId(firstFinger, 4, 0));
        if (hand && index && middle && little)
        {
            rig.Hand = hand;
            rig.HandRestRot = toLocal * hand.rotation;
            rig.HandIdleLocal = hand.localRotation;
            rig.HandRestFrameInv = Quaternion.Inverse(HandFrame(
                toLocal * (middle.position - hand.position),
                toLocal * (index.position - little.position)));
        }
        else
            Debug.LogWarning($"[Humanoid] {handId}: 손 또는 검지·중지·새끼 뿌리 뼈가 없어 손 뼈는 움직이지 않습니다.");

        return rig;
    }

    // 손 좌표계. 기본 자세와 트래킹에 같은 식을 쓰므로 위 방향 부호(손바닥/손등)는 결과에 영향이 없다.
    private static Quaternion HandFrame(Vector3 forward, Vector3 side)
    {
        Vector3 up = Vector3.Cross(forward, side);
        if (forward.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f)
            return Quaternion.identity;
        return Quaternion.LookRotation(forward, up);
    }

    // 어깨선 방향(캐릭터 왼쪽 → 오른쪽)으로 상체 좌표계를 만든다.
    // 위 방향은 세계 위(+y) 기준이라 좌우 기울기(roll)와 비틀림(yaw)만 담기고, 앞뒤 숙임(pitch)은 담기지 않는다.
    private static Quaternion ShoulderFrame(Vector3 right)
    {
        Vector3 forward = Vector3.Cross(right, Vector3.up); // 오른쪽 × 위 = 앞 (+z)
        if (forward.sqrMagnitude < 1e-8f)
            return Quaternion.identity; // 어깨선이 수직이면 계산 불가
        Vector3 up = Vector3.Cross(forward, right);          // 어깨선에 수직인 위
        return Quaternion.LookRotation(forward, up);
    }

    // 프레임을 받을 때마다 필터만 갱신한다. 뼈·위치 적용은 LateUpdate.
    private void OnBodyFrame(int frameId)
    {
        BodyReceiver receiver = BodyReceiver.Instance;
        BodyFrame frame = receiver.Frame;

        if (receiver.RestartCount != lastRestart) // 트래커 재시작
        {
            lastRestart = receiver.RestartCount;
            needReset = true;
            rootNeedReset = true;
            leftHand.RequestReset();
            rightHand.RequestReset();
        }

        if (!frame.BodyDetected) // 놓치면 모든 점을 안 보임으로(팔은 내려감), 다시 잡히면 필터 초기화
        {
            System.Array.Clear(bodyVisible, 0, BodyPointCount);
            needReset = true;
            rootNeedReset = true;
            leftHand.RequestReset();
            rightHand.RequestReset();
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
            bodyVisible[i] = frame.PoseVisibility[i] > visibleThreshold;
        }

        UpdateRoot(frame);
        leftHand.Update(frame.LeftHandDetected, frame.LeftHand, frame.TimestampMs, handBeta);
        rightHand.Update(frame.RightHandDetected, frame.RightHand, frame.TimestampMs, handBeta);
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
        hasRoot = true;

        if (needOrigin)
        {
            rootOrigin = rootPos;
            needOrigin = false;
            Debug.Log("[Humanoid] 루트 원점을 잡았습니다.");
        }
    }

    // 지금 자리를 원점으로 다시 잡는다(다음 유효 루트 값에서 적용). 단축키·Controller가 부른다.
    [ContextMenu("Recenter (루트 원점 다시 잡기)")]
    public void Recenter()
    {
        needOrigin = true;
    }

    // 지금 머리 자세를 정면으로 저장한다. 모니터를 보며 누른다
    [ContextMenu("Recenter Head (머리 정면 저장)")]
    public void RecenterHead()
    {
        FaceReceiver face = FaceReceiver.Instance;
        if (!face || face.State != EReceiveState.Receiving || !face.Frame.Tracking)
        {
            Debug.LogWarning("[Humanoid] 얼굴을 추적 중일 때만 정면을 저장할 수 있습니다.");
            return;
        }
        Quaternion front = face.Frame.HeadRotation;
        headFrontInv = Quaternion.Inverse(front);
        FileIO.SaveFaceFront(front);
        Debug.Log("[Humanoid] 머리 정면을 저장했습니다.");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (recenterKey != Key.None && keyboard[recenterKey].wasPressedThisFrame)
            Recenter();

        if (headRecenterKey != Key.None && keyboard[headRecenterKey].wasPressedThisFrame)
            RecenterHead();

        if (lockRootKey != Key.None && keyboard[lockRootKey].wasPressedThisFrame)
        {
            lockRoot = !lockRoot;
            Debug.Log($"[Humanoid] 루트 이동 {(lockRoot ? "고정" : "해제")}");
        }
    }

    // Animator가 자세를 계산한 뒤에 덮어쓰도록 LateUpdate에서 적용한다.
    private void LateUpdate()
    {
        // 프레임 시간과 무관하게 같은 속도로 따라가는 지수 보간 비율
        poseLerp = poseSmoothTime > 0f ? 1f - Mathf.Exp(-Time.deltaTime / poseSmoothTime) : 1f;

        Quaternion space = transform.rotation;

        ApplyRoot(space);

        // 상체를 팔보다 먼저 돌린다. 팔은 상체의 자식이라, 팔을 먼저 놓고 상체를 돌리면 팔이 한 번 더 돌아간다.
        ApplySpine(space);

        ApplyHead(space);

        ApplyArms(space);

        // 손은 아래팔의 자식이라 팔 다음에 적용한다.
        // 손목 점이 안 보이면 팔이 멈춰 있으므로 손도 함께 멈춘다(이슈 #2)
        ApplyHand(leftRig, leftHand, space, bodyVisible[(int)EBodyParts.left_wrist]);
        ApplyHand(rightRig, rightHand, space, bodyVisible[(int)EBodyParts.right_wrist]);
    }

    // 안 보이는 동안의 회전은 부모 기준으로 두므로 팔이 내려가도 손이 아래팔에 붙어 간다.
    private void ApplyHand(HandRig rig, HandTrack track, Quaternion space, bool wristVisible)
    {
        if (rig == null)
            return;

        bool visible = track.Visible && wristVisible;
        float step = Time.deltaTime / armDownDuration;
        if (visible)
        {
            rig.Hidden = false;
            rig.HiddenTime = 0f;
            rig.IdleWeight = Mathf.MoveTowards(rig.IdleWeight, 0f, step);
        }
        else
        {
            if (!rig.Hidden)
            {
                rig.Hidden = true;
                CaptureHandHeld(rig);
            }
            rig.HiddenTime += Time.deltaTime;
            if (rig.HiddenTime > armDownDelay)
                rig.IdleWeight = Mathf.MoveTowards(rig.IdleWeight, 1f, step);
        }
        float idleBlend = Mathf.SmoothStep(0f, 1f, rig.IdleWeight);

        Vector3[] p = track.Pos; // 손목 원점, 캐릭터 기준

        // 손 회전(기본 자세 → 트래킹). 손 뼈가 없으면 identity
        Quaternion handDelta = Quaternion.identity;
        if (rig.Hand)
        {
            Quaternion target;
            if (visible)
            {
                Vector3 forward = p[(int)EHandParts.middle_finger_mcp] - p[(int)EHandParts.wrist];
                Vector3 side = p[(int)EHandParts.index_finger_mcp] - p[(int)EHandParts.pinky_mcp];
                handDelta = HandFrame(forward, side) * rig.HandRestFrameInv;
                target = space * (handDelta * rig.HandRestRot);
            }
            else
                target = rig.Hand.parent.rotation * rig.HandHeldLocal;
            if (applyHands)
                SetBlendedRotation(rig.Hand, target, rig.HandIdleLocal, idleBlend);
        }

        if (!applyFingers)
            return;

        // 뿌리 → 끝 순서로 적용한다. 각 마디에 절대 회전을 넣으므로 부모 마디의 영향은 덮어써진다.
        foreach (FingerBone finger in rig.Fingers)
        {
            if (!finger.Bone)
                continue;
            Quaternion target;
            if (visible)
            {
                Vector3 dir = p[finger.To] - p[finger.From];
                if (dir.sqrMagnitude < 1e-10f)
                    continue;
                // 손 회전을 먼저 적용한 자세에서 출발해 남은 방향 차이만 더한다.
                // 기본 자세에서 바로 돌리면 손의 비틀림(roll)을 물려받지 못해 마디 메시가 꼬인다.
                Vector3 handRestDir = handDelta * finger.RestDir;
                target = space * (Quaternion.FromToRotation(handRestDir, dir) * handDelta * finger.RestRot);
            }
            else
                target = finger.Bone.parent.rotation * finger.HeldLocal;
            SetBlendedRotation(finger.Bone, target, finger.IdleLocal, idleBlend);
        }
    }

    // target(세계 기준)과 Idle 자세(부모 기준)를 idleBlend로 섞어 넣는다
    private void SetBlendedRotation(Transform bone, Quaternion target, Quaternion idle, float idleBlend)
    {
        if (idleBlend > 0f)
            target = Quaternion.Slerp(target, bone.parent.rotation * idle, idleBlend);
        SetRotation(bone, target);
    }

    // 보이는 뼈는 트래킹 방향으로, 오래 안 보인 뼈는 내린 자세로. 둘 사이는 downWeight로 섞는다.
    private void ApplyArms(Quaternion space)
    {
        Quaternion toLocal = Quaternion.Inverse(space);
        float step = Time.deltaTime / armDownDuration;
        Quaternion upperRot = Quaternion.identity; // 이번 프레임에 넣은 위팔 회전(이 오브젝트 기준). 바로 다음 아래팔이 쓴다

        for (int i = 0; i < Links.Length; i++)
        {
            if (!bones[i])
                continue;

            int from = (int)Links[i].From;
            int to = (int)Links[i].To;
            if (bodyVisible[from] && bodyVisible[to]) // 보이지 않는 점은 추측값이라 쓰지 않음
            {
                Vector3 dir = bodyPos[to] - bodyPos[from]; // 캐릭터 기준 좌표
                if (dir.sqrMagnitude >= 1e-8f)
                {
                    // 기본 자세 방향을 트래킹 방향으로 돌리는 최소 회전. 뼈 축 비틀림(roll)은 반영되지 않는다.
                    trackedRot[i] = Quaternion.FromToRotation(restDir[i], dir) * restRot[i];
                }
                hiddenTime[i] = 0f;
                downWeight[i] = Mathf.MoveTowards(downWeight[i], 0f, step);
            }
            else
            {
                hiddenTime[i] += Time.deltaTime;
                if (hiddenTime[i] > armDownDelay)
                    downWeight[i] = Mathf.MoveTowards(downWeight[i], 1f, step);
            }

            Quaternion rot = trackedRot[i];
            if (downWeight[i] > 0f)
            {
                Quaternion down;
                if (hasIdle)
                    down = toLocal * bones[i].parent.rotation * idleLocal[i];
                else
                    down = IsUpperArm(i)
                        ? Quaternion.FromToRotation(restDir[i], ArmDownDir(i)) * restRot[i]
                        : upperRot * Quaternion.Inverse(restRot[i - 1]) * restRot[i]; // 위팔과의 기본 자세 관계 = 편 팔
                rot = Quaternion.Slerp(rot, down, Mathf.SmoothStep(0f, 1f, downWeight[i]));
            }
            SetRotation(bones[i], space * rot);
            if (IsUpperArm(i))
                upperRot = rot;
        }
    }

    // Links는 위팔 → 아래팔 순서로 짝지어 있다(아래팔 i의 위팔 = i − 1)
    private static bool IsUpperArm(int link) =>
        Links[link].Bone == HumanBodyBones.LeftUpperArm || Links[link].Bone == HumanBodyBones.RightUpperArm;

    // 내린 위팔 방향(캐릭터 기준). 아래에서 바깥(왼팔 −x, 오른팔 +x)으로 armDownOutAngle만큼 벌린다
    private Vector3 ArmDownDir(int link)
    {
        HumanBodyBones bone = Links[link].Bone;
        float side = bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.LeftLowerArm ? -1f : 1f;
        float a = armDownOutAngle * Mathf.Deg2Rad;
        return new Vector3(side * Mathf.Sin(a), -Mathf.Cos(a), 0f);
    }

    // rootTarget을 "배치한 자리 + (루트 위치 − 원점) × rootScale"로 옮긴다. 이동량은 캐릭터 기준이라
    // 캐릭터 회전으로 세계 방향으로 바꾼 뒤, rootTarget 부모의 좌표로 바꿔 넣는다. 단위는 미터.
    private void ApplyRoot(Quaternion space)
    {
        if (!rootTarget)
            return;
        if (!lockRoot && (!hasRoot || needOrigin))
            return;

        Vector3 move = lockRoot ? Vector3.zero : Vector3.Scale(rootPos - rootOrigin, rootScale);

        // 목표에서 rootDeadZone 넘게 벗어날 때만 목표를 옮기고, 실제 위치는 목표를 천천히 따라간다.
        // 0.1 격자로 끊으면 경계값(0.849 ↔ 0.851)에서 목표가 오가므로 데드존을 쓴다.
        if (lockRoot || (move - rootMoveTarget).sqrMagnitude > rootDeadZone * rootDeadZone)
            rootMoveTarget = move;
        rootMoveCurrent = Vector3.SmoothDamp(rootMoveCurrent, rootMoveTarget, ref rootMoveVelocity, rootSmoothTime);

        Vector3 offsetWorld = space * rootMoveCurrent;
        Transform parent = rootTarget.parent;
        Vector3 offsetLocal = parent ? Quaternion.Inverse(parent.rotation) * offsetWorld : offsetWorld;
        rootTarget.localPosition = rootTargetBase + offsetLocal;
    }

    // 어깨선 회전(delta)을 척추 뼈들에 나눠 준다. 자식 뼈는 부모 회전을 물려받으므로
    // 각 뼈에 "그 뼈까지의 누적 비중"만큼 돌린 절대 회전을 넣는다. 맨 위 뼈는 delta 전체가 된다.
    private void ApplySpine(Quaternion space)
    {
        int l = (int)EBodyParts.left_shoulder;
        int r = (int)EBodyParts.right_shoulder;
        if (!hasSpine || !bodyVisible[l] || !bodyVisible[r])
            return; // 이전 회전 유지

        Vector3 right = bodyPos[r] - bodyPos[l]; // 미러 후 캐릭터 기준: 왼어깨 → 오른어깨 = +x 쪽
        if (right.sqrMagnitude < 1e-8f)
            return;

        Quaternion delta = ShoulderFrame(right) * shoulderRestFrameInv; // 기본 어깨선 → 트래킹 어깨선
        delta = Quaternion.Slerp(Quaternion.identity, delta, chestWeight);

        float total = 0f;
        for (int k = 0; k < spine.Length; k++)
        {
            if (spine[k])
                total += SpineWeight(k);
        }
        if (total <= 0f)
            return;

        float cumulative = 0f;
        for (int k = 0; k < spine.Length; k++)
        {
            if (!spine[k])
                continue;
            cumulative += SpineWeight(k) / total;
            SetRotation(spine[k], space * (Quaternion.Slerp(Quaternion.identity, delta, cumulative) * spineRestRot[k]));
        }
    }

    private void ApplyHead(Quaternion space)
    {
        FaceReceiver face = FaceReceiver.Instance;
        if (!applyHead || !headBone || !face || face.State == EReceiveState.None)
            return;

        if (face.State == EReceiveState.Receiving && face.Frame.Tracking)
        {
            // 폰 카메라 기준 → 캐릭터 기준: y 180° 차이라 x·z 부호를 바꾼다(2026-11-24 실측: 위아래 반대였음)
            Quaternion q = headFrontInv * face.Frame.HeadRotation;
            headLast = new Quaternion(-q.x, q.y, -q.z, q.w);
            headLostTime = 0f;
            headReturnWeight = Mathf.MoveTowards(headReturnWeight, 0f, Time.deltaTime / headReturnDuration);
        }
        else
        {
            headLostTime += Time.deltaTime;
            if (headLostTime > headHoldTime)
                headReturnWeight = Mathf.MoveTowards(headReturnWeight, 1f, Time.deltaTime / headReturnDuration);
        }

        Quaternion delta = Quaternion.Slerp(headLast, Quaternion.identity, Mathf.SmoothStep(0f, 1f, headReturnWeight));
        if (neckBone)
            SetRotation(neckBone, space * (Quaternion.Slerp(Quaternion.identity, delta, neckWeight) * neckRestRot));
        SetRotation(headBone, space * (delta * headRestRot));
    }

    // 뼈를 목표 회전(세계 기준) 쪽으로 poseLerp만큼 돌린다. 뼈 회전은 모두 이 함수로 넣는다.
    // Animator Controller가 비어 있어 지난 프레임 회전이 그대로 남아 있으므로, 그 회전에서 출발한다.
    private void SetRotation(Transform bone, Quaternion target)
    {
        bone.rotation = poseLerp >= 1f ? target : Quaternion.Slerp(bone.rotation, target, poseLerp);
    }

    private float SpineWeight(int k) =>
        spineWeights != null && k < spineWeights.Length ? Mathf.Max(0f, spineWeights[k]) : 0f;
}
