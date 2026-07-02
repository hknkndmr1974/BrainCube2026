using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hint (ipucu) butonu, Önceki/Sonraki Bölüm geçişleri ve Sol üst köşedeki Hızlı Bölüm Seçme Panelini yönetir.
/// </summary>
public class HintController : MonoBehaviour
{
    [Header("Hint Ayarları")]
    [Tooltip("Hamleler arası bekleme süresi (saniye)")]
    public float moveDelay = 0.4f;

    private Button hintButton;
    private Text hintButtonText;
    private bool isPlaying = false;

    // Kaldığı yeri hafızada tut
    private int resumeMoveIndex = 0;
    private string[] savedMoves = null;
    private int savedWorldIndex = -1;
    private int savedLevelIndex = -1;

    // Input alanları (Sol üst köşe)
    private InputField worldInputField;
    private InputField levelInputField;

    private GameObject loadingOverlay; // Simülasyon sirasinda görünecek loading paneli

    private void Start()
    {
        CreateFullTestUI();
    }

    private void OnEnable()
    {
        LevelLoader.OnLevelLoaded += ResetHint;
    }

    private void OnDisable()
    {
        LevelLoader.OnLevelLoaded -= ResetHint;
    }

    /// <summary>Level yüklenince ipucunu sıfırla.</summary>
    private void ResetHint()
    {
        StopAllCoroutines();
        isPlaying = false;
        resumeMoveIndex = 0;
        savedMoves = null;
        savedWorldIndex = -1;
        savedLevelIndex = -1;
        if (hintButtonText != null)
            hintButtonText.text = "💡 İpucu";

        // Input alanlarındaki yazıları güncelle
        if (worldInputField != null && levelInputField != null)
        {
            var loader = LevelLoader.Instance;
            if (loader != null)
            {
                worldInputField.text = loader.worldIndex.ToString();
                levelInputField.text = loader.levelIndex.ToString();
            }
        }
    }

    // ─── UI OLUŞTURMA ────────────────────────────────
    private void CreateFullTestUI()
    {
        // EventSystem check
        System.Type inputModuleType =
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");

        var existingES = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null && inputModuleType != null)
        {
            var standalone = existingES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                standalone.enabled = false; // Hata vermesini önlemek için önce devre dışı bırakıyoruz
                DestroyImmediate(standalone);
                if (existingES.GetComponent(inputModuleType) == null)
                    existingES.gameObject.AddComponent(inputModuleType);
            }
        }
        if (existingES == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (inputModuleType != null) es.AddComponent(inputModuleType);
            else es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Canvas check
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("TestUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. İpucu Butonu (Sağ Üste Yakın - Güvenli Alan)
        CreateHintButton(canvas.transform, defaultFont);

        // 2. Bölüm Değiştirme Paneli (Sağ Alt Köşe)
        CreatePrevNextPanel(canvas.transform, defaultFont);

        // 3. Sol Üst Bölüm Git Paneli
        CreateLevelSelectPanel(canvas.transform, defaultFont);

        // 4. Loading Overlay (Simülasyon karartma ekranı)
        CreateLoadingOverlay(canvas.transform, defaultFont);
    }

    private void Update()
    {
        if (loadingOverlay != null)
        {
            // TumbleController.isSimulating durumuna göre loading panelini aktif/deaktif et
            loadingOverlay.SetActive(TumbleController.isSimulating);
        }
    }

    private void CreateLoadingOverlay(Transform parent, Font font)
    {
        loadingOverlay = new GameObject("LoadingOverlay");
        loadingOverlay.transform.SetParent(parent, false);

        RectTransform rect = loadingOverlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        // Arka Plan: Yarı saydam çok şık premium koyu bir renk (glassmorphism/dark mode esintisi)
        Image img = loadingOverlay.AddComponent<Image>();
        img.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

        // Ana Başlık
        GameObject textObj = new GameObject("Title");
        textObj.transform.SetParent(loadingOverlay.transform, false);
        RectTransform titleRect = textObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 25f);
        titleRect.sizeDelta = new Vector2(0f, 60f);

        Text t = textObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 24;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.2f, 0.65f, 1f, 1f); // Neon Mavi/Siyan renk tonu
        t.text = "BÖLÜM ANALİZ EDİLİYOR...";

        // Alt Bilgi Yazısı
        GameObject subTextObj = new GameObject("Sub");
        subTextObj.transform.SetParent(loadingOverlay.transform, false);
        RectTransform subRect = subTextObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.5f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.anchoredPosition = new Vector2(0f, -25f);
        subRect.sizeDelta = new Vector2(0f, 40f);

        Text subT = subTextObj.AddComponent<Text>();
        subT.font = font;
        subT.fontSize = 14;
        subT.fontStyle = FontStyle.Normal;
        subT.alignment = TextAnchor.MiddleCenter;
        subT.color = new Color(0.7f, 0.75f, 0.8f, 1f);
        subT.text = "Yol haritasi ve ipuçlari çikariliyor, lütfen bekleyiniz...";

        loadingOverlay.SetActive(false);
    }

    private void CreateHintButton(Transform parent, Font font)
    {
        GameObject btnObj = new GameObject("HintButton");
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -80f); // Daha içte ve aşağıda
        rect.sizeDelta = new Vector2(140f, 48f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.12f, 0.5f, 0.9f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(OnHintPressed);
        hintButton = btn;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.2f, 0.65f, 1f, 1f);
        cb.pressedColor = new Color(0.05f, 0.3f, 0.7f, 1f);
        btn.colors = cb;

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text t = textObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = "💡 İpucu";
        hintButtonText = t;
    }

    private void CreatePrevNextPanel(Transform parent, Font font)
    {
        // Container Panel (Ekranın alt-ortasına konumlandırıldı)
        GameObject panelObj = new GameObject("PrevNextPanel");
        panelObj.transform.SetParent(parent, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 40f); // Alt kenardan 40 piksel yukarıda (toleranslı)
        panelRect.sizeDelta = new Vector2(350f, 70f); // Panel boyutu genişletildi

        // Buton boyutları: 160 x 60 piksel olarak büyütüldü, aradaki boşluk 30 piksel yapıldı
        // Önceki Bölüm Butonu (Sol taraf)
        CreateButton(panelObj.transform, "PrevBtn", "◀ Önceki", new Vector2(-175f, 0f), new Vector2(160f, 60f), font, () => LoadOffsetLevel(-1));

        // Sonraki Bölüm Butonu (Sağ taraf)
        CreateButton(panelObj.transform, "NextBtn", "Sonraki ▶", new Vector2(15f, 0f), new Vector2(160f, 60f), font, () => LoadOffsetLevel(1));
    }

    private void CreateLevelSelectPanel(Transform parent, Font font)
    {
        // Container Panel (Sol üstten 40 piksel toleranslı içeriye)
        GameObject panelObj = new GameObject("LevelSelectPanel");
        panelObj.transform.SetParent(parent, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(40f, -40f); // 40 piksel sağa, 40 piksel aşağıya
        panelRect.sizeDelta = new Vector2(340f, 70f); // Genişletildi

        // Arka plan rengi (yarı saydam siyah)
        Image bg = panelObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        // World Input
        CreateTextLabel(panelObj.transform, "WLabel", "W:", new Vector2(10f, -22f), font, 20);
        worldInputField = CreateInputField(panelObj.transform, "WInput", new Vector2(40f, -10f), new Vector2(55f, 50f), font, "1");

        // Level Input
        CreateTextLabel(panelObj.transform, "LLabel", "L:", new Vector2(110f, -22f), font, 20);
        levelInputField = CreateInputField(panelObj.transform, "LInput", new Vector2(140f, -10f), new Vector2(55f, 50f), font, "1");

        // Git Butonu
        CreateButton(panelObj.transform, "GoBtn", "GİT", new Vector2(215f, -10f), new Vector2(110f, 50f), font, OnGoButtonClicked);

        // Başlangıç değerlerini eşle
        var loader = LevelLoader.Instance;
        if (loader != null)
        {
            worldInputField.text = loader.worldIndex.ToString();
            levelInputField.text = loader.levelIndex.ToString();
        }
    }

    // ─── YARDIMCI METOTLAR (Dinamik UI Elementleri) ────
    private void CreateTextLabel(Transform parent, string name, string text, Vector2 pos, Font font, int fontSize)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(30f, 30f); // Etiket boyutunu genişlettik

        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
    }

    private InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size, Font font, string startVal)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        InputField input = obj.AddComponent<InputField>();

        // Text alanı
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-6f, -6f);

        Text t = textObj.AddComponent<Text>();
        t.font = font;
        // Giriş kutusu boyutuna göre fontu 20'ye yükselt
        t.fontSize = 20;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.supportRichText = false;

        input.textComponent = t;
        input.text = startVal;
        input.characterValidation = InputField.CharacterValidation.Integer;

        return input;
    }

    private void CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Font font, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.12f, 0.5f, 0.9f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.2f, 0.65f, 1f, 1f);
        cb.pressedColor = new Color(0.05f, 0.3f, 0.7f, 1f);
        btn.colors = cb;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text t = textObj.AddComponent<Text>();
        t.font = font;
        // Buton yüksekliğine göre yazı boyutunu ölçeklendir
        t.fontSize = (size.y > 50f) ? 20 : 14;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;
    }

    // ─── BUTON EYLEMLERİ ─────────────────────────────
    private void OnHintPressed()
    {
        if (isPlaying)
        {
            isPlaying = false;
            hintButtonText.text = "💡 İpucu (devam)";
            return;
        }
        StartCoroutine(PlayHint());
    }

    private void OnGoButtonClicked()
    {
        if (worldInputField == null || levelInputField == null) return;

        int w, l;
        if (int.TryParse(worldInputField.text, out w) && int.TryParse(levelInputField.text, out l))
        {
            var loader = LevelLoader.Instance;
            if (loader != null)
            {
                // Değerleri sınırla
                w = Mathf.Clamp(w, 1, 20);
                l = Mathf.Clamp(l, 1, 20);
                loader.LoadLevel(w, l);
            }
        }
    }

    private void LoadOffsetLevel(int offset)
    {
        var loader = LevelLoader.Instance;
        if (loader == null) return;

        int targetWorld = loader.worldIndex;
        int targetLevel = loader.levelIndex + offset;

        if (targetLevel > 20)
        {
            targetLevel = 1;
            targetWorld = Mathf.Clamp(targetWorld + 1, 1, 20);
        }
        else if (targetLevel < 1)
        {
            targetLevel = 20;
            targetWorld = Mathf.Clamp(targetWorld - 1, 1, 20);
        }

        loader.LoadLevel(targetWorld, targetLevel);
    }

    private IEnumerator PlayHint()
    {
        isPlaying = true;

        LevelLoader loader = LevelLoader.Instance;
        if (loader == null)
        {
            Debug.LogError("[HintController] LevelLoader bulunamadı!");
            Finish(allDone: false);
            yield break;
        }

        if (loader.worldIndex != savedWorldIndex || loader.levelIndex != savedLevelIndex)
        {
            resumeMoveIndex = 0;
            savedMoves = null;
            savedWorldIndex = loader.worldIndex;
            savedLevelIndex = loader.levelIndex;
        }

        if (savedMoves == null)
        {
            if (loader.CurrentLevelData == null || string.IsNullOrWhiteSpace(loader.CurrentLevelData.hintMoves))
            {
                Debug.LogWarning($"[HintController] w{loader.worldIndex}l{loader.levelIndex} için ipucu yok.");
                Finish(allDone: false);
                yield break;
            }

            savedMoves = loader.CurrentLevelData.hintMoves.Split(
                new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        TumbleController tc = FindObjectOfType<TumbleController>();
        if (tc == null)
        {
            Debug.LogError("[HintController] TumbleController bulunamadı!");
            Finish(allDone: false);
            yield break;
        }

        // Eğer ipucu ilk defa (devam edilmeden) başlatılıyorsa en son çakıştığı doğru konuma al
        if (resumeMoveIndex == 0)
        {
            bool waitingForRewind = true;
            StartCoroutine(tc.MatchAndRewind((resultIndex) => {
                resumeMoveIndex = resultIndex;
                waitingForRewind = false;
            }));

            while (waitingForRewind)
            {
                yield return null;
            }
        }

        hintButtonText.text = $"⏹ Dur ({resumeMoveIndex + 1}/{savedMoves.Length})";

        for (int i = resumeMoveIndex; i < savedMoves.Length; i++)
        {
            if (!isPlaying)
            {
                resumeMoveIndex = i;
                yield break;
            }

            while (tc.IsMoving)
            {
                if (!isPlaying) { resumeMoveIndex = i; yield break; }
                yield return null;
            }

            Vector3 dir = ParseMove(savedMoves[i]);
            if (dir != Vector3.zero)
                tc.TryMoveExternal(dir);

            if (hintButtonText != null)
                hintButtonText.text = $"⏹ Dur ({i + 1}/{savedMoves.Length})";

            yield return new WaitForSeconds(moveDelay);
        }

        while (tc.IsMoving) yield return null;
        resumeMoveIndex = 0;
        savedMoves = null;
        Finish(allDone: true);
    }

    private void Finish(bool allDone)
    {
        isPlaying = false;
        if (hintButtonText != null)
            hintButtonText.text = allDone ? "💡 İpucu" : "💡 İpucu (devam)";
    }

    private static Vector3 ParseMove(string move)
    {
        switch (move.ToUpper())
        {
            case "F": return Vector3.forward;
            case "B": return Vector3.back;
            case "L": return Vector3.left;
            case "R": return Vector3.right;
            default: return Vector3.zero;
        }
    }

    public void ResetHintState()
    {
        resumeMoveIndex = 0;
        isPlaying = false;
        if (hintButtonText != null)
        {
            hintButtonText.text = "💡 İpucu";
        }
    }
}
