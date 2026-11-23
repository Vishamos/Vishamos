using UnityEngine;

public class SimpleCharacter : MonoBehaviour
{
    [SerializeField] GameObject mouth;
    [SerializeField] SpriteRenderer body;
    [SerializeField] public float minVolume = -90.0f;
    float mouthMin = 0.2f;

    private void Update()
    {
        float volume = MicManager.Instance.DbValue;
        if (volume < minVolume)
            volume = minVolume;
        else if (volume > 0)
            volume = 0.0f;

        mouth.transform.localPosition = Vector3.down * ((volume - minVolume) / -minVolume * mouthMin);
    }


}
