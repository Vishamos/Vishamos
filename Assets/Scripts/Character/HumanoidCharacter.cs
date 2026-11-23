using MyMath;
using UnityEngine;
using UnityEngine.InputSystem;

// Body 프레임(BodyReceiver)을 직접 받아 필터링하고, 휴머노이드 뼈를 돌린다. MainCharacter와 독립.
// 점 사이 "방향"만 쓰므로 사람과 모델의 체형·크기 차이와 무관하다(Body §4).
//
// 배치: Humanoid 리그 모델의 루트(Animator가 있는 오브젝트)에 붙인다.
//       데이터는 캐릭터가 +z를 바라보는 기준이고 모델도 +z를 보므로, 이 오브젝트를 y 180° 돌리면 카메라를 마주 본다.
//       Animator Controller는 비워 둔다(있으면 매 프레임 자세를 덮어씀).
// 팔:   위팔·아래팔 4개 뼈. 두 점 중 하나라도 보이지 않으면 그 뼈는 이전 회전을 유지한다.
// 상체: 어깨선으로 좌우 기울기·비틀림을 구해 척추 뼈(Spine·Chest·UpperChest 중 있는 것)에 나눠 건다.
//       앞뒤 숙임은 골반이 추측값이라 넣지 않는다(Body §3.1).
// 루트: rootTarget을 지정하면 루트 위치(카메라 기준 골반 위치)의 "원점에서 움직인 양 × rootScale"만큼 옮긴다.
//       원점은 시작 후 첫 유효 값으로 자동으로 잡고, Recenter()로 다시 잡는다. 놓침·재시작 때는 유지한다(plan_review 2-9).
//       lockRootY가 켜져 있으면 위아래 이동을 막는다(F10으로 켜고 끔).
// 손:   손목·중지 뿌리·검지 뿌리·새끼 뿌리 점으로 손 좌표계를 만들어 손 뼈를 돌리고, 손가락 마디는 점 방향으로 돌린다.
//       기본 자세와 트래킹에 같은 계산을 쓰므로 왼손·오른손의 손바닥 방향 부호를 따로 맞출 필요가 없다.
//       손바닥 회전(LeftPalm)은 쓰지 않는다. 손가락 뼈는 Humanoid 선택 뼈라 모델에 없으면 그 부분만 건너뛴다.
[RequireComponent(typeof(Animator))]
public class HumanoidCharacter : MonoBehaviour
{
    private const int BodyPointCount = (int)EBodyParts.max;
    private const float VisibleThreshold = 0.4f;

    [SerializeField] [Range(0.0f, 10f)] private float beta = 0f;

    [Header("상체")]
    [SerializeField] [Range(0.0f, 1f)] private float chestWeight = 1f; // 상체 회전을 얼마나 반영할지 (0 = 고정)
    // 척추 뼈별 비중 (Spine, Chest, UpperChest 순). 모델에 없는 뼈는 빼고 나머지로 다시 나눈다.
    [SerializeField] private float[] spineWeights = { 0.3f, 0.35f, 0.35f };

    [Header("루트 위치")]
    [SerializeField] private Transform rootTarget; // 비우면 루트 위치를 쓰지 않는다
    [SerializeField] private Vector3 rootScale = Vector3.one; // 축별 이동 배율. 0이면 그 축 이동을 끈다
    [SerializeField] private Key recenterKey = Key.F9;        // 원점 다시 잡기. Vishamos 창에 포커스가 있을 때만 받는다
    [SerializeField] private bool lockRootY;                  // 위아래 이동 막기
    [SerializeField] private Key lockRootYKey = Key.F10;      // lockRootY 켜고 끄기

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
        public int From, To;       // EHandParts 번호. 트래킹 방향 = Pos[To] − Pos[From]
    }

    private class HandRig
    {
        public Transform Hand;
        public Quaternion HandRestRot;      // 이 오브젝트 기준
        public Quaternion HandRestFrameInv; // 기본 자세 손 좌표계의 역회전
        public FingerBone[] Fingers;        // 엄지·검지·중지·약지·새끼 × 뿌리·중간·끝. 없는 뼈는 Bone == null
    }

    private HandRig leftRig, rightRig;
    private readonly HandTrack leftHand = new();
    private readonly HandTrack rightHand = new();

    // 몸 필터 (캐릭터 기준 좌표, 골반 원점)
    private readonly EuroVector3[] bodyFilter = new EuroVector3[BodyPointCount];
    private readonly Vector3[] bodyPos = new Vector3[BodyPointCount];
    private readonly bool[] bodyVisible = new bool[BodyPointCount];
    private bool hasBody;
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

        leftRig = CaptureHand(animator, toLocal, HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal);
        rightRig = CaptureHand(animator, toLocal, HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal);
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

        if (!frame.BodyDetected) // 놓치면 그대로 두고, 다시 잡히면 초기화
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

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (recenterKey != Key.None && keyboard[recenterKey].wasPressedThisFrame)
            Recenter();

        if (lockRootYKey != Key.None && keyboard[lockRootYKey].wasPressedThisFrame)
        {
            lockRootY = !lockRootY;
            Debug.Log($"[Humanoid] 루트 위아래 이동 {(lockRootY ? "고정" : "해제")}");
        }
    }

    // Animator가 자세를 계산한 뒤에 덮어쓰도록 LateUpdate에서 적용한다.
    private void LateUpdate()
    {
        if (!hasBody)
            return;

        Quaternion space = transform.rotation;

        ApplyRoot(space);

        // 상체를 팔보다 먼저 돌린다. 팔은 상체의 자식이라, 팔을 먼저 놓고 상체를 돌리면 팔이 한 번 더 돌아간다.
        ApplySpine(space);

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

        // 손은 아래팔의 자식이라 팔 다음에 적용한다.
        ApplyHand(leftRig, leftHand, space);
        ApplyHand(rightRig, rightHand, space);
    }

    private void ApplyHand(HandRig rig, HandTrack track, Quaternion space)
    {
        if (rig == null || !track.Visible)
            return; // 이전 회전 유지

        Vector3[] p = track.Pos; // 손목 원점, 캐릭터 기준

        if (applyHands && rig.Hand)
        {
            Vector3 forward = p[(int)EHandParts.middle_finger_mcp] - p[(int)EHandParts.wrist];
            Vector3 side = p[(int)EHandParts.index_finger_mcp] - p[(int)EHandParts.pinky_mcp];
            Quaternion delta = HandFrame(forward, side) * rig.HandRestFrameInv;
            rig.Hand.rotation = space * (delta * rig.HandRestRot);
        }

        if (!applyFingers)
            return;

        // 뿌리 → 끝 순서로 적용한다. 각 마디에 절대 회전을 넣으므로 부모 마디의 영향은 덮어써진다.
        foreach (FingerBone finger in rig.Fingers)
        {
            if (!finger.Bone)
                continue;
            Vector3 dir = p[finger.To] - p[finger.From];
            if (dir.sqrMagnitude < 1e-10f)
                continue;
            finger.Bone.rotation = space * (Quaternion.FromToRotation(finger.RestDir, dir) * finger.RestRot);
        }
    }

    // rootTarget을 "배치한 자리 + (루트 위치 − 원점) × rootScale"로 옮긴다. 이동량은 캐릭터 기준이라
    // 캐릭터 회전으로 세계 방향으로 바꾼 뒤, rootTarget 부모의 좌표로 바꿔 넣는다. 단위는 미터.
    private void ApplyRoot(Quaternion space)
    {
        if (!rootTarget || !hasRoot || needOrigin)
            return;

        Vector3 move = Vector3.Scale(rootPos - rootOrigin, rootScale);
        if (lockRootY)
            move.y = 0f;
        Vector3 offsetWorld = space * move;
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
            spine[k].rotation = space * (Quaternion.Slerp(Quaternion.identity, delta, cumulative) * spineRestRot[k]);
        }
    }

    private float SpineWeight(int k) =>
        spineWeights != null && k < spineWeights.Length ? Mathf.Max(0f, spineWeights[k]) : 0f;
}
