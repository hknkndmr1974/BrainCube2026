using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class MainMenuAutoSetup : EditorWindow
{
    [MenuItem("Tools/Dump and Setup Main Menu")]
    public static void ExecuteDumpAndSetup()
    {
        ExecuteDumpAndSetupInternal(false);
    }

    [MenuItem("Tools/Reset Menu Positions")]
    public static void ResetMenuPositions()
    {
        if (EditorUtility.DisplayDialog("Reset Positions", 
            "This will reset all panel positions and 3D rotations back to their default prefab settings. Are you sure?", 
            "Yes", "No"))
        {
            ExecuteDumpAndSetupInternal(true);
        }
    }

    private static void ExecuteDumpAndSetupInternal(bool resetPositions)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Setup Main Menu Error", 
                "This setup script cannot be run while Unity is in Play Mode! Please stop the game first and then run it.", 
                "OK");
            return;
        }

        // 1. Sahneyi Aç
        string scenePath = "Assets/Scenes/MainMenu.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("MainMenu scene could not be loaded at " + scenePath);
            return;
        }

        // 2. Hiyerarşiyi Dosyaya Yazdır (Analiz için)
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== MainMenu Scene Hierarchy ===");
        var rootObjects = scene.GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            DumpHierarchy(root, sb, 0);
        }
        string dumpPath = Path.Combine(Application.dataPath, "Editor/hierarchy_dump.txt");
        File.WriteAllText(dumpPath, sb.ToString(), Encoding.UTF8);
        Debug.Log("Scene hierarchy dumped to Assets/Editor/hierarchy_dump.txt");

        // 3. Kamera Stack Ayarlarını Uygula
        SetupCameras();

        // 4. Ana Menü, Ayarlar ve 15 Dünya Seçim Akışını Kur
        SetupMainMenuFlow(resetPositions);

        // Değişiklikleri kaydet
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MainMenu setup and customization completed successfully!");
    }

    private static void DumpHierarchy(GameObject go, StringBuilder sb, int depth)
    {
        string indent = new string('-', depth * 2);
        string rectDetails = "";
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rectDetails = $" Pos: {rt.anchoredPosition}, Size: {rt.sizeDelta}";
        }
        sb.AppendLine($"{indent} [{go.name}] (Active: {go.activeSelf}) {rectDetails} Components: {GetComponentsList(go)}");
        for (int i = 0; i < go.transform.childCount; i++)
        {
            DumpHierarchy(go.transform.GetChild(i).gameObject, sb, depth + 1);
        }
    }

    private static string GetComponentsList(GameObject go)
    {
        var components = go.GetComponents<Component>();
        List<string> names = new List<string>();
        foreach (var c in components)
        {
            if (c != null) names.Add(c.GetType().Name);
        }
        return string.Join(", ", names);
    }

    private static void SetupCameras()
    {
        GameObject guiCameraGo = FindGameObjectInChildrenOrScene("GUI Camera");
        GameObject mainCameraGo = FindGameObjectInChildrenOrScene("Main Camera");

        if (guiCameraGo == null || mainCameraGo == null)
        {
            Debug.LogWarning($"Cameras not found. GUI: {guiCameraGo != null}, Main: {mainCameraGo != null}");
            return;
        }

        Camera guiCamera = guiCameraGo.GetComponent<Camera>();
        Camera mainCamera = mainCameraGo.GetComponent<Camera>();

        var guiCameraData = guiCameraGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (guiCameraData == null)
        {
            guiCameraData = guiCameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        var mainCameraData = mainCameraGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (mainCameraData == null)
        {
            mainCameraData = mainCameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        // GUI Camera -> Overlay
        guiCameraData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
        
        // Main Camera -> Base
        mainCameraData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
        
        // Stack listesini temizle ve GUI Camera'yı ekle
        mainCameraData.cameraStack.Clear();
        mainCameraData.cameraStack.Add(guiCamera);

        Debug.Log("URP Camera Stack setup done automatically!");
    }

    private static void SetupMainMenuFlow(bool resetPositions)
    {
        GameObject canvasGo = FindGameObjectInChildrenOrScene("Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("Canvas not found in scene!");
            return;
        }

        // MainMenuController ekle veya bul
        MainMenuController controller = canvasGo.GetComponent<MainMenuController>();
        if (controller == null)
        {
            controller = canvasGo.AddComponent<MainMenuController>();
        }

        // MenuManager altındaki PanelManager'ı bul
        GameObject menuManagerGo = FindGameObjectInChildrenOrScene("MenuManager");
        if (menuManagerGo == null)
        {
            Debug.LogError("MenuManager not found!");
            return;
        }
        PanelManager mainPanelManager = menuManagerGo.GetComponent<PanelManager>();

        // Panelleri bul
        Transform mainMenuTrans = canvasGo.transform.Find("MainMenu");
        Transform settingsTrans = canvasGo.transform.Find("Settings");

        if (mainMenuTrans == null || settingsTrans == null)
        {
            Debug.LogError("MainMenu or Settings panel not found under Canvas!");
            return;
        }

        Animator mainMenuAnim = mainMenuTrans.GetComponent<Animator>();
        Animator settingsAnim = settingsTrans.GetComponent<Animator>();

        Transform windowTrans = mainMenuTrans.Find("Window");
        if (windowTrans == null)
        {
            Debug.LogError("MainMenu/Window not found!");
            return;
        }

        // Başlığı BRAIN CUBE yap
        Transform titleTrans = windowTrans.Find("SF Title/TitleLabel");
        if (titleTrans != null)
        {
            Text titleText = titleTrans.GetComponent<Text>();
            if (titleText != null)
            {
                titleText.text = "BRAIN CUBE";
                EditorUtility.SetDirty(titleText);
            }
        }

        // Butonları bul ve konumlandır
        Transform continueBtnTrans = windowTrans.Find("Continue");
        Transform playBtnTrans = windowTrans.Find("Newgame");
        Transform settingsBtnTrans = windowTrans.Find("Settings");
        Transform quitBtnTrans = windowTrans.Find("Quit");

        if (continueBtnTrans == null || playBtnTrans == null || settingsBtnTrans == null || quitBtnTrans == null)
        {
            Debug.LogError("One or more Main Menu buttons not found!");
            return;
        }

        // Buton isimlerini ve metinlerini güncelle
        SetButtonText(continueBtnTrans, "CONTINUE");
        SetButtonText(playBtnTrans, "PLAY");
        SetButtonText(settingsBtnTrans, "SETTINGS");
        SetButtonText(quitBtnTrans, "QUIT");

        // Pozisyonları düzenle (Simetrik 60px aralıklarla)
        SetRectPosition(continueBtnTrans, new Vector2(0, 120));
        SetRectPosition(playBtnTrans, new Vector2(0, 60));
        SetRectPosition(settingsBtnTrans, new Vector2(0, -60));
        SetRectPosition(quitBtnTrans, new Vector2(0, -120));

        // LEVEL SELECT butonunu oluştur (Play butonunu kopyalayarak)
        Transform levelSelectBtnTrans = windowTrans.Find("LevelSelectBtn");
        if (levelSelectBtnTrans == null)
        {
            GameObject levelSelectBtnGo = Instantiate(playBtnTrans.gameObject, windowTrans);
            levelSelectBtnGo.name = "LevelSelectBtn";
            levelSelectBtnTrans = levelSelectBtnGo.transform;
            levelSelectBtnTrans.SetSiblingIndex(playBtnTrans.GetSiblingIndex() + 1);
        }
        SetButtonText(levelSelectBtnTrans, "LEVEL SELECT");
        SetRectPosition(levelSelectBtnTrans, new Vector2(0, 0));

        // --- LEVEL SELECT PANELİ OLUŞTURMA (SETTINGS'DEN TAMPON ALARAK) ---
        Transform levelSelectPanelTrans = canvasGo.transform.Find("LevelSelect");
        
        SavedTransform savedWorldWindow = new SavedTransform();
        SavedTransform savedLevelPanels = new SavedTransform();

        if (levelSelectPanelTrans != null)
        {
            if (!resetPositions)
            {
                // Eski konumları hafızaya al
                Transform oldWorldWindow = levelSelectPanelTrans.Find("WorldWindow");
                if (oldWorldWindow != null)
                {
                    savedWorldWindow = SavedTransform.Save(oldWorldWindow.GetComponent<RectTransform>());
                }

                Transform oldLevelPanels = levelSelectPanelTrans.Find("LevelPanelsContainer");
                if (oldLevelPanels != null)
                {
                    savedLevelPanels = SavedTransform.Save(oldLevelPanels.GetComponent<RectTransform>());
                }
            }

            DestroyImmediate(levelSelectPanelTrans.gameObject);
        }

        // Settings panelini kopyalayarak LevelSelect yapıyoruz (tüm 2 panelli akış yapısı hazır geliyor)
        GameObject levelSelectPanelGo = Instantiate(settingsTrans.gameObject, canvasGo.transform);
        levelSelectPanelGo.name = "LevelSelect";
        levelSelectPanelGo.SetActive(false); // Başlangıçta kapalı
        levelSelectPanelTrans = levelSelectPanelGo.transform;
        levelSelectPanelTrans.SetSiblingIndex(settingsTrans.GetSiblingIndex());

        Animator levelSelectAnim = levelSelectPanelTrans.GetComponent<Animator>();

        // LevelSelect altındaki PanelManager'ı bulalım (bu, dünyalara tıklandığında sağdaki bölümleri değiştirecek)
        PanelManager levelSelectPanelManager = levelSelectPanelTrans.GetComponentInChildren<PanelManager>();
        if (levelSelectPanelManager == null)
        {
            Debug.LogError("PanelManager not found under LevelSelect copy!");
            return;
        }

        // Sol pencereyi WorldWindow olarak adlandır
        Transform worldWindowTrans = levelSelectPanelTrans.Find("SettingsWindow");
        if (worldWindowTrans != null)
        {
            worldWindowTrans.name = "WorldWindow";
            
            // Hafızaya aldığımız konumu geri yükle
            if (savedWorldWindow.hasData)
            {
                savedWorldWindow.Restore(worldWindowTrans.GetComponent<RectTransform>());
            }
            
            // Başlığı değiştir
            Transform worldTitleTrans = worldWindowTrans.Find("Title/TitleLabel");
            if (worldTitleTrans != null)
            {
                Text worldTitleText = worldTitleTrans.GetComponent<Text>();
                if (worldTitleText != null) worldTitleText.text = "WORLDS";
            }

            // Eski ayarlar butonlarını (Gameplay, Video, Audio) temizle, Back butonunu tut
            Transform backBtnTrans = worldWindowTrans.Find("Back");
            if (backBtnTrans != null)
            {
                backBtnTrans.name = "BackBtn";
                SetButtonText(backBtnTrans, "BACK");
                SetRectPosition(backBtnTrans, new Vector2(0, -210)); // Sabit alta yerleştir

                Button backBtn = backBtnTrans.GetComponent<Button>();
                if (backBtn != null)
                {
                    ClearButtonEvents(backBtn);
                    // Ana menüye dön
                    UnityEventTools.AddObjectPersistentListener(backBtn.onClick, mainPanelManager.OpenPanel, mainMenuAnim);
                }
            }

            foreach (string bName in new[] { "Gameplay", "Video", "Audio" })
            {
                Transform oldBtn = worldWindowTrans.Find(bName);
                if (oldBtn != null) DestroyImmediate(oldBtn.gameObject);
            }

            // Dünya listesi için ScrollRect (Kaydırma alanı) oluştur
            GameObject scrollGo = new GameObject("WorldScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(worldWindowTrans, false);
            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(55, 90);   // Sol margin: 55, Alt margin: 90 (Daraltıldı)
            scrollRt.offsetMax = new Vector2(-55, -135); // Sağ margin: 55, Üst margin: 135 (Daraltıldı)

            ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.viewport = scrollRt; // Viewport referansı bağlandı
            scrollRect.scrollSensitivity = 20f; // Kaydırma hassasiyeti artırıldı (fare tekerleği için)

            // Maskeleme
            scrollGo.AddComponent<RectMask2D>();

            // İçerik (Content) taşıyıcısı
            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0, 0);

            scrollRect.content = contentRt;

            // Dikey Düzenleme
            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(5, 5, 10, 10);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // --- SAĞ PANEL (LEVELS CONTAINER) KURULUMU ---
            Transform levelPanelsContainerTrans = levelSelectPanelTrans.Find("SettingsPanels");
            if (levelPanelsContainerTrans != null)
            {
                levelPanelsContainerTrans.name = "LevelPanelsContainer";
            }
            else
            {
                levelPanelsContainerTrans = levelSelectPanelTrans.Find("LevelPanelsContainer");
            }

            if (levelPanelsContainerTrans != null && savedLevelPanels.hasData)
            {
                savedLevelPanels.Restore(levelPanelsContainerTrans.GetComponent<RectTransform>());
            }



            if (levelPanelsContainerTrans == null)
            {
                Debug.LogError("LevelPanelsContainer not found!");
                return;
            }

            // Klonlama şablonu olarak GameplayWindow'u alalım
            Transform templateTrans = levelPanelsContainerTrans.Find("GamePlayWindow");
            if (templateTrans == null) templateTrans = levelPanelsContainerTrans.Find("VideoWindow");
            if (templateTrans == null)
            {
                Debug.LogError("Sub-window template not found in SettingsPanels!");
                return;
            }
            GameObject templateGo = templateTrans.gameObject;

            // 15 Dünya ve her dünya için 20 Seviye oluşturulması
            GameObject worldBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity UI Samples/Prefabs/SF Button.prefab");
            GameObject levelGridBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity UI Samples/Prefabs/SF Grid Button.prefab");

            if (worldBtnPrefab == null || levelGridBtnPrefab == null)
            {
                Debug.LogError("Prefabs missing under Assets/Unity UI Samples/Prefabs/");
                return;
            }

            List<Animator> levelPanelAnimators = new List<Animator>();

            for (int w = 1; w <= 15; w++)
            {
                GameObject wBtnGo = Instantiate(worldBtnPrefab, contentGo.transform);
                wBtnGo.name = $"WorldBtn_{w}";
                SetButtonText(wBtnGo.transform, $"WORLD {w}");
                RectTransform wBtnRt = wBtnGo.GetComponent<RectTransform>();
                wBtnRt.sizeDelta = new Vector2(0, 60);
                LayoutElement le = wBtnGo.GetComponent<LayoutElement>();
                if (le == null) le = wBtnGo.AddComponent<LayoutElement>();
                le.preferredHeight = 60f;

                GameObject wLevelsGo = Instantiate(templateGo, levelPanelsContainerTrans);
                wLevelsGo.name = $"World{w}_Levels";
                wLevelsGo.SetActive(false);
                Animator wLevelsAnim = wLevelsGo.GetComponent<Animator>();
                levelPanelAnimators.Add(wLevelsAnim);

                Transform subTitleTrans = wLevelsGo.transform.Find("Panel/Title/TitleLabel");
                if (subTitleTrans != null)
                {
                    Text subTitleText = subTitleTrans.GetComponent<Text>();
                    if (subTitleText != null) subTitleText.text = $"WORLD {w} LEVELS";
                }

                Transform panelTrans = wLevelsGo.transform.Find("Panel");
                if (panelTrans != null)
                {
                    Transform oldSettings = panelTrans.Find("Settings");
                    if (oldSettings != null) DestroyImmediate(oldSettings.gameObject);
                    Transform oldClose = panelTrans.Find("Close");
                    if (oldClose != null) DestroyImmediate(oldClose.gameObject);

                    GameObject gridGo = new GameObject("LevelGrid", typeof(RectTransform));
                    gridGo.transform.SetParent(panelTrans, false);
                    RectTransform gridRt = gridGo.GetComponent<RectTransform>();
                    gridRt.anchorMin = Vector2.zero;
                    gridRt.anchorMax = Vector2.one;
                    gridRt.offsetMin = new Vector2(20, 12);
                    gridRt.offsetMax = new Vector2(-20, -148);
                    GridLayoutGroup grid = gridGo.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(50, 50);
                    grid.spacing = new Vector2(12, 12);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 5;
                    grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                    grid.childAlignment = TextAnchor.UpperCenter;

                    for (int l = 1; l <= 20; l++)
                    {
                        GameObject lBtnGo = Instantiate(levelGridBtnPrefab, gridGo.transform);
                        lBtnGo.name = $"LevelButton_{l}";
                        Text btnText = lBtnGo.GetComponentInChildren<Text>();
                        if (btnText != null)
                        {
                            btnText.text = l.ToString();
                            btnText.fontSize = 20;
                            btnText.alignment = TextAnchor.MiddleCenter;
                            btnText.horizontalOverflow = HorizontalWrapMode.Overflow;
                            btnText.verticalOverflow = VerticalWrapMode.Overflow;
                            EditorUtility.SetDirty(btnText);
                        }
                        LevelSelectButton btnScript = lBtnGo.AddComponent<LevelSelectButton>();
                        btnScript.worldIndex = w;
                        btnScript.levelIndex = l;
                        Button btn = lBtnGo.GetComponent<Button>();
                        if (btn != null)
                        {
                            ClearButtonEvents(btn);
                            UnityEventTools.AddVoidPersistentListener(btn.onClick, btnScript.OnClick);
                        }
                    }
                }

                Button wBtn = wBtnGo.GetComponent<Button>();
                if (wBtn != null)
                {
                    ClearButtonEvents(wBtn);
                    UnityEventTools.AddObjectPersistentListener(wBtn.onClick, levelSelectPanelManager.OpenPanel, wLevelsAnim);
                }
            }

            if (levelPanelAnimators.Count > 0)
            {
                levelSelectPanelManager.initiallyOpen = levelPanelAnimators[0];
                EditorUtility.SetDirty(levelSelectPanelManager);
            }

            // Şablon pencereleri artık silebiliriz
            foreach (string tName in new[] { "GamePlayWindow", "VideoWindow", "AudioWindow" })
            {
                Transform oldT = levelPanelsContainerTrans.Find(tName);
                if (oldT != null) DestroyImmediate(oldT.gameObject);
            }
        }

        // --- BUTTON EVENTLERİNİ BAĞLAMA (MAIN MENU) ---
        Button playBtn = playBtnTrans.GetComponent<Button>();
        if (playBtn != null)
        {
            ClearButtonEvents(playBtn);
            UnityEventTools.AddVoidPersistentListener(playBtn.onClick, controller.PlayNewGame);
        }

        Button continueBtn = continueBtnTrans.GetComponent<Button>();
        if (continueBtn != null)
        {
            ClearButtonEvents(continueBtn);
            UnityEventTools.AddVoidPersistentListener(continueBtn.onClick, controller.ContinueGame);
        }

        Button levelSelectBtn = levelSelectBtnTrans.GetComponent<Button>();
        if (levelSelectBtn != null)
        {
            ClearButtonEvents(levelSelectBtn);
            // Ana menüdeki LEVEL SELECT butonuna basınca LevelSelect panelini aç
            UnityEventTools.AddObjectPersistentListener(levelSelectBtn.onClick, mainPanelManager.OpenPanel, levelSelectAnim);
        }

        Button settingsBtn = settingsBtnTrans.GetComponent<Button>();
        if (settingsBtn != null)
        {
            ClearButtonEvents(settingsBtn);
            UnityEventTools.AddObjectPersistentListener(settingsBtn.onClick, mainPanelManager.OpenPanel, settingsAnim);
        }

        Button quitBtn = quitBtnTrans.GetComponent<Button>();
        if (quitBtn != null)
        {
            ClearButtonEvents(quitBtn);
            UnityEventTools.AddVoidPersistentListener(quitBtn.onClick, controller.QuitGame);
        }
    }

    private static void SetButtonText(Transform buttonTrans, string text)
    {
        Transform labelTrans = buttonTrans.Find("Background/Label");
        if (labelTrans == null) labelTrans = buttonTrans.Find("Label");

        if (labelTrans != null)
        {
            Text txt = labelTrans.GetComponent<Text>();
            if (txt != null)
            {
                txt.text = text;
                EditorUtility.SetDirty(txt);
            }
        }
    }

    private static void SetRectPosition(Transform trans, Vector2 pos)
    {
        RectTransform rt = trans.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = pos;
            EditorUtility.SetDirty(rt);
        }
    }

    private static void ClearButtonEvents(Button btn)
    {
        while (btn.onClick.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        }
    }

    private static GameObject FindGameObjectInChildrenOrScene(string name)
    {
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allObjects)
        {
            if (go.name == name && go.scene.isLoaded)
            {
                return go;
            }
        }
        return null;
    }

    private struct SavedTransform
    {
        public bool hasData;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Quaternion localRotation;
        public Vector3 localScale;

        public static SavedTransform Save(RectTransform rt)
        {
            if (rt == null) return new SavedTransform();
            SavedTransform st = new SavedTransform();
            st.hasData = true;
            st.anchorMin = rt.anchorMin;
            st.anchorMax = rt.anchorMax;
            st.pivot = rt.pivot;
            st.anchoredPosition = rt.anchoredPosition;
            st.sizeDelta = rt.sizeDelta;
            st.localRotation = rt.localRotation;
            st.localScale = rt.localScale;
            return st;
        }

        public void Restore(RectTransform rt)
        {
            if (!hasData || rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.localRotation = localRotation;
            rt.localScale = localScale;
            EditorUtility.SetDirty(rt);
        }
    }
}
