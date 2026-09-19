using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class UIColorChanger : EditorWindow
{
    private Color targetColor = new Color32(0, 226, 255, 255); // Kullanıcının istediği varsayılan turkuaz/cyan rengi
    private bool includeText = true; // Yazı renkleri de değiştirilsin mi?

    [MenuItem("Tools/UI Color Changer")]
    public static void ShowWindow()
    {
        GetWindow<UIColorChanger>("UI Color Changer");
    }

    private void OnGUI()
    {
        GUILayout.Label("UI Color Changer (URP)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetColor = EditorGUILayout.ColorField("Target Neon Color", targetColor);
        includeText = EditorGUILayout.Toggle("Include Text Elements", includeText);

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply Color to Active Scene UI"))
        {
            ApplyColorToActiveScene();
        }
    }

    private void ApplyColorToActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("No active scene loaded!");
            return;
        }

        // Sahnedeki tüm Canvas nesnelerini bul
        var canvases = Object.FindObjectsOfType<Canvas>(true);
        if (canvases.Length == 0)
        {
            Debug.LogWarning("No Canvas found in the active scene!");
            return;
        }

        int imagesUpdated = 0;
        int textsUpdated = 0;
        int outlinesUpdated = 0;

        foreach (var canvas in canvases)
        {
            // 1. Image (Görsel ve Arka Plan) Bileşenleri
            var images = canvas.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (IsColorBlueish(img.color))
                {
                    // Rengi değiştirirken orijinal şeffaflığı (Alpha) koruyoruz!
                    Color newColor = new Color(targetColor.r, targetColor.g, targetColor.b, img.color.a);
                    img.color = newColor;
                    EditorUtility.SetDirty(img);
                    imagesUpdated++;
                }
            }

            // 2. Text (Metin) Bileşenleri (İsteğe bağlı)
            if (includeText)
            {
                var texts = canvas.GetComponentsInChildren<Text>(true);
                foreach (var txt in texts)
                {
                    if (IsColorBlueish(txt.color))
                    {
                        Color newColor = new Color(targetColor.r, targetColor.g, targetColor.b, txt.color.a);
                        txt.color = newColor;
                        EditorUtility.SetDirty(txt);
                        textsUpdated++;
                    }
                }
            }

            // 3. Shadow / Outline (Gölge ve Çerçeve) Efektleri
            var outlines = canvas.GetComponentsInChildren<Outline>(true);
            foreach (var outline in outlines)
            {
                if (IsColorBlueish(outline.effectColor))
                {
                    Color newColor = new Color(targetColor.r, targetColor.g, targetColor.b, outline.effectColor.a);
                    outline.effectColor = newColor;
                    EditorUtility.SetDirty(outline);
                    outlinesUpdated++;
                }
            }
        }

        // Değişiklikleri kaydet (Sadece Play Mode aktif değilse!)
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"<color=lime>[UIColorChanger]</color> Success! Updated in active scene: {imagesUpdated} Images, {textsUpdated} Texts, {outlinesUpdated} Outlines.");
            EditorUtility.DisplayDialog("UI Color Changer", 
                $"Successfully updated UI elements permanently:\n- Images: {imagesUpdated}\n- Texts: {textsUpdated}\n- Outlines: {outlinesUpdated}\n\n(Original transparencies were preserved!)", 
                "OK");
        }
        else
        {
            Debug.Log($"<color=orange>[UIColorChanger]</color> Play Mode Warning! Temporarily updated: {imagesUpdated} Images, {textsUpdated} Texts, {outlinesUpdated} Outlines in memory. Exit Play Mode and apply again to make them permanent!");
            EditorUtility.DisplayDialog("UI Color Changer (Play Mode)", 
                $"Temporarily updated UI elements in memory (Play Mode):\n- Images: {imagesUpdated}\n- Texts: {textsUpdated}\n- Outlines: {outlinesUpdated}\n\nNOTE: These changes will reset when you stop Play Mode! Exit Play Mode and click again to save them permanently.", 
                "OK");
        }
    }

    /// <summary>
    /// Rengin mavi/turkuaz (neon) tonlarında olup olmadığını HSV kullanarak kontrol eder.
    /// Bu sayede farklı parlaklık ve şeffaflıktaki mavi tonlarını kolayca yakalarız.
    /// </summary>
    private bool IsColorBlueish(Color color)
    {
        // Beyaz, siyah ve gri tonlarını elemek için doygunluğu kontrol edelim
        Color.RGBToHSV(color, out float h, out float s, out float v);
        
        // Mavi/Turkuaz Hue (Ton) aralığı: 160 derece (0.44) ile 260 derece (0.72) arasıdır.
        // Doygunluk (Saturation) ve Parlaklık (Value) 0.15'ten büyük olmalı ki nötr renkler elensin.
        return (h >= 0.44f && h <= 0.72f) && (s > 0.15f) && (v > 0.15f);
    }
}
