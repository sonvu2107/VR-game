using System;
using System.IO;
using UnityEngine;

[Serializable]
public class LabyrinthSaveData
{
    public int highScore;
    public int highestLevel;
}

public static class SaveSystem
{
    private static LabyrinthSaveData data;

    public static string FilePath =>
        System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    public static LabyrinthSaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                data = JsonUtility.FromJson<LabyrinthSaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Không đọc được save.json: {e.Message}");
        }

        if (data == null)
        {
            data = new LabyrinthSaveData();
            data.highScore = PlayerPrefs.GetInt("HighScore", 0);
        }

        // Tạo file ngay lần đầu để dễ kiểm tra.
        if (!File.Exists(FilePath))
            Save();

        Debug.Log($"[Save] File lưu: {FilePath}");
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Không ghi được save.json: {e.Message}");
        }
    }
}