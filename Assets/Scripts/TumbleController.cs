using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FlexibleGlassDestructor;

public class TumbleController : MonoBehaviour
{
    public float tumblingDuration = 0.3f;
    
    [Header("Sounds")]
    public AudioClip tumbleSound;
    public AudioClip gameOverSound;
    public AudioClip winSound;

    private Rigidbody rb;
    private bool isTumbling = false;
    private bool playerFell = false;
    private Vector3 lastMoveDir = Vector3.forward;
    private Vector3 pendingConveyorDir = Vector3.zero; // Konveyör tile'dan gelen bekleyen yön

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

    /// <summary>
    /// Dışarıdan (HintController) hamle tetiklemek için çağrılır.
    /// Küp meşgulse false döner, değilse hareketi başlatıp true döner.
    /// </summary>
    public bool TryMoveExternal(Vector3 direction)
    {
        if (isTumbling || playerFell) return false;
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

        // Sectors (leaving ±10° deadzones around 45°, 135°, 225°, 315°):
        // Right: 0 to 35, 325 to 360
        // Up: 55 to 125
        // Left: 145 to 215
        // Down: 235 to 305
        if ((angle >= 0 && angle < 35f) || (angle >= 325f && angle <= 360f))
        {
            return Vector3.right;
        }
        else if (angle >= 55f && angle < 125f)
        {
            return Vector3.forward;
        }
        else if (angle >= 145f && angle < 215f)
        {
            return Vector3.left;
        }
        else if (angle >= 235f && angle < 305f)
        {
            return Vector3.back;
        }

        return Vector3.zero;
    }

    private void Update()
    {
        if (isSplit && !isTumbling && !playerFell)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
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
            if (touch.press.wasPressedThisFrame)
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
                if (delta.magnitude >= minSwipeDistance)
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
                    if (delta.magnitude >= minSwipeDistance)
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
        PlaySound(tumbleSound);
        
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
    }

    private IEnumerator Tumble(Vector3 direction)
    {
        isTumbling = true;
        lastMoveDir = direction;

        // Play tumble sound
        PlaySound(tumbleSound);

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
        CheckIfFell();

        // Konveyör tile tetiklediyse kayarak ilerle (devrilme değil)
        if (!playerFell && pendingConveyorDir != Vector3.zero)
        {
            Vector3 convDir = pendingConveyorDir;
            pendingConveyorDir = Vector3.zero;
            isTumbling = false;
            StartCoroutine(ConveyorSlide(convDir));
            yield break;
        }

        isTumbling = false;
    }

    /// <summary>
    /// Konveyör tile üzerinde: küp DİK konumda kalarak ok yönünde 1 hücre kayar.
    /// Rotasyon yoktur, sadece pozisyon değişir.
    /// </summary>
    private IEnumerator ConveyorSlide(Vector3 direction)
    {
        isTumbling = true;
        lastMoveDir = direction;

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
            isTumbling = false;
            StartCoroutine(ConveyorSlide(nextDir));
            yield break;
        }

        isTumbling = false;
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
        foreach (var obj in GameObject.FindObjectsOfType<GameObject>())
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

        PlaySound(gameOverSound);

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

        yield return new WaitForSeconds(2.0f);
        RestartLevel();
    }

    private IEnumerator FallDown(Vector3 customRotAxis, bool straightDown = false)
    {
        isTumbling = true;
        playerFell = true;

        PlaySound(gameOverSound);

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

        yield return new WaitForSeconds(2.0f);
        RestartLevel();
    }

    private IEnumerator WinLevel()
    {
        isTumbling = true;
        playerFell = true;

        PlaySound(winSound);

        // Rocket/fall-in exit animation
        Vector3 targetPos = transform.position - Vector3.up * 2f;
        float elapsed = 0f;
        while (elapsed < 1.0f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, elapsed);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, elapsed);
            elapsed += Time.deltaTime * 3f;
            yield return null;
        }

        // Load next level
        if (LevelLoader.Instance != null)
        {
            int nextLevel = LevelLoader.Instance.levelIndex + 1;
            if (nextLevel <= 20)
            {
                LevelLoader.Instance.LoadLevel(LevelLoader.Instance.worldIndex, nextLevel);
            }
            else
            {
                int nextWorld = LevelLoader.Instance.worldIndex + 1;
                if (nextWorld <= 10)
                {
                    LevelLoader.Instance.LoadLevel(nextWorld, 1);
                }
                else
                {
                    Debug.Log("Congratulations! All levels beaten!");
                    LevelLoader.Instance.LoadLevel(1, 1);
                }
            }
        }
    }

    private GameObject GetActivePlayer()
    {
        return (activeSplitPlayer == 1) ? player1 : player2;
    }

    private void StartSplit(Vector3 pos1, Vector3 pos2)
    {
        isSplit = true;
        activeSplitPlayer = 1;

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

        PlaySound(tumbleSound);

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

        PlaySound(gameOverSound);

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

        var allObjs = GameObject.FindObjectsOfType<GameObject>();
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
    }


    private void RestartLevel()
    {
        if (LevelLoader.Instance != null)
        {
            LevelLoader.Instance.LoadLevel(LevelLoader.Instance.worldIndex, LevelLoader.Instance.levelIndex);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

}
