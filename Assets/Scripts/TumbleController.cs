using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FlexibleGlassDestructor;

[System.Serializable]
public struct BridgeState
{
    public int channel;
    public bool isActive;
}

[System.Serializable]
public struct BlockState
{
    public Vector3 position;
    public Quaternion rotation;
    public bool isSplit;
    public Vector3 p1Position;
    public Quaternion p1Rotation;
    public Vector3 p2Position;
    public Quaternion p2Rotation;
    public int activeSplitPlayer;
    public System.Collections.Generic.List<BridgeState> bridgeStates;
}

public class TumbleController : MonoBehaviour
{
    public enum WinAnimationType
    {
        RocketUp,  // Tek parça dönerek yukarı fırlama
        SplitWarp, // İki parçaya ayrılıp ters yönlerde dönerek yukarı fırlama
        Atomize    // Bilim kurgu ışınlanması gibi atomlarına ayrılıp yukarı uçma
    }

    private class AtomInfo
    {
        public GameObject go;
        public Vector3 startPos;
        public float startThreshold;
        public float speed;
    }

    [Header("Victory Settings")]
    public WinAnimationType winAnimation = WinAnimationType.RocketUp;

    public float tumblingDuration = 0.3f;
    private float baseTumblingDuration = -1f;
    
    [Header("Sounds")]
    public AudioClip tumbleSound;
    public AudioClip gameOverSound;
    public AudioClip winSound;

    private Rigidbody rb;
    private bool isTumbling = false;
    private bool playerFell = false;
    private Vector3 lastMoveDir = Vector3.forward;
    private Vector3 pendingConveyorDir = Vector3.zero; // Konveyör tile'dan gelen bekleyen yön
    private bool isTeleporting = false; // Teleport animasyonunun aktiflik durumu

    [Header("Mobile Swipe Settings")]
    public float minSwipeDistance = 50f;
    private Vector2 swipeStartPos;
    private bool isSwiping = false;

    [Header("Split Settings")]
    [HideInInspector] public bool isSplit = false;
    [HideInInspector] public GameObject player1;
    [HideInInspector] public GameObject player2;
    [HideInInspector] public int activeSplitPlayer = 1;
    // Teleport animasyonu ayarları
    public float teleportShrinkStep = 0.05f;      // Y ekseninde küçülme adımı
    public float teleportStepDuration = 0.05f;    // Her adımın süresi (saniye)

    private const string PrefKeyStep = "TeleportShrinkStep";
    private const string PrefKeyDur  = "TeleportStepDuration";

    // ─── Durum Geçmişi ve Simülasyon Değişkenleri ──────────
    public static bool isSimulating = false;
    public static int simulatedWorld = -1;
    public static int simulatedLevel = -1;
    public static System.Collections.Generic.List<BlockState> correctStateHistory = new System.Collections.Generic.List<BlockState>();
    public System.Collections.Generic.List<BlockState> playerStateHistory = new System.Collections.Generic.List<BlockState>();

    [Header("Decorative / Menu")]
    [Tooltip("False ise klavye/swipe input'u yok sayılır (menü botları için).")]
    public bool acceptPlayerInput = true;
    [Tooltip("Menü arka planı: düşüşte level restart yok, spawn'a döner; ses/win kapalı.")]
    public bool decorativeMode = false;

    private Vector3 decorativeSpawnPos;
    private Quaternion decorativeSpawnRot = Quaternion.identity;

    private void Awake()
    {
        CaptureBaseTumblingDuration();
        ApplyMoveSpeedFromSettings();
    }

    private void OnEnable()
    {
        GameplaySettings.SettingsChanged += ApplyMoveSpeedFromSettings;
    }

    private void OnDisable()
    {
        GameplaySettings.SettingsChanged -= ApplyMoveSpeedFromSettings;
    }

    private void CaptureBaseTumblingDuration()
    {
        if (baseTumblingDuration < 0f)
        {
            baseTumblingDuration = tumblingDuration > 0f ? tumblingDuration : 0.3f;
        }
    }

    private void ApplyMoveSpeedFromSettings()
    {
        if (decorativeMode)
        {
            return;
        }

        CaptureBaseTumblingDuration();
        float multiplier = GameplaySettings.MoveSpeedMultiplier;
        tumblingDuration = baseTumblingDuration / Mathf.Max(0.01f, multiplier);
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // PlayerPrefs'te kaydedilmiş değer varsa yükle
        if (PlayerPrefs.HasKey(PrefKeyStep))
            teleportShrinkStep = PlayerPrefs.GetFloat(PrefKeyStep);
        if (PlayerPrefs.HasKey(PrefKeyDur))
            teleportStepDuration = PlayerPrefs.GetFloat(PrefKeyDur);

        if (decorativeMode)
        {
            playerStateHistory.Clear();
            playerStateHistory.Add(GetCurrentState());
            return;
        }

        ApplyMoveSpeedFromSettings();

        // Yeni bir level yüklendiyse simülasyon kilitlerini temizle
        if (LevelLoader.Instance != null && 
            (LevelLoader.Instance.worldIndex != simulatedWorld || LevelLoader.Instance.levelIndex != simulatedLevel))
        {
            isSimulating = false;
        }

        // Eğer seviye zaten fırınlanmış veriye sahipse simülasyonu çalıştırma, veriyi doğrudan yükle!
        if (LevelLoader.Instance != null && LevelLoader.Instance.CurrentLevelData != null && 
            LevelLoader.Instance.CurrentLevelData.correctStates != null && LevelLoader.Instance.CurrentLevelData.correctStates.Count > 0)
        {
            correctStateHistory = new System.Collections.Generic.List<BlockState>(LevelLoader.Instance.CurrentLevelData.correctStates);
            isSimulating = false;
            simulatedWorld = LevelLoader.Instance.worldIndex;
            simulatedLevel = LevelLoader.Instance.levelIndex;

            // Oyuncu geçmişini temizle ve başlangıç durumunu ekle
            playerStateHistory.Clear();
            playerStateHistory.Add(GetCurrentState());

            // Başlangıç animasyonunu oynat (Eğer Atomize seçildiyse)
            if (winAnimation == WinAnimationType.Atomize)
            {
                StartCoroutine(StartLevelAnimation());
            }
        }
        // Simülasyonu başlat (fırınlanmamışsa)
        else if (!isSimulating && LevelLoader.Instance != null &&
            (LevelLoader.Instance.worldIndex != simulatedWorld || LevelLoader.Instance.levelIndex != simulatedLevel))
        {
            StartCoroutine(RunSimulation());
        }
        else
        {
            // Simülasyon tamamlandıysa başlangıç durumunu oyuncu geçmişine ekle
            isSimulating = false;
            playerStateHistory.Clear();
            playerStateHistory.Add(GetCurrentState());
        }
    }

    /// <summary>Main menu dekoratif küp: input kapalı, düşüşte respawn.</summary>
    public void ConfigureDecorative(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        decorativeMode = true;
        acceptPlayerInput = false;
        decorativeSpawnPos = spawnPosition;
        decorativeSpawnRot = spawnRotation;
    }

    private void OnValidate()
    {
        // Inspector'da değer değiştirildiğinde otomatik kaydet
        PlayerPrefs.SetFloat(PrefKeyStep, teleportShrinkStep);
        PlayerPrefs.SetFloat(PrefKeyDur,  teleportStepDuration);
        PlayerPrefs.Save();
    }

    // ─── Dış Erişim (HintController için) ─────────────────
    /// <summary>Küp şu an hareket ediyorsa true döner.</summary>
    public bool IsMoving => isTumbling || playerFell;
    public int CurrentMoveCount { get; private set; }

    /// <summary>
    /// Dışarıdan (HintController) hamle tetiklemek için çağrılır.
    /// Küp meşgulse false döner, değilse hareketi başlatıp true döner.
    /// </summary>
    public bool TryMoveExternal(Vector3 direction)
    {
        if (!decorativeMode && GameUIController.Instance != null && GameUIController.Instance.BlocksGameplay)
        {
            return false;
        }

        if (isTumbling || playerFell) return false;
        CurrentMoveCount++;
        if (isSplit)
            StartCoroutine(Tumble1x1(direction));
        else
            StartCoroutine(Tumble(direction));
        return true;
    }

    private Vector3 GetSwipeDirection(Vector2 delta)
    {
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // Hassas İzometrik Kamera Eşleşmesi (Sağ: 355°-75°, Yukarı: 85°-165°, Sol: 175°-255°, Aşağı: 265°-345°)
        // Sağ (Right / Vector3.right)
        if ((angle >= 355f && angle <= 360f) || (angle >= 0f && angle < 75f))
        {
            return Vector3.right;
        }
        // Yukarı/İleri (Forward / Vector3.forward)
        else if (angle >= 85f && angle < 165f)
        {
            return Vector3.forward;
        }
        // Sol (Left / Vector3.left)
        else if (angle >= 175f && angle < 255f)
        {
            return Vector3.left;
        }
        // Aşağı/Geri (Back / Vector3.back)
        else if (angle >= 265f && angle < 345f)
        {
            return Vector3.back;
        }

        return Vector3.zero;
    }

    private void Update()
    {
        if (isSimulating) return;
        if (!acceptPlayerInput) return;

        if (GameUIController.Instance != null && GameUIController.Instance.BlocksGameplay)
        {
            isSwiping = false;
            return;
        }

        if (isSplit && !isTumbling && !playerFell)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                // Space basarak aktif küp değiştirdiğinde ipucunu sıfırla
                HintController hc = FindObjectOfType<HintController>();
                if (hc != null) hc.ResetHintState();

                SwitchActiveSplitPlayer();
            }
        }

        if (isTumbling || playerFell)
        {
            isSwiping = false;
            return;
        }

        Vector3 direction = Vector3.zero;

        var keyboardState = Keyboard.current;
        if (keyboardState != null)
        {
            if (keyboardState.wKey.wasPressedThisFrame || keyboardState.upArrowKey.wasPressedThisFrame) direction = Vector3.forward;
            else if (keyboardState.sKey.wasPressedThisFrame || keyboardState.downArrowKey.wasPressedThisFrame) direction = Vector3.back;
            else if (keyboardState.aKey.wasPressedThisFrame || keyboardState.leftArrowKey.wasPressedThisFrame) direction = Vector3.left;
            else if (keyboardState.dKey.wasPressedThisFrame || keyboardState.rightArrowKey.wasPressedThisFrame) direction = Vector3.right;
        }

        // Touchscreen Swipe Controls
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch != null)
        {
            var touch = touchscreen.primaryTouch;
            
            // Sadece aktif (basılı tutulan) dokunuşları say
            int activeTouchCount = 0;
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var t = touchscreen.touches[i];
                if (t.isInProgress) // Dokunuş şu an devam ediyorsa (Began, Moved, Stationary)
                {
                    activeTouchCount++;
                }
            }

            // Çoklu dokunma (zoom/pinch) durumunda tekli swipe algılamasını engelle
            if (activeTouchCount >= 2)
            {
                isSwiping = false;
            }
            else if (touch.press.wasPressedThisFrame)
            {
                // Don't start swiping if touching a UI element
                bool isOverUI = false;
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.IsPointerOverGameObject(-1))
                {
                    isOverUI = true;
                }
                
                if (!isOverUI)
                {
                    swipeStartPos = touch.position.ReadValue();
                    isSwiping = true;
                }
                else
                {
                    isSwiping = false;
                }
            }
            else if (touch.press.isPressed && isSwiping)
            {
                Vector2 currentPos = touch.position.ReadValue();
                Vector2 delta = currentPos - swipeStartPos;
                
                // Ekran yüksekliğinin %7.5'i kadar dinamik eşik (en az 50 piksel)
                float dynamicThreshold = Screen.height * 0.075f;
                if (dynamicThreshold < 50f) dynamicThreshold = 50f;

                if (delta.magnitude >= dynamicThreshold)
                {
                    isSwiping = false; // consume swipe
                    direction = GetSwipeDirection(delta);
                }
            }
            else if (touch.press.wasReleasedThisFrame)
            {
                isSwiping = false;
            }
        }

        // Mouse Drag Fallback (for testing in Editor)
        if (direction == Vector3.zero)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    // Don't start swiping if clicking on a UI element
                    bool isOverUI = false;
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null && es.IsPointerOverGameObject())
                    {
                        isOverUI = true;
                    }
                    
                    if (!isOverUI)
                    {
                        swipeStartPos = mouse.position.ReadValue();
                        isSwiping = true;
                    }
                    else
                    {
                        isSwiping = false;
                    }
                }
                else if (mouse.leftButton.isPressed && isSwiping)
                {
                    Vector2 currentPos = mouse.position.ReadValue();
                    Vector2 delta = currentPos - swipeStartPos;
                    
                    // Ekran yüksekliğinin %7.5'i kadar dinamik eşik (en az 50 piksel)
                    float dynamicThreshold = Screen.height * 0.075f;
                    if (dynamicThreshold < 50f) dynamicThreshold = 50f;

                    if (delta.magnitude >= dynamicThreshold)
                    {
                        isSwiping = false; // consume swipe
                        direction = GetSwipeDirection(delta);
                    }
                }
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    isSwiping = false;
                }
            }
        }

        if (direction != Vector3.zero)
        {
            // Oyuncu kendi fiziksel girdisiyle hamle yaptığında ipucunu sıfırla
            HintController hc = FindObjectOfType<HintController>();
            if (hc != null) hc.ResetHintState();

            CurrentMoveCount++;
            if (isSplit)
            {
                StartCoroutine(Tumble1x1(direction));
            }
            else
            {
                StartCoroutine(Tumble(direction));
            }
        }
    }

    private void SwitchActiveSplitPlayer()
    {
        activeSplitPlayer = (activeSplitPlayer == 1) ? 2 : 1;
        PlaySound(AudioEventId.PlayerSwitch, null);
        
        CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = GetActivePlayer().transform;
        }
    }

    private GameObject switchButtonObj;

    private void CreateSwitchButton()
    {
        if (switchButtonObj != null) return;

        // Ensure EventSystem exists so button clicks can be processed
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            
            // Try InputSystem UI Input Module (multiple possible assembly names)
            System.Type inputModuleType =
                System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
                System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI") ??
                System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            
            if (inputModuleType != null)
            {
                eventSystemObj.AddComponent(inputModuleType);
            }
            else
            {
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("SplitCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        switchButtonObj = new GameObject("SwitchPlayerButton");
        switchButtonObj.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = switchButtonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0);
        rectTransform.anchorMax = new Vector2(0.5f, 0);
        rectTransform.pivot = new Vector2(0.5f, 0);
        rectTransform.anchoredPosition = new Vector2(0, 40f);
        rectTransform.sizeDelta = new Vector2(240f, 65f);

        UnityEngine.UI.Image image = switchButtonObj.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.12f, 0.12f, 0.16f, 0.85f);

        UnityEngine.UI.Button button = switchButtonObj.AddComponent<UnityEngine.UI.Button>();
        switchButtonObj.AddComponent<UIAudioFeedback>();
        button.onClick.AddListener(() => {
            if (!isTumbling && isSplit)
            {
                SwitchActiveSplitPlayer();
                UpdateSwitchButtonText();
            }
        });

        UnityEngine.UI.ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.2f, 0.4f, 0.7f, 0.9f);
        cb.pressedColor = new Color(0.1f, 0.3f, 0.5f, 1f);
        button.colors = cb;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(switchButtonObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        UnityEngine.UI.Text textComponent = textObj.AddComponent<UnityEngine.UI.Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (textComponent.font == null)
        {
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        textComponent.fontSize = 20;
        textComponent.fontStyle = FontStyle.Bold;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.color = Color.white;
        
        UpdateSwitchButtonText();
    }

    private void UpdateSwitchButtonText()
    {
        if (switchButtonObj == null) return;
        UnityEngine.UI.Text textComponent = switchButtonObj.GetComponentInChildren<UnityEngine.UI.Text>();
        if (textComponent != null)
        {
            textComponent.text = "Küp Değiştir (" + activeSplitPlayer + "/2)";
        }
    }

    private void DestroySwitchButton()
    {
        if (switchButtonObj != null)
        {
            Destroy(switchButtonObj);
            switchButtonObj = null;
        }
    }

    private void OnDestroy()
    {
        DestroySwitchButton();
        // Sadece bu oyuncunun ürettiği atomları sil — sahnedeki tüm StartAtomParticle'lara
        // dokunma; yoksa Restart'ta yeni animasyon aynı karede başlamış atomlar da gider.
        DestroyTrackedAtomParticles();
    }

    private readonly System.Collections.Generic.List<GameObject> trackedAtomParticles =
        new System.Collections.Generic.List<GameObject>();

    private void TrackAtomParticle(GameObject go)
    {
        if (go != null) trackedAtomParticles.Add(go);
    }

    private void DestroyTrackedAtomParticles()
    {
        for (int i = 0; i < trackedAtomParticles.Count; i++)
        {
            if (trackedAtomParticles[i] != null)
                Destroy(trackedAtomParticles[i]);
        }
        trackedAtomParticles.Clear();
    }

    private IEnumerator Tumble(Vector3 direction)
    {
        isTumbling = true;
        lastMoveDir = direction;

        // Calculate heights and extents based on block orientation dynamically using local scale
        float verticalExtent = transform.localScale.x * 0.5f; // half of thickness
        // Check if block is standing upright
        if (Mathf.Abs(Vector3.Dot(transform.up, Vector3.up)) > 0.9f)
        {
            verticalExtent = transform.localScale.y * 0.5f; // half of height
        }

        float rollExtent = 0.5f;
        // Check if block is lying down along the roll direction
        if (Mathf.Abs(Vector3.Dot(transform.up, direction)) > 0.9f)
        {
            rollExtent = 1.0f;
        }

        Vector3 pivot = transform.position - Vector3.up * verticalExtent + direction * rollExtent;
        Vector3 rotAxis = Vector3.Cross(Vector3.up, direction);

        float totalRotation = 90f;
        float rotated = 0f;
        float speed = 90f / tumblingDuration;

        while (rotated < totalRotation)
        {
            float step = speed * Time.deltaTime;
            if (rotated + step > totalRotation)
            {
                step = totalRotation - rotated;
            }
            transform.RotateAround(pivot, rotAxis, step);
            rotated += step;
            yield return null;
        }

        SnapToGrid();
        // Ses, hareket sonrası dik/yatık dinlenme zemine göre çalınır.
        if (HasFullGroundSupport(transform))
        {
            PlayCubeMoveSound(transform);
        }
        CheckIfFell();

        // Konveyör tile tetiklediyse kayarak ilerle (devrilme değil)
        if (!playerFell && pendingConveyorDir != Vector3.zero)
        {
            Vector3 convDir = pendingConveyorDir;
            pendingConveyorDir = Vector3.zero;
            if (!isTeleporting) isTumbling = false;
            StartCoroutine(ConveyorSlide(convDir));
            yield break;
        }

        if (!isTeleporting) isTumbling = false;
    }

    /// <summary>
    /// Konveyör tile üzerinde: küp DİK konumda kalarak ok yönünde 1 hücre kayar.
    /// Rotasyon yoktur, sadece pozisyon değişir.
    /// </summary>
    private IEnumerator ConveyorSlide(Vector3 direction)
    {
        isTumbling = true;
        lastMoveDir = direction;
        PlaySound(AudioEventId.Conveyor, null);

        Vector3 startPos = transform.position;
        Vector3 endPos   = startPos + direction * 1f; // 1 hücre ilerle
        endPos.y = transform.localScale.y * 0.5f;     // Y sabit kalsın

        float duration = 0.25f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        SnapToGrid();
        CheckIfFell();

        // Zincirleme konveyör varsa devam et
        if (!playerFell && pendingConveyorDir != Vector3.zero)
        {
            Vector3 nextDir = pendingConveyorDir;
            pendingConveyorDir = Vector3.zero;
            if (!isTeleporting) isTumbling = false;
            StartCoroutine(ConveyorSlide(nextDir));
            yield break;
        }

        if (!isTeleporting) isTumbling = false;
    }

    private void SnapToGrid()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x * 2f) / 2f;
        pos.z = Mathf.Round(pos.z * 2f) / 2f;
        
        // Snapping Y dynamically based on orientation to prevent floating or sinking
        bool isStanding = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up)) > 0.9f;
        if (isStanding)
        {
            pos.y = transform.localScale.y * 0.5f;
        }
        else
        {
            pos.y = transform.localScale.x * 0.5f;
        }
        transform.position = pos;

        Vector3 rot = transform.eulerAngles;
        rot.x = Mathf.Round(rot.x / 90f) * 90f;
        rot.y = Mathf.Round(rot.y / 90f) * 90f;
        rot.z = Mathf.Round(rot.z / 90f) * 90f;
        transform.eulerAngles = rot;

        // Simülasyon değilse oyuncunun durum geçmişine kaydet
        if (!isSimulating)
        {
            playerStateHistory.Add(GetCurrentState());
        }
    }

    private void CheckIfFell()
    {
        bool isStanding = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up)) > 0.9f;
        bool isOnGround = false;

        if (isStanding)
        {
            isOnGround = CheckTile(transform.position);
        }
        else
        {
            Vector3 longAxisDir = transform.up;
            longAxisDir.y = 0;
            longAxisDir.Normalize();

            Vector3 pos1 = transform.position + longAxisDir * 0.5f;
            Vector3 pos2 = transform.position - longAxisDir * 0.5f;

            bool ground1 = CheckTile(pos1);
            bool ground2 = CheckTile(pos2);

            isOnGround = ground1 && ground2;

            // If only one end is on the ground, the block tips and falls
            if (ground1 != ground2)
            {
                StartCoroutine(TipAndFall(ground1 ? pos1 : pos2, ground1 ? -longAxisDir : longAxisDir));
                return;
            }
        }

        if (!isOnGround)
        {
            StartCoroutine(FallDown(Vector3.zero));
        }
    }

    private bool CheckTile(Vector3 position)
    {
        RaycastHit hit;
        // Cast a ray from slightly above the tile plane (Y = 0) downwards
        Vector3 origin = new Vector3(position.x, 0.5f, position.z);
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(origin, Vector3.down, out hit, 1.5f, layerMask, QueryTriggerInteraction.Collide))
        {
            HandleTileTriggers(hit.collider);
            return true;
        }
        return false;
    }

    private void HandleTileTriggers(Collider tileCollider)
    {
        bool isStanding = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up)) > 0.9f;

        if (tileCollider.GetComponentInParent<GoalTile>() != null)
        {
            if (isStanding)
            {
                StartCoroutine(WinLevel());
            }
        }
        else if (tileCollider.GetComponentInParent<FragileTile>() != null)
        {
            if (isStanding)
            {
                // Try to get FlexibleGlass component for a realistic shatter effect
                FlexibleGlass glass = tileCollider.GetComponentInParent<FlexibleGlass>();
                if (glass != null)
                {
                    glass.Fracture();
                    tileCollider.enabled = false; // Disable collision so player falls through
                    glass.WakeNeighbors(transform.position, 1.5f, Vector3.down * 10f); // Make pieces under player fall
                }
                else
                {
                    // Fallback to old behavior (instantly deactivate)
                    tileCollider.gameObject.SetActive(false);
                }
                StartCoroutine(FallDown(Vector3.zero, true));
            }
        }

        // Check for Split Switch
        if (tileCollider.gameObject.name.StartsWith("SplitSwitchTile_"))
        {
            if (isStanding)
            {
                TriggerSplit(tileCollider.gameObject.name);
            }
        }
        // Check for Teleport Switch
        else if (tileCollider.gameObject.name.StartsWith("TeleportTile_"))
        {
            if (isStanding)
            {
                TriggerTeleport(tileCollider.gameObject.name);
            }
        }

        // Check for Switch triggers
        SwitchController[] switches = tileCollider.GetComponentsInParent<SwitchController>();
        foreach (var sw in switches)
        {
            sw.TryPress(isStanding);
        }

        // Check for Conveyor (m tile) — sadece dik durumda çalışır
        ConveyorTile conveyor = tileCollider.GetComponentInParent<ConveyorTile>();
        if (conveyor != null && isStanding)
        {
            pendingConveyorDir = conveyor.direction;
        }
    }

    // Teleport handling
    // Teleport handling - moves player with shrink/expand animation
    private void TriggerTeleport(string tileName)
    {
        // Expected format: "TeleportTile_tX"
        int underscoreIdx = tileName.IndexOf('_');
        if (underscoreIdx == -1)
        {
            Debug.LogWarning($"Invalid teleport tile name: {tileName}");
            return;
        }
        string token = tileName.Substring(underscoreIdx + 1); // e.g., "t4"
        if (token.Length < 2)
        {
            Debug.LogWarning($"Invalid teleport token: {token}");
            return;
        }
        // Convert to appear token (replace leading 't' with 'a')
        string appearToken = "a" + token.Substring(1);
        // Find the corresponding AppearTile object
        GameObject appearTile = null;
        foreach (var obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (obj.name.StartsWith("AppearTile_" + appearToken))
            {
                appearTile = obj;
                break;
            }
        }
        if (appearTile != null)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = appearTile.transform.position;
            // Ensure target Y matches standing height
            targetPos.y = transform.localScale.y * 0.5f;
            StartCoroutine(TeleportSequenceScale(startPos, targetPos));
        }
        else
        {
            Debug.LogWarning($"Appear tile not found for token '{appearToken}'");
        }
    }



    private IEnumerator TipAndFall(Vector3 pivotPos, Vector3 fallDir)
    {
        isTumbling = true;
        playerFell = true;

        PlaySound(AudioEventId.GameOver, gameOverSound);

        // Tip 30 degrees around the edge before falling
        Vector3 pivot = pivotPos + fallDir * 0.5f - Vector3.up * 0.5f;
        Vector3 rotAxis = Vector3.Cross(Vector3.up, fallDir);

        float rotated = 0f;
        float rotateSpeed = 150f;

        while (rotated < 30f)
        {
            float step = rotateSpeed * Time.deltaTime;
            transform.RotateAround(pivot, rotAxis, step);
            rotated += step;
            yield return null;
        }

        // Apply frictionless material to prevent sticking to the edge/walls
        PhysicsMaterial frictionless = new PhysicsMaterial("Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.sharedMaterial = frictionless;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(fallDir * 0.4f, ForceMode.VelocityChange);
            rb.AddTorque(rotAxis * 2f, ForceMode.VelocityChange);
        }

        yield return new WaitForSeconds(decorativeMode ? 0.85f : 2.0f);
        if (decorativeMode)
        {
            RespawnDecorative();
            yield break;
        }
        if (isSimulating)
        {
            Debug.LogError($"[RunSimulation] Küp simülasyon sirasinda düstü! Sonsuz döngüyü önlemek için simülasyon kapatiliyor. World: {LevelLoader.Instance.worldIndex}, Level: {LevelLoader.Instance.levelIndex}");
            isSimulating = false;
            simulatedWorld = LevelLoader.Instance.worldIndex;
            simulatedLevel = LevelLoader.Instance.levelIndex;
        }
        RestartLevel();
    }

    private IEnumerator FallDown(Vector3 customRotAxis, bool straightDown = false)
    {
        isTumbling = true;
        playerFell = true;

        PlaySound(AudioEventId.GameOver, gameOverSound);

        Vector3 rotAxis = customRotAxis;
        if (rotAxis == Vector3.zero)
        {
            rotAxis = Vector3.Cross(Vector3.up, lastMoveDir);
        }

        // Apply frictionless material to prevent sticking to the edge/walls
        PhysicsMaterial frictionless = new PhysicsMaterial("Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.sharedMaterial = frictionless;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            
            if (!straightDown)
            {
                rb.AddForce(lastMoveDir * 0.3f, ForceMode.VelocityChange);
                rb.AddTorque(rotAxis * 2f, ForceMode.VelocityChange);
            }
            else
            {
                // Falling straight down a hole (e.g. shattered fragile tile).
                // Do not apply horizontal force or extreme torque to prevent exaggerated flips and stuck walls.
                // Just a tiny random torque so it tumbles very slightly and naturally.
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.5f, ForceMode.VelocityChange);
            }
        }

        yield return new WaitForSeconds(decorativeMode ? 0.85f : 2.0f);
        if (decorativeMode)
        {
            RespawnDecorative();
            yield break;
        }
        if (isSimulating)
        {
            Debug.LogError($"[RunSimulation] Küp simülasyon sirasinda düstü! Sonsuz döngüyü önlemek için simülasyon kapatiliyor. World: {LevelLoader.Instance.worldIndex}, Level: {LevelLoader.Instance.levelIndex}");
            isSimulating = false;
            simulatedWorld = LevelLoader.Instance.worldIndex;
            simulatedLevel = LevelLoader.Instance.levelIndex;
        }
        RestartLevel();
    }

    private void RespawnDecorative()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        transform.SetPositionAndRotation(decorativeSpawnPos, decorativeSpawnRot);
        SnapToGrid();
        playerFell = false;
        isTumbling = false;
        pendingConveyorDir = Vector3.zero;
        isTeleporting = false;
    }

    private IEnumerator WinLevel()
    {
        if (isSimulating || decorativeMode) yield break;

        isTumbling = true;
        playerFell = true;

        PlaySound(AudioEventId.LevelComplete, winSound);

        Vector3 startPos = transform.position;

        // Kamera takibini durdur (küp havaya uçarken kamera sabit kalsın)
        CameraFollow cameraFollow = FindObjectsByType<CameraFollow>(FindObjectsSortMode.None)?[0];
        if (cameraFollow != null)
        {
            cameraFollow.target = null;
        }

        // Geçici bir parıltı ışığı (Point Light) oluştur
        GameObject lightObj = new GameObject("WarpGlowLight");
        lightObj.transform.SetParent(transform); // Oyuncu yok edildiğinde otomatik temizlensin
        lightObj.transform.position = startPos + Vector3.up * 0.5f;
        Light glowLight = lightObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.18f, 0.65f, 1f); // Neon Mavi
        glowLight.range = 8f;
        glowLight.shadows = LightShadows.None;

        float duration = 1.4f; // Animasyon süresi
        float elapsed = 0f;
        float maxSpinSpeed = 1350f; // Maksimum spin hızı

        if (winAnimation == WinAnimationType.RocketUp)
        {
            Vector3 targetPos = startPos + Vector3.up * 14f; // Ekrandan çıkacak kadar yüksek

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Kübik Ease-In ivmelenmesi
                float easeInT = t * t * t;

                // Pozisyon yükselmesi
                transform.position = Vector3.Lerp(startPos, targetPos, easeInT);

                // Dönüş hızı ivmelenmesi
                float currentSpinSpeed = Mathf.Lerp(180f, maxSpinSpeed, t);
                transform.Rotate(Vector3.up, currentSpinSpeed * Time.deltaTime, Space.World);

                // Işık parlama/sönme dalgalanması
                if (t < 0.3f)
                    glowLight.intensity = Mathf.Lerp(0f, 15f, t / 0.3f);
                else
                    glowLight.intensity = Mathf.Lerp(15f, 0f, (t - 0.3f) / 0.7f);

                yield return null;
            }
        }
        else if (winAnimation == WinAnimationType.SplitWarp) // SplitWarp: Küpü dikey olarak ikiye bölüp zıt yönlerde fırlatmak
        {
            // Ana küp görsellerini ve collider'larını gizle
            SetRenderersAndCollidersActive(false);

            // Alt ve Üst yarı küplerin başlangıç pozisyonları
            Vector3 pos1 = startPos - Vector3.up * 0.45f;
            Vector3 pos2 = startPos + Vector3.up * 0.45f;

            // 1x1 küp oluşturucularla geçici küpleri oluştur
            GameObject part1 = Create1x1Block(pos1, "WinPart1");
            GameObject part2 = Create1x1Block(pos2, "WinPart2");

            // Parçaların hedefleri (Yükselirken biri sola, biri sağa kavislenir)
            Vector3 targetPos1 = pos1 + Vector3.up * 14f + Vector3.left * 1.8f;
            Vector3 targetPos2 = pos2 + Vector3.up * 14f + Vector3.right * 1.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeInT = t * t * t;

                // Parça 1 (Alt): Yukarı ve Sola uçar, Pozitif yönde döner
                if (part1 != null)
                {
                    part1.transform.position = Vector3.Lerp(pos1, targetPos1, easeInT);
                    float currentSpin = Mathf.Lerp(180f, maxSpinSpeed, t);
                    part1.transform.Rotate(Vector3.up, currentSpin * Time.deltaTime, Space.World);
                }

                // Parça 2 (Üst): Yukarı ve Sağa uçar, Negatif (ters) yönde döner
                if (part2 != null)
                {
                    part2.transform.position = Vector3.Lerp(pos2, targetPos2, easeInT);
                    float currentSpin = Mathf.Lerp(180f, maxSpinSpeed, t);
                    part2.transform.Rotate(Vector3.up, -currentSpin * Time.deltaTime, Space.World);
                }

                // Işık parlama/sönme dalgalanması
                if (t < 0.3f)
                    glowLight.intensity = Mathf.Lerp(0f, 15f, t / 0.3f);
                else
                    glowLight.intensity = Mathf.Lerp(15f, 0f, (t - 0.3f) / 0.7f);

                yield return null;
            }

            // Temizlik: Geçici parçaları yok et
            if (part1 != null) Destroy(part1);
            if (part2 != null) Destroy(part2);

            // Ana küpü tekrar görünür yap (sahne geçişi temiz olsun)
            SetRenderersAndCollidersActive(true);
        }
        else // Atomize: Bilim kurgu ışınlanması gibi atomlarına ayrılıp yukarı uçma
        {
            // Oyuncunun materyalini gizlemeden önce referans al
            Renderer mainRenderer = GetComponentInChildren<Renderer>();
            Material playerMat = mainRenderer != null ? mainRenderer.sharedMaterial : null;

            // Ana küp görsellerini ve collider'larını gizle
            SetRenderersAndCollidersActive(false);

            // Sütun ve yükseklik parametreleri (3x3 sütun düzeninde, her sütunda 6 katman var)
            int countX = 3;
            int countY = 6;
            int countZ = 3;

            // Parçaların başlangıçta boşluksuz bir bütün oluşturması için tam boyut hesabı
            float sx = 1.0f / countX;
            float sy = 1.0f / countY; // Unity standart Cube mesh'inin yerel boyutu 1.0f'dir, ölçekleme originalScale ile yapılır
            float sz = 1.0f / countZ;

            Vector3 originalScale = transform.localScale;
            Vector3 atomScale = new Vector3(sx * originalScale.x, sy * originalScale.y, sz * originalScale.z);

            // 9 sütunu temsil eden listeler (3x3 = 9)
            System.Collections.Generic.List<AtomInfo>[] columns = new System.Collections.Generic.List<AtomInfo>[9];
            for (int i = 0; i < 9; i++)
            {
                columns[i] = new System.Collections.Generic.List<AtomInfo>();
            }

            for (int x = 0; x < countX; x++)
            {
                for (int y = 0; y < countY; y++)
                {
                    for (int z = 0; z < countZ; z++)
                    {
                        // Pozisyonları 1x1x1 yerel hacmine boşluksuz yerleşecek şekilde hesapla
                        float px = -0.5f + (x + 0.5f) * sx;
                        float py = -0.5f + (y + 0.5f) * sy; // py aralığı yerel mesh sınırları olan -0.5 ile 0.5 arasındadır
                        float pz = -0.5f + (z + 0.5f) * sz;

                        Vector3 localPos = new Vector3(px, py, pz);
                        Vector3 worldPos = transform.TransformPoint(localPos);

                        GameObject atom = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        atom.name = "AtomParticle";
                        TrackAtomParticle(atom);
                        atom.transform.position = worldPos;
                        atom.transform.rotation = transform.rotation; // Ana küpün rotasyonunu birebir koru
                        atom.transform.localScale = atomScale; // Tam birleşen boyutlar

                        // Fizik çakışmasını önlemek için collider'ı hemen yok et
                        Collider c = atom.GetComponent<Collider>();
                        if (c != null) Destroy(c);

                        // Blok materyalini ata
                        if (playerMat != null)
                        {
                            atom.GetComponent<Renderer>().material = playerMat;
                        }

                        // Atom verisini oluştur
                        AtomInfo info = new AtomInfo();
                        info.go = atom;
                        info.startPos = worldPos;

                        // Sütun index'i: x * 3 + z (0 ile 8 arası)
                        int colIndex = x * 3 + z;
                        columns[colIndex].Add(info);
                    }
                }
            }

            // Düzleştirilmiş tüm atom verileri listesi
            System.Collections.Generic.List<AtomInfo> allAtoms = new System.Collections.Generic.List<AtomInfo>();

            // --- SÜTUN BAZLI DÜNYA Y SIRALAMASI VE GEÇİŞ ZAMANLAMASI ---
            for (int colIndex = 0; colIndex < 9; colIndex++)
            {
                var column = columns[colIndex];
                
                // Sütundaki atomları dünya Y eksenine göre yukarıdan aşağıya (descending) sırala
                column.Sort((a, b) => b.startPos.y.CompareTo(a.startPos.y));

                // Her sütun için rastgele bir kalkış başlangıç gecikmesi (0 ile 0.22s arası)
                // Bu gecikme, sütunların birbirinden farklı zamanlarda çözünmeye başlamasını sağlar.
                float columnStartDelay = UnityEngine.Random.Range(0f, 0.22f);

                for (int j = 0; j < column.Count; j++)
                {
                    AtomInfo info = column[j];
                    
                    // Sütun içindeki dikey sıralamaya göre kalkış eşiği:
                    // Üstteki atom havalandıktan çok kısa bir süre sonra (0.08s gecikmeyle) altındaki atom tetiklenir.
                    // Böylece alttaki katman, tüm üst katmanın bitmesini beklemeden kendi kolonunda akmaya başlar!
                    info.startThreshold = columnStartDelay + (j * 0.08f);

                    // Yükseklik sırasına göre hız ivme çarpanı
                    // Üstteki parçacıklar daha hızlı havalanarak aradaki mesafeyi açar
                    float speedMultiplier = 1.6f - (j * 0.12f);
                    info.speed = UnityEngine.Random.Range(2.2f, 3.2f) * speedMultiplier;

                    allAtoms.Add(info);
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Tüm atomları hareket ettir ve küçült
                for (int i = 0; i < allAtoms.Count; i++)
                {
                    AtomInfo info = allAtoms[i];
                    if (info.go == null) continue;

                    // Sırası gelmeyen atomlar başlangıç pozisyonunda (yani ana blok bütününde) sabit bekler
                    if (t < info.startThreshold)
                    {
                        info.go.transform.position = info.startPos;
                    }
                    else
                    {
                        // Bu parça için etkin süreci saniye cinsinden hesapla
                        float activeTime = elapsed - (info.startThreshold * duration);
                        
                        // Fiziksel ivmelenme formülü: d = v0 * t + 0.5 * a * t^2
                        float v0 = info.speed; // başlangıç hızı
                        float a = 12f;         // yukarı çekim ivmesi
                        float yOffset = v0 * activeTime + 0.5f * a * activeTime * activeTime;

                        // Dönme olmadan, doğrudan dikey yukarı doğru çekilme
                        info.go.transform.position = info.startPos + Vector3.up * yOffset;

                        // Bu parça için normalize edilmiş aktiflik süresi (0 ile 1 arası)
                        float totalActiveTime = duration - (info.startThreshold * duration);
                        float normalizedActiveT = totalActiveTime > 0.01f ? Mathf.Clamp01(activeTime / totalActiveTime) : 1f;

                        // Yükselirken küçülerek çözünme (Dissolve) efekti
                        info.go.transform.localScale = Vector3.Lerp(atomScale, Vector3.zero, normalizedActiveT);
                    }
                }

                // Işık parlama/sönme dalgalanması
                if (t < 0.3f)
                    glowLight.intensity = Mathf.Lerp(0f, 15f, t / 0.3f);
                else
                    glowLight.intensity = Mathf.Lerp(15f, 0f, (t - 0.3f) / 0.7f);

                yield return null;
            }

            // Animasyon bittiğinde tüm atomları yok et
            foreach (var info in allAtoms)
            {
                if (info.go != null) Destroy(info.go);
            }
            trackedAtomParticles.Clear();

            // Ana küpü tekrar görünür yap
            SetRenderersAndCollidersActive(true);
        }

        // Genel temizlik
        Destroy(lightObj);

        // Level sonu menüsünü aç; sonraki levele oyuncu karar verir.
        if (GameUIController.Instance != null)
        {
            GameUIController.Instance.ShowLevelComplete();
        }
        else if (LevelLoader.Instance != null)
        {
            // Menü yoksa eski otomatik geçişe düş.
            int nextLevel = LevelLoader.Instance.levelIndex + 1;
            if (nextLevel <= 20)
            {
                LevelLoader.Instance.LoadLevel(LevelLoader.Instance.worldIndex, nextLevel);
            }
            else
            {
                int nextWorld = LevelLoader.Instance.worldIndex + 1;
                if (nextWorld <= 15)
                {
                    LevelLoader.Instance.LoadLevel(nextWorld, 1);
                }
                else
                {
                    LevelLoader.Instance.LoadLevel(1, 1);
                }
            }
        }
    }

    private System.Collections.IEnumerator StartLevelAnimation()
    {
        // Işınlanma animasyonu boyunca oyuncu hareket edemesin
        isTumbling = true; 

        // Oyuncunun materyalini ve collider/renderer durumunu sakla
        Renderer mainRenderer = GetComponentInChildren<Renderer>();
        Material playerMat = mainRenderer != null ? mainRenderer.sharedMaterial : null;

        SetRenderersAndCollidersActive(false);

        float duration = 1.4f;
        float elapsed = 0f;

        int countX = 3;
        int countY = 6;
        int countZ = 3;

        float sx = 1.0f / countX;
        float sy = 1.0f / countY;
        float sz = 1.0f / countZ;

        Vector3 originalScale = transform.localScale;
        Vector3 atomScale = new Vector3(sx * originalScale.x, sy * originalScale.y, sz * originalScale.z);

        System.Collections.Generic.List<AtomInfo>[] columns = new System.Collections.Generic.List<AtomInfo>[9];
        for (int i = 0; i < 9; i++)
        {
            columns[i] = new System.Collections.Generic.List<AtomInfo>();
        }

        // Atomları başlangıç (hedef) pozisyonlarında yarat
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    float px = -0.5f + (x + 0.5f) * sx;
                    float py = -0.5f + (y + 0.5f) * sy;
                    float pz = -0.5f + (z + 0.5f) * sz;

                    Vector3 localPos = new Vector3(px, py, pz);
                    Vector3 worldPos = transform.TransformPoint(localPos);

                    GameObject atom = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    atom.name = "StartAtomParticle";
                    TrackAtomParticle(atom);
                    
                    // Gökyüzünde görünmez olarak başlat
                    atom.transform.position = worldPos + Vector3.up * 14f;
                    atom.transform.rotation = transform.rotation;
                    atom.transform.localScale = Vector3.zero;

                    Collider c = atom.GetComponent<Collider>();
                    if (c != null) Destroy(c);

                    if (playerMat != null)
                    {
                        atom.GetComponent<Renderer>().material = playerMat;
                    }

                    AtomInfo info = new AtomInfo();
                    info.go = atom;
                    info.startPos = worldPos; // Hedef pozisyonu

                    int colIndex = x * 3 + z;
                    columns[colIndex].Add(info);
                }
            }
        }

        System.Collections.Generic.List<AtomInfo> allAtoms = new System.Collections.Generic.List<AtomInfo>();

        // Her sütunu dünya Y eksenine göre aşağıdan yukarıya (ascending) sırala (Bölüm başında alttan üste birleşecek)
        for (int colIndex = 0; colIndex < 9; colIndex++)
        {
            var column = columns[colIndex];
            column.Sort((a, b) => a.startPos.y.CompareTo(b.startPos.y));

            float columnStartDelay = UnityEngine.Random.Range(0f, 0.22f);

            for (int j = 0; j < column.Count; j++)
            {
                AtomInfo info = column[j];
                // Sırayla gelme: Alttaki atom en erken gelir, üstteki en son gelir
                info.startThreshold = columnStartDelay + (j * 0.08f);
                
                float speedMultiplier = 1.0f + (j * 0.12f);
                info.speed = UnityEngine.Random.Range(2.2f, 3.2f) * speedMultiplier;

                allAtoms.Add(info);
            }
        }

        // Işık oluştur (Glow efekti)
        GameObject lightObj = new GameObject("StartWarpLight");
        lightObj.transform.SetParent(transform); // Oyuncu yok edildiğinde otomatik temizlensin
        Light glowLight = lightObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0f, 0.8f, 1f); // Neon sci-fi ışığı
        glowLight.range = 7f;
        glowLight.intensity = 0f;
        lightObj.transform.position = transform.position + Vector3.up * 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            for (int i = 0; i < allAtoms.Count; i++)
            {
                AtomInfo info = allAtoms[i];
                if (info.go == null) continue;

                if (t < info.startThreshold)
                {
                    // Sırası gelmeyenler gökyüzünde görünmez bekler
                    info.go.transform.position = info.startPos + Vector3.up * 14f;
                    info.go.transform.localScale = Vector3.zero;
                }
                else
                {
                    float totalActiveTime = duration - (info.startThreshold * duration);
                    float activeTime = elapsed - (info.startThreshold * duration);
                    float normalizedActiveT = totalActiveTime > 0.01f ? Mathf.Clamp01(activeTime / totalActiveTime) : 1f;

                    // Kübik Ease-Out (Gökten gelip yavaşça yatağa oturma)
                    float easeOutT = 1f - Mathf.Pow(1f - normalizedActiveT, 3f);

                    Vector3 skyPos = info.startPos + Vector3.up * 14f;
                    info.go.transform.position = Vector3.Lerp(skyPos, info.startPos, easeOutT);
                    info.go.transform.localScale = Vector3.Lerp(Vector3.zero, atomScale, normalizedActiveT);
                }
            }

            // Işık parlama/sönme dalgalanması (Warp etkisi)
            if (t < 0.4f)
                glowLight.intensity = Mathf.Lerp(0f, 15f, t / 0.4f);
            else
                glowLight.intensity = Mathf.Lerp(15f, 0f, (t - 0.4f) / 0.6f);

            yield return null;
        }

        // Temizlik
        foreach (var info in allAtoms)
        {
            if (info.go != null) Destroy(info.go);
        }
        trackedAtomParticles.Clear();

        Destroy(lightObj);

        // Ana küpü tekrar görünür yap
        SetRenderersAndCollidersActive(true);
        
        isTumbling = false; // Oyuncunun hareket kilidini aç
    }

    private GameObject GetActivePlayer()
    {
        return (activeSplitPlayer == 1) ? player1 : player2;
    }

    private void StartSplit(Vector3 pos1, Vector3 pos2)
    {
        isSplit = true;
        activeSplitPlayer = 1;
        PlaySound(AudioEventId.Split, null);

        // Create player1 and player2 GameObjects
        player1 = Create1x1Block(pos1, "SplitPlayer1");
        player2 = Create1x1Block(pos2, "SplitPlayer2");

        // Deactivate main block visual/collision
        SetMainBlockActive(false);

        // Snap camera to player1
        CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = player1.transform;
            cameraFollow.SnapToTarget();
        }

        // Create UI Button
        CreateSwitchButton();
    }

    private GameObject Create1x1Block(Vector3 position, string name)
    {
        GameObject block;
        if (LevelLoader.Instance != null && LevelLoader.Instance.playerBlockPrefab != null)
        {
            block = Instantiate(LevelLoader.Instance.playerBlockPrefab, position, Quaternion.identity);
            
            TumbleController clonedController = block.GetComponent<TumbleController>();
            if (clonedController != null)
            {
                Destroy(clonedController);
            }
            
            // Scale to 1x1x1
            Renderer[] renderers = block.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                Vector3 size = combinedBounds.size;
                Vector3 currentScale = block.transform.localScale;

                float targetScaleVal = 0.9f;

                float scaleX = (size.x > 0.01f) ? (targetScaleVal / size.x) * currentScale.x : currentScale.x;
                float scaleY = (size.y > 0.01f) ? (targetScaleVal / size.y) * currentScale.y : currentScale.y;
                float scaleZ = (size.z > 0.01f) ? (targetScaleVal / size.z) * currentScale.z : currentScale.z;

                block.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                // Re-calculate bounds after scaling
                combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                float minY = combinedBounds.min.y;
                block.transform.position = new Vector3(position.x, position.y - minY, position.z);
            }
        }
        else
        {
            block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.position = position;
            block.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(0.2f, 0.6f, 0.9f); // Blue
                renderer.material = material;
            }
        }

        block.name = name;
        block.tag = "Player";
        
        Rigidbody blockRb = block.GetComponent<Rigidbody>();
        if (blockRb == null)
        {
            blockRb = block.AddComponent<Rigidbody>();
        }
        blockRb.isKinematic = true;

        // Simülasyon sırasında 1x1 blok görsellerini gizle
        if (isSimulating)
        {
            foreach (var r in block.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in block.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        if (CubeThemeManager.Instance != null)
        {
            CubeThemeManager.Instance.ApplyTo(block);
        }

        return block;
    }

    private void SetMainBlockActive(bool active)
    {
        // Renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // If it belongs to player1 or player2, don't change it
            if (player1 != null && r.transform.IsChildOf(player1.transform)) continue;
            if (player2 != null && r.transform.IsChildOf(player2.transform)) continue;
            r.enabled = active;
        }

        // Colliders
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (player1 != null && c.transform.IsChildOf(player1.transform)) continue;
            if (player2 != null && c.transform.IsChildOf(player2.transform)) continue;
            c.enabled = active;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    private IEnumerator Tumble1x1(Vector3 direction)
    {
        isTumbling = true;
        lastMoveDir = direction;

        GameObject activePlayer = GetActivePlayer();
        float verticalExtent = activePlayer.transform.localScale.y * 0.5f;
        float rollExtent = 0.5f;

        Vector3 pivot = activePlayer.transform.position - Vector3.up * verticalExtent + direction * rollExtent;
        Vector3 rotAxis = Vector3.Cross(Vector3.up, direction);

        float totalRotation = 90f;
        float rotated = 0f;
        float speed = 90f / tumblingDuration;

        while (rotated < totalRotation)
        {
            float step = speed * Time.deltaTime;
            if (rotated + step > totalRotation)
            {
                step = totalRotation - rotated;
            }
            activePlayer.transform.RotateAround(pivot, rotAxis, step);
            rotated += step;
            yield return null;
        }

        SnapToGrid1x1(activePlayer);
        if (HasTileAt(activePlayer.transform.position))
        {
            PlayCubeMoveSound(activePlayer.transform);
        }
        CheckIfFell1x1();

        if (!playerFell)
        {
            CheckMerge();
        }

        isTumbling = false;
    }

    private void SnapToGrid1x1(GameObject target)
    {
        Vector3 pos = target.transform.position;
        pos.x = Mathf.Round(pos.x);
        pos.z = Mathf.Round(pos.z);
        pos.y = target.transform.localScale.y * 0.5f;
        target.transform.position = pos;

        Vector3 rot = target.transform.eulerAngles;
        rot.x = Mathf.Round(rot.x / 90f) * 90f;
        rot.y = Mathf.Round(rot.y / 90f) * 90f;
        rot.z = Mathf.Round(rot.z / 90f) * 90f;
        target.transform.eulerAngles = rot;

        // Simülasyon değilse oyuncunun durum geçmişine kaydet
        if (!isSimulating)
        {
            playerStateHistory.Add(GetCurrentState());
        }
    }

    private void CheckIfFell1x1()
    {
        bool ground1 = CheckTile1x1(player1.transform.position, player1);
        bool ground2 = CheckTile1x1(player2.transform.position, player2);

        if (!ground1 || !ground2)
        {
            StartCoroutine(FallDown1x1(!ground1 ? player1 : player2));
        }
    }

    private bool CheckTile1x1(Vector3 position, GameObject playerObj)
    {
        RaycastHit hit;
        Vector3 origin = new Vector3(position.x, 0.5f, position.z);
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(origin, Vector3.down, out hit, 1.5f, layerMask, QueryTriggerInteraction.Collide))
        {
            HandleTileTriggers1x1(hit.collider, playerObj);
            return true;
        }
        return false;
    }

    private void HandleTileTriggers1x1(Collider tileCollider, GameObject playerObj)
    {
        if (tileCollider.GetComponentInParent<GoalTile>() != null)
        {
            return;
        }
        else if (tileCollider.GetComponentInParent<FragileTile>() != null)
        {
            return;
        }

        SwitchController[] switches = tileCollider.GetComponentsInParent<SwitchController>();
        foreach (var sw in switches)
        {
            if (sw.switchType.StartsWith("s"))
            {
                sw.TryPress(true);
            }
        }
    }

    private IEnumerator FallDown1x1(GameObject fallingPlayer)
    {
        isTumbling = true;
        playerFell = true;
        DestroySwitchButton();

        PlaySound(AudioEventId.GameOver, gameOverSound);

        Rigidbody fallingRb = fallingPlayer.GetComponent<Rigidbody>();
        if (fallingRb != null)
        {
            fallingRb.isKinematic = false;
            fallingRb.useGravity = true;
            fallingRb.AddForce(lastMoveDir * 0.3f, ForceMode.VelocityChange);
            fallingRb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
        }

        yield return new WaitForSeconds(2.0f);
        RestartLevel();
    }

    private void CheckMerge()
    {
        Vector3 p1 = player1.transform.position;
        Vector3 p2 = player2.transform.position;

        float distance = Vector3.Distance(p1, p2);
        if (Mathf.Approximately(distance, 1.0f) || distance < 1.05f)
        {
            isSplit = false;

            Vector3 midPoint = (p1 + p2) * 0.5f;
            bool adjacentAlongX = Mathf.Abs(p1.x - p2.x) > 0.9f;

            transform.position = midPoint;
            
            if (adjacentAlongX)
            {
                transform.rotation = Quaternion.Euler(0, 0, -90);
            }
            else
            {
                transform.rotation = Quaternion.Euler(90, 0, 0);
            }

            SnapToGrid();

            Destroy(player1);
            Destroy(player2);
            DestroySwitchButton();

            SetMainBlockActive(true);

            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.target = transform;
            }
        }
    }

    private void TriggerSplit(string tileName)
    {
        RaycastHit hit;
        Vector3 rayOrigin = new Vector3(transform.position.x, 0.5f, transform.position.z);
        int layerMask = ~LayerMask.GetMask("Player");
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1.5f, layerMask, QueryTriggerInteraction.Collide))
        {
            SplitTile splitTile = hit.collider.GetComponentInParent<SplitTile>();
            if (splitTile != null && splitTile.hasTargets)
            {
                StartSplit(splitTile.target1, splitTile.target2);
                return;
            }
        }

        string token = "";
        int underscoreIdx = tileName.IndexOf('_');
        if (underscoreIdx != -1)
        {
            token = tileName.Substring(underscoreIdx + 1);
        }
        else
        {
            token = "m";
        }

        var allObjs = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        var matchingObjs = new System.Collections.Generic.List<GameObject>();
        foreach (var obj in allObjs)
        {
            if (obj.name == "SplitSwitchTile_" + token)
            {
                matchingObjs.Add(obj);
            }
        }

        Vector3 target1Pos = transform.position;
        Vector3 target2Pos = transform.position;

        GameObject switchObj = null;
        float minDistance = float.MaxValue;
        foreach (var obj in matchingObjs)
        {
            float dist = Vector3.Distance(obj.transform.position, transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                switchObj = obj;
            }
        }

        if (switchObj != null)
        {
            matchingObjs.Remove(switchObj);
        }

        if (matchingObjs.Count == 2)
        {
            target1Pos = matchingObjs[0].transform.position;
            target2Pos = matchingObjs[1].transform.position;
        }
        else if (matchingObjs.Count == 1)
        {
            target1Pos = switchObj.transform.position;
            target2Pos = matchingObjs[0].transform.position;
        }

        target1Pos.y = 0.45f;
        target2Pos.y = 0.45f;

        StartSplit(target1Pos, target2Pos);
    }



    /// <summary>
    /// Animates a “sink‑and‑rise” teleport effect.
    /// 1) Shrink & sink the block at the source.
    /// 2) Teleport instantly to the target ground.
    /// 3) Grow & rise to the standing height.
    /// </summary>
    private System.Collections.IEnumerator TeleportSequenceScale(Vector3 startPos, Vector3 targetPos)
    {
        isTeleporting = true;
        isTumbling = true; // Teleport animasyonu boyunca girdi ve hareket sistemlerini kilitle
        PlaySound(AudioEventId.Teleport, null);

        // Preserve original scale
        Vector3 originalScale = transform.localScale;

        // Floor Y of the source tile (bottom of block)
        float sourceFloorY = startPos.y - originalScale.y * 0.5f;

        // ----- Sink (Y-only, bottom stays on floor) -----
        float currentY = originalScale.y;
        while (currentY > 0f)
        {
            currentY = Mathf.Max(0f, currentY - teleportShrinkStep);
            transform.localScale = new Vector3(originalScale.x, currentY, originalScale.z);
            // Keep bottom on source floor: centre = floorY + halfHeight
            transform.position = new Vector3(startPos.x, sourceFloorY + currentY * 0.5f, startPos.z);
            yield return new WaitForSeconds(teleportStepDuration);
        }

        // Floor Y of the target tile (bottom of block)
        float targetFloorY = targetPos.y - originalScale.y * 0.5f;

        // Snap to target with scale = 0, position at floor level
        transform.localScale = new Vector3(originalScale.x, 0f, originalScale.z);
        transform.position = new Vector3(targetPos.x, targetFloorY, targetPos.z);

        // ----- Rise (Y-only, bottom stays on target floor) -----
        currentY = 0f;
        while (currentY < originalScale.y)
        {
            currentY = Mathf.Min(originalScale.y, currentY + teleportShrinkStep);
            transform.localScale = new Vector3(originalScale.x, currentY, originalScale.z);
            // Keep bottom on target floor: centre = floorY + halfHeight
            transform.position = new Vector3(targetPos.x, targetFloorY + currentY * 0.5f, targetPos.z);
            yield return new WaitForSeconds(teleportStepDuration);
        }

        // Ensure exact final state
        transform.localScale = originalScale;
        transform.position = targetPos;
        SnapToGrid();

        // Update camera
        CameraFollow cam = FindObjectOfType<CameraFollow>();
        if (cam != null) cam.SnapToTarget();

        isTeleporting = false;
        isTumbling = false; // Teleport tamamlandığında kilidi kaldır
    }


    private void RestartLevel()
    {
        if (LevelLoader.Instance != null)
        {
            LevelLoader.Instance.LoadLevel(LevelLoader.Instance.worldIndex, LevelLoader.Instance.levelIndex);
        }
    }

    private void PlaySound(AudioEventId eventId, AudioClip fallbackClip)
    {
        // Simülasyon / menü dekorunda sesleri çalma
        if (isSimulating || decorativeMode) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEvent(eventId, transform.position, fallbackClip);
        }
        else if (fallbackClip != null)
        {
            AudioSource.PlayClipAtPoint(fallbackClip, transform.position);
        }
    }

    private void PlayCubeMoveSound(Transform mover)
    {
        AudioEventId eventId = IsOnGlassSurface(mover)
            ? AudioEventId.CubeMoveGlass
            : AudioEventId.CubeMove;

        // Cam hareket sesi atanmamışsa normal hareket sesine düş.
        if (eventId == AudioEventId.CubeMoveGlass &&
            AudioManager.Instance?.EventLibrary?.cubeMoveGlass?.clip == null)
        {
            eventId = AudioEventId.CubeMove;
        }

        PlaySound(eventId, tumbleSound);
    }

    /// <summary>
    /// Küpün hareket sonrası dik veya yatık olarak temas ettiği zemine bakar.
    /// Yatıkken her iki destek noktası da kontrol edilir.
    /// </summary>
    private bool IsOnGlassSurface(Transform mover)
    {
        if (mover == null)
        {
            return false;
        }

        bool isStanding = Mathf.Abs(Vector3.Dot(mover.up, Vector3.up)) > 0.9f;
        if (isStanding)
        {
            return IsGlassAt(mover.position);
        }

        // Yatık 1x2: uzun eksenin iki ucu zemine basar.
        Vector3 longAxisDir = mover.up;
        longAxisDir.y = 0f;
        if (longAxisDir.sqrMagnitude < 0.001f)
        {
            return IsGlassAt(mover.position);
        }

        longAxisDir.Normalize();
        bool end1Glass = IsGlassAt(mover.position + longAxisDir * 0.5f);
        bool end2Glass = IsGlassAt(mover.position - longAxisDir * 0.5f);
        return end1Glass || end2Glass;
    }

    private static bool HasFullGroundSupport(Transform mover)
    {
        if (mover == null)
        {
            return false;
        }

        bool isStanding = Mathf.Abs(Vector3.Dot(mover.up, Vector3.up)) > 0.9f;
        if (isStanding)
        {
            return HasTileAt(mover.position);
        }

        Vector3 longAxisDir = mover.up;
        longAxisDir.y = 0f;
        if (longAxisDir.sqrMagnitude < 0.001f)
        {
            return HasTileAt(mover.position);
        }

        longAxisDir.Normalize();
        return HasTileAt(mover.position + longAxisDir * 0.5f) &&
               HasTileAt(mover.position - longAxisDir * 0.5f);
    }

    private static bool HasTileAt(Vector3 position)
    {
        return TryGetTileAt(position, out _);
    }

    private static bool IsGlassAt(Vector3 position)
    {
        if (!TryGetTileAt(position, out Collider tileCollider))
        {
            return false;
        }

        return tileCollider.GetComponentInParent<FragileTile>() != null ||
               tileCollider.GetComponentInParent<FlexibleGlass>() != null;
    }

    private static bool TryGetTileAt(Vector3 position, out Collider tileCollider)
    {
        Vector3 origin = new Vector3(position.x, 0.5f, position.z);
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, layerMask, QueryTriggerInteraction.Collide))
        {
            tileCollider = hit.collider;
            return true;
        }

        tileCollider = null;
        return false;
    }

    // ─── Durum Yönetim Metotları ─────────────────────
    public BlockState GetCurrentState()
    {
        BlockState state = new BlockState();
        state.isSplit = isSplit;
        if (!isSplit)
        {
            state.position = transform.position;
            state.rotation = transform.rotation;
        }
        else
        {
            if (player1 != null)
            {
                state.p1Position = player1.transform.position;
                state.p1Rotation = player1.transform.rotation;
            }
            if (player2 != null)
            {
                state.p2Position = player2.transform.position;
                state.p2Rotation = player2.transform.rotation;
            }
        }
        state.activeSplitPlayer = activeSplitPlayer;

        // Köprüleri kaydet
        state.bridgeStates = new System.Collections.Generic.List<BridgeState>();
        BridgeController[] bridges = FindObjectsByType<BridgeController>(FindObjectsSortMode.None);
        foreach (var bridge in bridges)
        {
            state.bridgeStates.Add(new BridgeState {
                channel = bridge.channel,
                isActive = bridge.IsActive()
            });
        }

        return state;
    }

    public void RestoreState(BlockState state, bool snapCamera = true)
    {
        // Önceki split nesneleri varsa temizle
        if (player1 != null) Destroy(player1);
        if (player2 != null) Destroy(player2);

        isSplit = state.isSplit;
        activeSplitPlayer = state.activeSplitPlayer;

        if (isSplit)
        {
            player1 = Create1x1Block(state.p1Position, "SplitPlayer1");
            player1.transform.rotation = state.p1Rotation;

            player2 = Create1x1Block(state.p2Position, "SplitPlayer2");
            player2.transform.rotation = state.p2Rotation;

            SetMainBlockActive(false);

            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.target = (activeSplitPlayer == 1) ? player1.transform : player2.transform;
                if (snapCamera)
                {
                    cameraFollow.SnapToTarget();
                }
            }

            CreateSwitchButton();
        }
        else
        {
            transform.position = state.position;
            transform.rotation = state.rotation;
            SetMainBlockActive(true);

            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.target = transform;
                if (snapCamera)
                {
                    cameraFollow.SnapToTarget();
                }
            }

            DestroySwitchButton();
        }

        // Köprüleri eski haline getir
        BridgeController[] bridges = FindObjectsByType<BridgeController>(FindObjectsSortMode.None);
        foreach (var bridge in bridges)
        {
            foreach (var savedBridge in state.bridgeStates)
            {
                if (savedBridge.channel == bridge.channel)
                {
                    bridge.SetActiveState(savedBridge.isActive);
                    break;
                }
            }
        }
    }

    private IEnumerator RunSimulation()
    {
        isSimulating = true;

        // Görselleri ve colliderları gizle
        SetRenderersAndCollidersActive(false);

        // Hızlandırılmış simülasyon ayarları
        float originalTumblingDuration = tumblingDuration;
        float originalTeleportStepDuration = teleportStepDuration;
        tumblingDuration = 0.0001f;
        teleportStepDuration = 0.0001f;

        correctStateHistory.Clear();
        correctStateHistory.Add(GetCurrentState());

        LevelLoader loader = LevelLoader.Instance;
        if (loader != null && loader.CurrentLevelData != null && !string.IsNullOrEmpty(loader.CurrentLevelData.hintMoves))
        {
            string[] moves = loader.CurrentLevelData.hintMoves.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string move in moves)
            {
                Vector3 dir = ParseMove(move);
                if (dir != Vector3.zero)
                {
                    if (isSplit)
                        StartCoroutine(Tumble1x1(dir));
                    else
                        StartCoroutine(Tumble(dir));

                    // Hareketin bitmesini bekle
                    yield return new WaitForSeconds(0.005f);
                    while (IsMoving) yield return null;

                    correctStateHistory.Add(GetCurrentState());
                }
            }
        }

        // Değerleri geri yükle
        tumblingDuration = originalTumblingDuration;
        teleportStepDuration = originalTeleportStepDuration;
        isSimulating = false;

        // Fırınlama (Bake) İşlemi: Yalnızca Editor modunda çalışır ve simüle edilen durumları JSON dosyasına yazar.
#if UNITY_EDITOR
        if (loader != null && loader.CurrentLevelData != null)
        {
            loader.CurrentLevelData.correctStates = new System.Collections.Generic.List<BlockState>(correctStateHistory);
            string updatedJson = JsonUtility.ToJson(loader.CurrentLevelData, true);
            string jsonPath = System.IO.Path.Combine(Application.dataPath, $"Resources/LevelsJSON/w{loader.worldIndex}l{loader.levelIndex}.json");
            try
            {
                System.IO.File.WriteAllText(jsonPath, updatedJson);
                Debug.Log($"<color=lime>[Auto-Baker]</color> Seviye {loader.worldIndex}-{loader.levelIndex} için durum geçmişi basariyla JSON'a fırınlandı!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Auto-Baker] JSON fırınlama hatası: {ex.Message}");
            }
        }
#endif

        // Sahneyi temiz bir şekilde yeniden yükle
        simulatedWorld = loader.worldIndex;
        simulatedLevel = loader.levelIndex;
        loader.LoadLevel(loader.worldIndex, loader.levelIndex);
    }

    private void SetRenderersAndCollidersActive(bool active)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = active;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = active;
    }

    public IEnumerator MatchAndRewind(System.Action<int> onComplete)
    {
        // Eğer oyuncu henüz hiç hareket etmediyse doğrudan 0. adımdan başla (geri sarma yok)
        if (playerStateHistory.Count <= 1 || correctStateHistory.Count == 0)
        {
            if (correctStateHistory.Count > 0)
            {
                playerStateHistory.Clear();
                playerStateHistory.Add(correctStateHistory[0]);
            }
            onComplete?.Invoke(0);
            yield break;
        }

        int matchCorrectIndex = 0;
        int matchPlayerIndex = 0;
        bool foundMatch = false;

        // Tersten başlayarak oyuncunun geçtiği yolları doğru yol ile karşılaştır
        for (int i = playerStateHistory.Count - 1; i >= 0; i--)
        {
            BlockState playerState = playerStateHistory[i];

            for (int j = correctStateHistory.Count - 1; j >= 0; j--)
            {
                BlockState correctState = correctStateHistory[j];

                if (StatesMatch(playerState, correctState))
                {
                    matchCorrectIndex = j;
                    matchPlayerIndex = i;
                    foundMatch = true;
                    break;
                }
            }
            if (foundMatch) break;
        }

        if (!foundMatch)
        {
            // Hiçbir çakışma bulunamazsa doğrudan başlangıca ışınla
            if (correctStateHistory.Count > 0)
            {
                RestoreState(correctStateHistory[0], true);
                playerStateHistory.Clear();
                playerStateHistory.Add(correctStateHistory[0]);
            }
            onComplete?.Invoke(0);
            yield break;
        }

        // Eğer zaten doğru konumdaysak doğrudan başla (geri sarma animasyonuna gerek yok)
        if (matchPlayerIndex == playerStateHistory.Count - 1)
        {
            onComplete?.Invoke(matchCorrectIndex);
            yield break;
        }

        // Geri sarma animasyonunu oynat
        PlaySound(AudioEventId.Undo, null);
        yield return StartCoroutine(RewindSequence(matchPlayerIndex, correctStateHistory[matchCorrectIndex]));

        onComplete?.Invoke(matchCorrectIndex);
    }

    private IEnumerator RewindSequence(int matchPlayerHistoryIndex, BlockState targetCorrectState)
    {
        isTumbling = true; // Geri sararken girdi engellemesi ve hareket kilidi koy

        // Her geri sarma adımı için sabit 0.4 saniye süre (kamera sarsıntısını önlemek için)
        float stepDuration = 0.4f;

        for (int k = playerStateHistory.Count - 1; k > matchPlayerHistoryIndex; k--)
        {
            BlockState fromState = playerStateHistory[k];
            BlockState toState = playerStateHistory[k - 1];

            // Her adımda hafif bir tık sesi çalınabilir
            PlaySound(AudioEventId.CubeMove, tumbleSound);

            yield return StartCoroutine(TransitionBetweenStates(fromState, toState, stepDuration));
        }

        // En son hedef durumu tamamen geri yükle (switchler ve köprüler dahil, kamerayı hedefe sabitleyerek)
        RestoreState(targetCorrectState, true);

        // Oyuncunun geçmişini çakıştığı adıma kadar kırp
        playerStateHistory.RemoveRange(matchPlayerHistoryIndex + 1, playerStateHistory.Count - (matchPlayerHistoryIndex + 1));

        isTumbling = false;
    }

    private IEnumerator TransitionBetweenStates(BlockState fromState, BlockState toState, float duration)
    {
        // Eğer split durumu değiştiyse (örn. birleşme veya ayrılma olduysa) ani geçiş yap
        if (fromState.isSplit != toState.isSplit)
        {
            RestoreState(toState, false);
            yield return new WaitForSeconds(duration);
            yield break;
        }

        // Görsel yapıyı hedef duruma göre hazırla (konumları henüz eşitlemeden, kamerayı sabitlemeden)
        RestoreState(toState, false);

        // Ancak geçici olarak başlangıç konumlarına yerleştir
        if (toState.isSplit)
        {
            if (player1 != null)
            {
                player1.transform.position = fromState.p1Position;
                player1.transform.rotation = fromState.p1Rotation;
            }
            if (player2 != null)
            {
                player2.transform.position = fromState.p2Position;
                player2.transform.rotation = fromState.p2Rotation;
            }
        }
        else
        {
            transform.position = fromState.position;
            transform.rotation = fromState.rotation;
        }

        // Yumuşak Lerp ile hedef konuma kaydır
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (toState.isSplit)
            {
                if (player1 != null)
                {
                    player1.transform.position = Vector3.Lerp(fromState.p1Position, toState.p1Position, t);
                    player1.transform.rotation = Quaternion.Slerp(fromState.p1Rotation, toState.p1Rotation, t);
                }
                if (player2 != null)
                {
                    player2.transform.position = Vector3.Lerp(fromState.p2Position, toState.p2Position, t);
                    player2.transform.rotation = Quaternion.Slerp(fromState.p2Rotation, toState.p2Rotation, t);
                }
            }
            else
            {
                transform.position = Vector3.Lerp(fromState.position, toState.position, t);
                transform.rotation = Quaternion.Slerp(fromState.rotation, toState.rotation, t);
            }
            yield return null;
        }

        // Son konuma tam eşitle
        if (toState.isSplit)
        {
            if (player1 != null) player1.transform.position = toState.p1Position;
            if (player2 != null) player2.transform.position = toState.p2Position;
        }
        else
        {
            transform.position = toState.position;
            transform.rotation = toState.rotation;
        }
    }

    private bool StatesMatch(BlockState a, BlockState b)
    {
        // X ve Z koordinatları yakın mı kontrol et (aynı hücre)
        float flatDist = Vector2.Distance(new Vector2(a.position.x, a.position.z), new Vector2(b.position.x, b.position.z));
        bool isSameTile = flatDist < 0.1f;

        if (Vector3.Distance(a.position, b.position) > 0.1f)
        {
            if (isSameTile)
            {
                Debug.LogWarning($"[StatesMatch] Aynı hücre ama Y konumu farklı! a.y: {a.position.y}, b.y: {b.position.y}");
            }
            return false;
        }

        if (a.isSplit != b.isSplit)
        {
            if (isSameTile)
            {
                Debug.LogWarning($"[StatesMatch] Aynı hücre ama split durumları farklı! a.isSplit: {a.isSplit}, b.isSplit: {b.isSplit}");
            }
            return false;
        }

        if (a.isSplit)
        {
            if (Vector3.Distance(a.p1Position, b.p1Position) > 0.1f || Vector3.Distance(a.p2Position, b.p2Position) > 0.1f)
            {
                if (isSameTile)
                {
                    Debug.LogWarning($"[StatesMatch] Aynı hücre ama split parça konumları farklı!");
                }
                return false;
            }
        }
        else
        {
            // Tek parça 1x2x1 blok kendi boyuna ekseninde simetriktir.
            // Bu nedenle 180 derecelik yön farkları veya kendi eksenindeki dönüşler fonksiyonel olarak farksızdır.
            // Sadece boyuna eksenin (local Up) dünya eksenindeki doğrultusunun çakışıp çakışmadığına (Dot product) bakıyoruz.
            Vector3 aUp = a.rotation * Vector3.up;
            Vector3 bUp = b.rotation * Vector3.up;
            float axisDot = Mathf.Abs(Vector3.Dot(aUp, bUp));

            if (axisDot < 0.95f)
            {
                if (isSameTile)
                {
                    Debug.LogWarning($"[StatesMatch] Aynı hücre ama oryantasyon farklı! Eksen doğrultu Dot farkı: {axisDot}");
                }
                return false;
            }
        }

        // Köprü durumlarını karşılaştır
        if (a.bridgeStates == null || b.bridgeStates == null) return false;
        if (a.bridgeStates.Count != b.bridgeStates.Count)
        {
            if (isSameTile)
            {
                Debug.LogWarning($"[StatesMatch] Aynı hücre ama köprü sayıları uyuşmuyor! a: {a.bridgeStates.Count}, b: {b.bridgeStates.Count}");
            }
            return false;
        }

        foreach (var bridgeA in a.bridgeStates)
        {
            bool matchedBridge = false;
            foreach (var bridgeB in b.bridgeStates)
            {
                if (bridgeB.channel == bridgeA.channel)
                {
                    if (bridgeB.isActive != bridgeA.isActive)
                    {
                        if (isSameTile)
                        {
                            Debug.LogWarning($"[StatesMatch] Aynı hücre ama Kanal {bridgeA.channel} köprü durumları uyuşmuyor! a: {bridgeA.isActive}, b: {bridgeB.isActive}");
                        }
                        return false;
                    }
                    matchedBridge = true;
                    break;
                }
            }
            if (!matchedBridge) return false;
        }

        return true;
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

}
