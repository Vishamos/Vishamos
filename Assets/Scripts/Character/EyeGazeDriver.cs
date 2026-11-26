using UnityEngine;

// 시선 적용 방식. EyeGaze가 매 프레임 시선 2축(가로·세로 -1∼1)을 넘긴다. 명세 04 §1a 1e-1
public abstract class EyeGazeDriver : MonoBehaviour
{
    public abstract void Apply(Vector2 gaze);
}
