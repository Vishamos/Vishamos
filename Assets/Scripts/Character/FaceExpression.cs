using System;
using System.Collections.Generic;
using UnityEngine;

// Face 블렌드셰이프(ARKit 52개)를 모델 블렌드셰이프로 옮긴다. 대응표는 인스펙터에서 캐릭터마다 바꾼다.
// 기본값은 표준: ARKit 52개를 같은 이름의 블렌드셰이프에 1:1(범위 0∼1), 모음은 음소 이름과 같은 블렌드셰이프(명세 03 §3).
// 모델에 없는 이름은 건너뛴다(시작할 때 경고 한 줄). 추적이 끊기면 잠시 유지 후 무표정으로(머리와 같은 규칙, plan_review 3-5).
// 입 모음: uLipSync 음소 비율 × uLipSync 음량 × Face jawOpen(보정 후). 모양은 소리, 벌림은 얼굴.
//          lipSync를 비우면 Face만으로 첫 모음(A)에 jawOpen을 건다.
public class FaceExpression : MonoBehaviour
{
    [Serializable]
    public class Mapping
    {
        public string target;               // 모델 블렌드셰이프 이름. "blendShape1.MTH_A"처럼 앞에 붙은 이름은 무시하고 비교
        public EFaceBlendShape[] sources;   // 이 중 가장 큰 값을 쓴다
        [Range(0f, 1f)] public float inMin; // 이 값 이하 = 0
        [Range(0f, 1f)] public float inMax; // 이 값 이상 = 최대 (사람마다 1에 못 미침, Face §5)
        [Range(0f, 100f)] public float maxWeight = 100f;

        public Mapping(string target, float inMin, float inMax, params EFaceBlendShape[] sources)
        {
            this.target = target;
            this.sources = sources;
            this.inMin = inMin;
            this.inMax = inMax;
        }
    }

    [Serializable]
    public class Vowel
    {
        public string phoneme; // uLipSync 프로필의 음소 이름
        public string target;
        [Range(0f, 100f)] public float maxWeight = 100f;

        public Vowel(string phoneme, string target)
        {
            this.phoneme = phoneme;
            this.target = target;
        }
    }

    [SerializeField] private List<Mapping> mappings = DefaultMappings();

    [Header("입 모음")]
    [SerializeField] private uLipSync.uLipSync lipSync;
    [SerializeField] [Range(0f, 1f)] private float jawMin = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float jawMax = 0.5f;
    [SerializeField] private List<Vowel> vowels = new()
    {
        new("A", "A"),
        new("I", "I"),
        new("U", "U"),
        new("E", "E"),
        new("O", "O"),
    };

    [Header("시간")]
    [SerializeField] [Range(0f, 0.2f)] private float smoothTime = 0.05f;
    [SerializeField] [Min(0f)] private float holdTime = 0.5f;
    [SerializeField] [Min(0.01f)] private float returnDuration = 0.5f;

    private struct Slot
    {
        public SkinnedMeshRenderer Renderer;
        public int Index;
        public int Channel; // 0..mappings-1 = 표정, 그 뒤 = 모음
    }

    private Slot[] slots;
    private float[] current;
    private float lostTime;
    private float returnWeight;

    private static List<Mapping> DefaultMappings()
    {
        var list = new List<Mapping>();
        for (int i = 0; i < (int)EFaceBlendShape.max; i++)
            list.Add(new Mapping(((EFaceBlendShape)i).ToString(), 0f, 1f, (EFaceBlendShape)i));
        return list;
    }

    private void Start()
    {
        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var found = new List<Slot>();
        var missing = new List<string>();
        for (int m = 0; m < mappings.Count; m++)
            FindSlots(renderers, mappings[m].target, m, found, missing);
        for (int v = 0; v < vowels.Count; v++)
            FindSlots(renderers, vowels[v].target, mappings.Count + v, found, missing);
        slots = found.ToArray();
        current = new float[mappings.Count + vowels.Count];

        if (missing.Count > 0)
            Debug.LogWarning($"[Expression] 모델에 없는 블렌드셰이프 {missing.Count}개(건너뜀): {string.Join(", ", missing)}");
    }

    private static void FindSlots(SkinnedMeshRenderer[] renderers, string target, int channel, List<Slot> found, List<string> missing)
    {
        int before = found.Count;
        foreach (var r in renderers)
        {
            Mesh mesh = r.sharedMesh;
            if (!mesh)
                continue;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                if (name == target || name.EndsWith("." + target))
                    found.Add(new Slot { Renderer = r, Index = i, Channel = channel });
            }
        }
        if (found.Count == before)
            missing.Add(target);
    }

    private void LateUpdate()
    {
        FaceReceiver face = FaceReceiver.Instance;
        if (slots == null || !face || face.State == EReceiveState.None)
            return;

        bool tracking = face.State == EReceiveState.Receiving && face.Frame.Tracking;
        if (tracking)
        {
            lostTime = 0f;
            returnWeight = Mathf.MoveTowards(returnWeight, 0f, Time.deltaTime / returnDuration);
        }
        else
        {
            lostTime += Time.deltaTime;
            if (lostTime > holdTime)
                returnWeight = Mathf.MoveTowards(returnWeight, 1f, Time.deltaTime / returnDuration);
        }

        float keep = 1f - Mathf.SmoothStep(0f, 1f, returnWeight);
        float lerp = smoothTime > 0f ? 1f - Mathf.Exp(-Time.deltaTime / smoothTime) : 1f;
        float[] shapes = face.Frame.BlendShapes;

        for (int m = 0; m < mappings.Count; m++)
        {
            Mapping map = mappings[m];
            float v = 0f;
            foreach (var s in map.sources)
                v = Mathf.Max(v, shapes[(int)s]);
            float target = Mathf.InverseLerp(map.inMin, map.inMax, v) * map.maxWeight * keep;
            current[m] = Mathf.Lerp(current[m], target, lerp);
        }

        float open = Mathf.InverseLerp(jawMin, jawMax, shapes[(int)EFaceBlendShape.jawOpen]) * keep;
        var ratios = lipSync ? lipSync.result.phonemeRatios : null;
        float volume = lipSync ? lipSync.result.volume : 1f;
        for (int v = 0; v < vowels.Count; v++)
        {
            float ratio;
            if (ratios == null)
                ratio = v == 0 ? 1f : 0f;
            else if (!ratios.TryGetValue(vowels[v].phoneme, out ratio))
                ratio = 0f;
            int c = mappings.Count + v;
            current[c] = Mathf.Lerp(current[c], ratio * volume * open * vowels[v].maxWeight, lerp);
        }

        foreach (var slot in slots)
            slot.Renderer.SetBlendShapeWeight(slot.Index, current[slot.Channel]);
    }
}
