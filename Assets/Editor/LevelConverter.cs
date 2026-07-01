using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class LevelConverter : EditorWindow
{
    [MenuItem("Tools/Convert Levels to JSON")]
    public static void ConvertLevels()
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        string jsonOutputPath = Path.Combine(resourcesPath, "LevelsJSON");

        if (!Directory.Exists(jsonOutputPath))
        {
            Directory.CreateDirectory(jsonOutputPath);
        }

        // 1. Minimum hamle sayılarını oku
        string minMovesPath = Path.Combine(resourcesPath, "minimun_moves.txt");
        int[,] minMovesArray = new int[20, 20]; // 20 World x 20 Level varsayımı
        if (File.Exists(minMovesPath))
        {
            string[] lines = File.ReadAllLines(minMovesPath);
            for (int w = 0; w < lines.Length; w++)
            {
                if (string.IsNullOrWhiteSpace(lines[w])) continue;
                string[] tokens = lines[w].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int l = 0; l < tokens.Length; l++)
                {
                    if (l < 20 && w < 20)
                    {
                        int.TryParse(tokens[l], out minMovesArray[w, l]);
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("minimun_moves.txt bulunamadı! Min hamleler 0 olarak atanacak.");
        }

        // 2. İpuçlarını oku
        string hintMovesPath = Path.Combine(resourcesPath, "hint_moves.txt");
        string[] hintLines = File.Exists(hintMovesPath) ? File.ReadAllLines(hintMovesPath) : new string[0];

        // 3. Her wXlY.txt dosyasını tara ve JSON'a dönüştür
        int convertedCount = 0;

        for (int world = 1; world <= 20; world++)
        {
            for (int level = 1; level <= 20; level++)
            {
                string txtFileName = $"w{world}l{level}.txt";
                string txtFilePath = Path.Combine(resourcesPath, txtFileName);

                if (!File.Exists(txtFilePath)) continue; // Eğer böyle bir level dosyası yoksa geç

                // Harita layout'unu oku
                string[] layoutLines = File.ReadAllLines(txtFilePath);
                List<string> layoutList = new List<string>();

                foreach (var line in layoutLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    layoutList.Add(line.Trim());
                }

                // İpucu hamlelerini al
                int hintLineIndex = (world - 1) * 20 + (level - 1);
                string hints = "";
                if (hintLineIndex < hintLines.Length && !string.IsNullOrWhiteSpace(hintLines[hintLineIndex]))
                {
                    hints = hintLines[hintLineIndex].Trim();
                }

                // Min hamle sayısını al
                int minMoves = minMovesArray[world - 1, level - 1];

                // Veri nesnesini hazırla
                LevelData data = new LevelData
                {
                    worldIndex = world,
                    levelIndex = level,
                    minMoves = minMoves,
                    hintMoves = hints,
                    layout = layoutList.ToArray()
                };

                // JSON'a çevir ve kaydet
                string jsonString = JsonUtility.ToJson(data, true);
                string jsonFileName = $"w{world}l{level}.json";
                string jsonFilePath = Path.Combine(jsonOutputPath, jsonFileName);

                File.WriteAllText(jsonFilePath, jsonString);
                convertedCount++;
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Dönüşüm Tamamlandı", 
            $"{convertedCount} adet level başarıyla 'Assets/Resources/LevelsJSON/' klasörüne JSON formatında kaydedildi.", "Tamam");
    }
}
