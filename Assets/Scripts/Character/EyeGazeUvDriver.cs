using UnityEngine;

// 시선 → 눈 머티리얼 텍스처 Offset. 명세 04 §1a 1e-6
// 텍스처는 Wrap Mode Clamp, 가장자리 흰색이어야 한다
public class EyeGazeUvDriver : EyeGazeDriver
{
    [SerializeField] private Renderer targetRenderer; // 비우면 이 오브젝트의 Renderer
    [SerializeField] [Min(0)] private int materialIndex;
    [SerializeField] private string propertyName = "_BaseMap";
    [SerializeField] private Vector2 maxOffset = new Vector2(0.15f, 0.15f); // 부호로 방향을 뒤집는다

    private Material[] instances;
    private Material material;
    private int propertyId;
    private Vector2 baseOffset;

    private void Awake()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();
        if (!targetRenderer)
        {
            Disable("Renderer가 없습니다. Target Renderer에 Eye 메시를 지정하세요.");
            return;
        }

        // 인스턴스 머티리얼을 써서 에셋 원본을 바꾸지 않는다
        instances = targetRenderer.materials;
        if (materialIndex >= instances.Length)
        {
            Disable($"머티리얼 슬롯 {materialIndex}이 없습니다(슬롯 {instances.Length}개). Material Index를 고치세요.");
            return;
        }

        material = instances[materialIndex];
        propertyId = Shader.PropertyToID(propertyName);
        if (!material.HasProperty(propertyId))
        {
            material = null;
            Disable($"머티리얼에 {propertyName} 속성이 없습니다. Property Name을 셰이더의 텍스처 속성으로 고치세요.");
            return;
        }
        baseOffset = material.GetTextureOffset(propertyId);
    }

    public override void Apply(Vector2 gaze)
    {
        if (material)
            material.SetTextureOffset(propertyId, baseOffset + Vector2.Scale(gaze, maxOffset));
    }

    private void OnDestroy()
    {
        if (instances == null)
            return;
        for (int i = 0; i < instances.Length; i++)
            Destroy(instances[i]);
    }

    private void Disable(string message)
    {
        Debug.LogWarning($"[EyeGaze] {name}: {message}");
        enabled = false;
    }
}
