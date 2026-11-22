using UnityEngine;

public class MainManager : SingletonMonoBehaviour<MainManager>
{
    private MainCharacter mainCharacter;
    

    public MainCharacter MainCharacter
    {
        get => mainCharacter;
        set
        {
            if (!mainCharacter)
                mainCharacter = value;
        }
    }

    public void SendBodyData(byte[] data)
    {
        mainCharacter.BodyBytes = data;
    }
}
