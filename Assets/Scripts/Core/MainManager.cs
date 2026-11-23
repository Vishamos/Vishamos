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
}
