using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    [SerializeField]
    GameObject mouth;
    [SerializeField]
    SpriteRenderer body;

    float mouthMin = 0.2f;

    [SerializeField]
    public float minVolume = -90.0f;

    void Start()
    {
        
    }

    void Update()
    {
        float volume = MicManager.Instance.DbValue;
        if (volume < minVolume)
            volume = minVolume;
        else if( volume > 0)
            volume = 0.0f;
        
        mouth.transform.localPosition = Vector3.down * ((volume - minVolume) / -minVolume) * mouthMin;
        // if(Input.GetKeyDown(KeyCode.RightArrow))
        // {
        //     //Sprite s = Resources.Load("Texture/Base/Body_Base", typeof(Sprite)) as Sprite;
        //     if (s != null)
        //     {
        //         Debug.Log("OK");
        //         body.sprite = s;
        //     }
        //     
        // }
        // if (Input.GetKeyDown(KeyCode.LeftArrow))
        // {
        //     //Sprite s = Resources.Load("Texture/Gordon/Body_Gordon", typeof(Sprite)) as Sprite;
        //     if (s != null)
        //     {
        //         Debug.Log("OK");
        //         body.sprite = s;
        //     }
        //
        // }
    }

}
