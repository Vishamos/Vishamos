using System;
using UnityEngine;
using System.IO;

public class FileIO : SingletonMonoBehaviour<FileIO>
{
    private static readonly string filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VishaSTD", "vishamos", "config.json");
    private static SynapseConfig cfg;
    public static string GetServer => cfg?.ServerHost;
    public static int GetPort => cfg?.ServerPort ?? 4000;
    public static float GetReconnectSeconds => Mathf.Max(0.1f, cfg?.ReconnectSeconds ?? 3f);

    // 2.4: 주소가 없으면 접속하지 않는다
    public static bool HasServer => !string.IsNullOrWhiteSpace(cfg?.ServerHost);

    protected override void OnAwake()
    {
        if (!File.Exists(filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            cfg = new SynapseConfig();
            File.WriteAllText(filePath, JsonUtility.ToJson(cfg, true));
            Debug.LogError($"[Config] 설정 파일이 없어 새로 만들었습니다. ServerHost를 채우고 다시 실행하세요: {filePath}");
            return;
        }

        try
        {
            cfg = JsonUtility.FromJson<SynapseConfig>(File.ReadAllText(filePath));
        }
        catch (Exception e)
        {
            cfg = null;
            Debug.LogError($"[Config] 설정 파일을 읽지 못했습니다({e.Message}). 고친 뒤 다시 실행하세요: {filePath}");
            return;
        }

        if (!HasServer)
            Debug.LogError($"[Config] ServerHost가 비어 있어 접속하지 않습니다. 채우고 다시 실행하세요: {filePath}");
    }
}


[Serializable]
public class SynapseConfig
{
    public string ServerHost;
    public int ServerPort = 4000;
    public float ReconnectSeconds = 3f;
}
