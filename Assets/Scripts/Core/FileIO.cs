using System;
using UnityEngine;
using System.IO;

public class FileIO : SingletonMonoBehaviour<FileIO>
{
    private static readonly string filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VishaSTD", "vishamos", "config.json");
    private static SynapseConfig cfg;
    public static string GetServer => cfg.ServerHost;
    public static int GetPort => cfg.ServerPort;

    protected override void OnAwake()
    {
        Debug.Log(filePath);
        if (File.Exists(filePath))
        {
            string txt = File.ReadAllText(filePath);
            cfg = JsonUtility.FromJson<SynapseConfig>(txt);
            Debug.Log(filePath);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            cfg = new SynapseConfig();
            File.WriteAllText(filePath, JsonUtility.ToJson(cfg, true));
        }
    }
}


[Serializable]
public class SynapseConfig
{
    public string ServerHost;
    public int ServerPort = 4000;
}