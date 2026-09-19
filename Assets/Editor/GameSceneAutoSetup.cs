using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameSceneAutoSetup : EditorWindow
{
    [MenuItem("Tools/Setup Game Scene UI")]
    public static void ExecuteSetup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Setup Error", 
                "This script cannot be run in Play Mode! Please stop the game first.", 
                "OK");
            return;
        }

        string scenePath = "Assets/BrainCube.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("BrainCube scene could not be loaded at " + scenePath);
            return;
        }

        // 1. Canvas Bul veya Oluştur
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // EventSystem Bul veya Oluştur (UI etkileşimi için gerekli)
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem", typeof(EventSystem));
#if UNITY_2022_1_OR_NEWER
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }

        // GameUIController Ekle veya Bul
        GameUIController controller = canvasGo.GetComponent<GameUIController>();
        if (controller == null)
        {
            controller = canvasGo.AddComponent<GameUIController>();
        }

        // Hint butonunu bulalım
        GameObject hintGo = null;
        var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allGameObjects)
        {
            if (go.scene.isLoaded && go.name.ToLower().Contains("hint") && go.GetComponent<Button>() != null)
            {
                hintGo = go;
                break;
            }
        }

        Vector2 buttonPos = new Vector2(-20, -20);
        Vector2 buttonSize = new Vector2(180, 50);
        Vector2 anchorMin = new Vector2(1, 1);
        Vector2 anchorMax = new Vector2(1, 1);
        Vector2 pivot = new Vector2(1, 1);

        if (hintGo != null)
        {
            RectTransform hintRt = hintGo.GetComponent<RectTransform>();
            if (hintRt != null)
            {
                anchorMin = hintRt.anchorMin;
                anchorMax = hintRt.anchorMax;
                pivot = hintRt.pivot;
                // Hint butonunun 15px altına yerleştir
                buttonPos = new Vector2(hintRt.anchoredPosition.x, hintRt.anchoredPosition.y - hintRt.sizeDelta.y - 15f);
                buttonSize = new Vector2(hintRt.sizeDelta.x, 50f); // Aynı genişlik
                Debug.Log($"Hint button found: '{hintGo.name}'. Placing MainMenuButton below it at: {buttonPos}");
            }
        }
        else
        {
            Debug.LogWarning("Hint button not found in scene. Falling back to default top-right corner placement.");
        }

        // 2. Main Menu Butonunu Bul veya Oluştur
        Transform btnTrans = canvasGo.transform.Find("MainMenuButton");
        if (btnTrans != null)
        {
            DestroyImmediate(btnTrans.gameObject);
        }

        GameObject btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity UI Samples/Prefabs/SF Button.prefab");
        GameObject btnGo;
        if (btnPrefab != null)
        {
            btnGo = Instantiate(btnPrefab, canvasGo.transform);
        }
        else
        {
            // Prefab yoksa standart UI butonu oluştur (Fallback)
            btnGo = new GameObject("MainMenuButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            Text txt = textGo.GetComponent<Text>();
            txt.text = "MAIN MENU";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
        }

        btnGo.name = "MainMenuButton";
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = buttonPos;
        rt.sizeDelta = buttonSize;

        // Buton Metnini Ayarla
        Transform labelTrans = btnGo.transform.Find("Background/Label");
        if (labelTrans == null) labelTrans = btnGo.transform.Find("Label");
        if (labelTrans != null)
        {
            Text txt = labelTrans.GetComponent<Text>();
            if (txt != null)
            {
                txt.text = "MAIN MENU";
                txt.fontSize = 20;
                txt.alignment = TextAnchor.MiddleCenter;
                EditorUtility.SetDirty(txt);
            }
        }

        // Event listener bağla
        Button btn = btnGo.GetComponent<Button>();
        if (btn != null)
        {
            while (btn.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(btn.onClick, 0);
            }
            UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.ReturnToMainMenu);
        }

        // Kaydet
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Game Scene Main Menu Button setup completed successfully!");
    }
}
