using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hint (ipucu) butonu: basılınca hint_moves.txt dosyasından
/// mevcut level'ın hamlelerini okuyup küpe otomatik uygular.
/// Durdurulup tekrar başlatılınca KALDIĞI YERDEN devam eder.
/// </summary>
public class HintController : MonoBehaviour
{
    [Header("Hint Ayarları")]
    [Tooltip("Hamleler arası bekleme süresi (saniye)")]
    public float moveDelay = 0.4f;

    private Button hintButton;
    private Text   hintButtonText;
    private bool   isPlaying = false;

    // Kaldığı yeri hafızada tut
    private int      resumeMoveIndex = 0;
    private string[] savedMoves      = null;
    private int      savedWorldIndex = -1;
    private int      savedLevelIndex = -1;

    // ───────────────────────────────────────────────
    private void Start()
    {
        CreateHintButton();
    }

    private void OnEnable()
    {
        LevelLoader.OnLevelLoaded += ResetHint;
    }

    private void OnDisable()
    {
        LevelLoader.OnLevelLoaded -= ResetHint;
    }

    /// <summary>Level yüklenince (restart veya yeni level) hint'i sıfırla.</summary>
    private void ResetHint()
    {
        StopAllCoroutines();
        isPlaying       = false;
        resumeMoveIndex = 0;
        savedMoves      = null;
        savedWorldIndex = -1;
        savedLevelIndex = -1;
        if (hintButtonText != null)
            hintButtonText.text = "💡 İpucu";
    }

    // ─── UI ────────────────────────────────────────
    private void CreateHintButton()
    {
        // InputSystem türünü bul
        System.Type inputModuleType =
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI") ??
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");

        // Mevcut EventSystem'de StandaloneInputModule varsa kaldırıp InputSystemUIInputModule koy
        var existingES = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null && inputModuleType != null)
        {
            var standalone = existingES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                DestroyImmediate(standalone);
                if (existingES.GetComponent(inputModuleType) == null)
                    existingES.gameObject.AddComponent(inputModuleType);
            }
        }

        // EventSystem hiç yoksa yeni oluştur
        if (existingES == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (inputModuleType != null)
                es.AddComponent(inputModuleType);
            else
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Canvas bul ya da oluştur
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("HintCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Buton nesnesi
        GameObject btnObj = new GameObject("HintButton");
        btnObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(1f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-12f, -12f);
        rect.sizeDelta        = new Vector2(130f, 48f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.1f, 0.45f, 0.9f, 0.88f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(OnHintPressed);
        hintButton = btn;

        UnityEngine.UI.ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.2f, 0.65f, 1f, 0.9f);
        cb.pressedColor     = new Color(0.05f, 0.3f, 0.7f, 1f);
        btn.colors          = cb;

        // Buton yazısı
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text t = textObj.AddComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize  = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color     = Color.white;
        t.text      = "💡 İpucu";
        hintButtonText = t;
    }

    // ─── Buton Tıklama ─────────────────────────────
    private void OnHintPressed()
    {
        if (isPlaying)
        {
            // Coroutine'i FLAG ile durdur; kaldığı yeri coroutine kaydedecek
            isPlaying = false;
            hintButtonText.text = "💡 İpucu (devam)";
            return;
        }
        StartCoroutine(PlayHint());
    }

    // ─── Ana Coroutine ──────────────────────────────
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

        // Level değiştiyse baştan başla
        if (loader.worldIndex != savedWorldIndex || loader.levelIndex != savedLevelIndex)
        {
            resumeMoveIndex = 0;
            savedMoves      = null;
            savedWorldIndex = loader.worldIndex;
            savedLevelIndex = loader.levelIndex;
        }

        // Hamleleri yükle (önbelleğe al)
        if (savedMoves == null)
        {
            TextAsset hintAsset = Resources.Load<TextAsset>("hint_moves");
            if (hintAsset == null)
            {
                Debug.LogError("[HintController] hint_moves.txt bulunamadı!");
                Finish(allDone: false);
                yield break;
            }

            int lineIndex = (loader.worldIndex - 1) * 20 + (loader.levelIndex - 1);
            string[] lines = hintAsset.text.Split(
                new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);

            if (lineIndex < 0 || lineIndex >= lines.Length || string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                Debug.LogWarning($"[HintController] w{loader.worldIndex}l{loader.levelIndex} için ipucu yok.");
                Finish(allDone: false);
                yield break;
            }

            savedMoves = lines[lineIndex].Trim()
                .Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

            Debug.Log($"[HintController] w{loader.worldIndex}l{loader.levelIndex}: {savedMoves.Length} hamle yüklendi.");
        }

        TumbleController tc = FindObjectOfType<TumbleController>();
        if (tc == null)
        {
            Debug.LogError("[HintController] TumbleController bulunamadı!");
            Finish(allDone: false);
            yield break;
        }

        int remaining = savedMoves.Length - resumeMoveIndex;
        Debug.Log($"[HintController] {resumeMoveIndex}. hamleden devam ediliyor ({remaining} hamle kaldı).");
        hintButtonText.text = $"⏹ Dur ({resumeMoveIndex + 1}/{savedMoves.Length})";

        // Hamleler: index'ten itibaren devam et
        for (int i = resumeMoveIndex; i < savedMoves.Length; i++)
        {
            // Kullanıcı durdurduysa → indeksi kaydet ve çık
            if (!isPlaying)
            {
                resumeMoveIndex = i;
                yield break;
            }

            // Küp hareketi bitene kadar bekle
            while (tc.IsMoving)
            {
                if (!isPlaying) { resumeMoveIndex = i; yield break; }
                yield return null;
            }

            Vector3 dir = ParseMove(savedMoves[i]);
            if (dir != Vector3.zero)
                tc.TryMoveExternal(dir);

            // Buton metnini güncelle
            if (hintButtonText != null)
                hintButtonText.text = $"⏹ Dur ({i + 1}/{savedMoves.Length})";

            yield return new WaitForSeconds(moveDelay);
        }

        // Tüm hamleler bitti — sıfırla
        while (tc.IsMoving) yield return null;
        resumeMoveIndex = 0;
        savedMoves      = null;
        Finish(allDone: true);
    }

    private void Finish(bool allDone)
    {
        isPlaying = false;
        if (hintButtonText != null)
            hintButtonText.text = allDone ? "💡 İpucu" : "💡 İpucu (devam)";
    }

    // ─── Yön Çevirici ──────────────────────────────
    private static Vector3 ParseMove(string move)
    {
        switch (move.ToUpper())
        {
            case "F": return Vector3.forward;
            case "B": return Vector3.back;
            case "L": return Vector3.left;
            case "R": return Vector3.right;
            default:  return Vector3.zero;
        }
    }
}
