using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// BrainCube HUD: Pause and Level Complete overlays (SF UI style).
/// All open/close paths use the same slide animation.
/// </summary>
public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    private const string OpenParam = "Open";
    private const string ClosedState = "Closed";
    private const float CloseTimeout = 1.5f;

    [Header("Panels")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject levelCompleteRoot;
    [SerializeField] private Animator pauseAnimator;
    [SerializeField] private Animator levelCompleteAnimator;

    [Header("Scene Loading")]
    [SerializeField] private string mainMenuSceneName = "MainMenuNew";

    [Header("Optional HUD")]
    [SerializeField] private Button pauseHudButton;

    [Header("Level Complete UI")]
    [SerializeField] private Text levelCompleteWorldLabel;
    [SerializeField] private Text levelCompleteLevelLabel;
    [SerializeField] private Transform levelCompleteStarsRoot;

    public bool IsPaused { get; private set; }
    public bool IsLevelCompleteOpen { get; private set; }
    public bool BlocksGameplay => IsPaused || IsLevelCompleteOpen || isAnimatingMenu || TutorialManager.BlocksGameplay;

    private float previousTimeScale = 1f;
    private int openParamId;
    private bool isAnimatingMenu;
    private Coroutine menuRoutine;
    private Button levelCompleteNextButton;
    private Text levelCompleteAdvanceHint;
    private bool canAdvanceToNextLevel;

    private void Awake()
    {
        Instance = this;
        if (GetComponent<Canvas>() != null && GetComponent<HudSafeArea>() == null)
            gameObject.AddComponent<HudSafeArea>();
        openParamId = Animator.StringToHash(OpenParam);
        ResolveReferences();
        CloseAllMenusImmediate();
    }

    private void OnEnable()
    {
        LevelLoader.OnLevelLoaded += CloseAllMenusImmediate;
    }

    private void OnDisable()
    {
        LevelLoader.OnLevelLoaded -= CloseAllMenusImmediate;
    }

    private void OnDestroy()
    {
        LevelLoader.OnLevelLoaded -= CloseAllMenusImmediate;
        if (Instance == this)
        {
            Instance = null;
        }

        if (Time.timeScale <= 0f)
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }
    }

    private void Update()
    {
        if (isAnimatingMenu)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (IsLevelCompleteOpen)
        {
            return;
        }

        if (IsPaused)
        {
            Resume();
        }
        else
        {
            OpenPause();
        }
    }

    public void Configure(
        GameObject pausePanel,
        GameObject levelCompletePanel,
        Button hudPauseButton)
    {
        pauseRoot = pausePanel;
        levelCompleteRoot = levelCompletePanel;
        pauseAnimator = pausePanel != null ? pausePanel.GetComponent<Animator>() : null;
        levelCompleteAnimator = levelCompletePanel != null ? levelCompletePanel.GetComponent<Animator>() : null;
        pauseHudButton = hudPauseButton;
        ResolveReferences();
        WireHudButton();
        CloseAllMenusImmediate();
    }

    public void OpenPause()
    {
        if (IsLevelCompleteOpen || IsPaused || isAnimatingMenu)
        {
            return;
        }

        IsPaused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        OpenPanelAnimated(pauseRoot, pauseAnimator);
        AudioManager.Instance?.PlayUiEvent(AudioEventId.MenuTransition);
        AudioManager.Instance?.PauseMusic(true);
    }

    public void Resume()
    {
        if (!IsPaused || isAnimatingMenu)
        {
            return;
        }

        AudioManager.Instance?.PlayUiEvent(AudioEventId.UiClick);
        CloseActiveMenuAnimated(() =>
        {
            AudioManager.Instance?.PauseMusic(false);
        });
    }

    public void ShowLevelComplete()
    {
        if (IsLevelCompleteOpen || isAnimatingMenu)
        {
            return;
        }

        if (IsPaused)
        {
            HidePanelImmediate(pauseRoot, pauseAnimator);
            IsPaused = false;
        }

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : previousTimeScale;
        if (previousTimeScale <= 0f)
        {
            previousTimeScale = 1f;
        }

        PopulateLevelCompleteContent();
        Time.timeScale = 0f;
        IsLevelCompleteOpen = true;
        OpenPanelAnimated(levelCompleteRoot, levelCompleteAnimator);
        AudioManager.Instance?.PlayUiEvent(AudioEventId.MenuTransition);
    }

    private void PopulateLevelCompleteContent()
    {
        int world = LevelLoader.Instance != null ? LevelLoader.Instance.worldIndex : 1;
        int level = LevelLoader.Instance != null ? LevelLoader.Instance.levelIndex : 1;

        if (levelCompleteWorldLabel != null)
        {
            levelCompleteWorldLabel.text = $"WORLD  {world}";
        }

        if (levelCompleteLevelLabel != null)
        {
            levelCompleteLevelLabel.text = $"Level  {level}";
        }

        int playerMoves = 0;
        TumbleController player = FindObjectOfType<TumbleController>();
        if (player != null)
        {
            playerMoves = player.CurrentMoveCount;
        }

        LevelData data = LevelLoader.Instance != null ? LevelLoader.Instance.CurrentLevelData : null;
        int minMoves = LevelStarRating.GetMinimumMoves(data);
        int earnedStars = LevelStarRating.Evaluate(playerMoves, minMoves);
        LevelStarRating.SaveBestStars(world, level, earnedStars);
        canAdvanceToNextLevel = LevelProgress.NotifyLevelCompleted(world, level, earnedStars);

        // Son seviye sonrası ana menüye dönüş her zaman serbest
        if (IsFinalLevel(world, level))
        {
            canAdvanceToNextLevel = true;
        }

        ApplyStarDisplay(earnedStars);
        ApplyAdvanceRequirementFeedback(world, level, earnedStars, canAdvanceToNextLevel);
        ApplyNextButtonState(canAdvanceToNextLevel);
    }

    private static bool IsFinalLevel(int world, int level)
    {
        return LevelProgress.GetAbsoluteIndex(world, level) >= LevelProgress.MaxWorld * LevelProgress.LevelsPerWorld;
    }

    private void ApplyAdvanceRequirementFeedback(int world, int level, int earnedStars, bool canAdvance)
    {
        EnsureAdvanceHintLabel();

        if (levelCompleteLevelLabel != null)
        {
            levelCompleteLevelLabel.text = $"Level  {level}";
        }

        if (levelCompleteAdvanceHint == null)
        {
            return;
        }

        if (canAdvance)
        {
            levelCompleteAdvanceHint.gameObject.SetActive(false);
            return;
        }

        int required = GameplaySettings.MinStarsToAdvance;
        string starWord = required == 1 ? "star" : "stars";

        levelCompleteAdvanceHint.gameObject.SetActive(true);
        levelCompleteAdvanceHint.text =
            $"Need {required} {starWord} to unlock next\n" +
            $"You got {earnedStars} — Restart for better score";
    }

    private void ApplyNextButtonState(bool visible)
    {
        if (levelCompleteNextButton == null)
        {
            return;
        }

        levelCompleteNextButton.gameObject.SetActive(visible);
        levelCompleteNextButton.interactable = visible;
    }

    private void EnsureAdvanceHintLabel()
    {
        if (levelCompleteRoot == null)
        {
            return;
        }

        Transform window = levelCompleteRoot.transform.Find("Window");
        if (window == null)
        {
            return;
        }

        if (levelCompleteAdvanceHint == null)
        {
            Transform existing = window.Find("AdvanceHint");
            if (existing != null)
            {
                levelCompleteAdvanceHint = existing.GetComponent<Text>();
            }
        }

        if (levelCompleteAdvanceHint == null)
        {
            GameObject go = new GameObject("AdvanceHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(window, false);

            Text text = go.GetComponent<Text>();
            text.font = levelCompleteLevelLabel != null
                ? levelCompleteLevelLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            levelCompleteAdvanceHint = text;
            go.SetActive(false);
        }

        LayoutAdvanceHintNearStars();
    }

    private void LayoutAdvanceHintNearStars()
    {
        if (levelCompleteAdvanceHint == null)
        {
            return;
        }

        RectTransform rt = levelCompleteAdvanceHint.rectTransform;
        RectTransform starsRt = levelCompleteStarsRoot as RectTransform;
        RectTransform levelRt = levelCompleteLevelLabel != null
            ? levelCompleteLevelLabel.rectTransform
            : null;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(580f, 120f);

        // Level satırının hemen altında; yıldızların üstüne biner
        if (levelRt != null)
        {
            float levelBottom = levelRt.anchoredPosition.y - (levelRt.rect.height * levelRt.pivot.y);
            rt.anchoredPosition = new Vector2(0f, levelBottom - 20f);
        }
        else if (starsRt != null)
        {
            float starsTop = starsRt.anchoredPosition.y + (starsRt.rect.height * (1f - starsRt.pivot.y));
            rt.anchoredPosition = new Vector2(0f, starsTop + 24f);
        }
        else
        {
            rt.anchoredPosition = new Vector2(0f, 40f);
        }

        rt.SetAsLastSibling();

        levelCompleteAdvanceHint.fontSize = 36;
        levelCompleteAdvanceHint.alignment = TextAnchor.MiddleCenter;
        levelCompleteAdvanceHint.color = Color.white;

        if (levelCompleteAdvanceHint.GetComponent<Outline>() == null)
        {
            var outline = levelCompleteAdvanceHint.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    private void ApplyStarDisplay(int earnedStars)
    {
        if (levelCompleteStarsRoot == null)
        {
            return;
        }

        earnedStars = Mathf.Clamp(earnedStars, 0, 3);

        for (int i = 0; i < levelCompleteStarsRoot.childCount; i++)
        {
            Transform star = levelCompleteStarsRoot.GetChild(i);
            bool filled = i < earnedStars;

            Transform empty = star.Find("Empty star");
            Transform full = star.Find("Full star");

            if (empty != null)
            {
                empty.gameObject.SetActive(!filled);
            }

            if (full != null)
            {
                full.gameObject.SetActive(filled);
            }
        }
    }

    public void RestartLevel()
    {
        if (isAnimatingMenu)
        {
            return;
        }

        AudioManager.Instance?.PlayUiEvent(AudioEventId.Restart);
        CloseActiveMenuAnimated(() =>
        {
            AudioManager.Instance?.PauseMusic(false);
            DoRestartLevel();
        });
    }

    public void NextLevel()
    {
        if (isAnimatingMenu)
        {
            return;
        }

        if (!canAdvanceToNextLevel)
        {
            AudioManager.Instance?.PlayUiEvent(AudioEventId.UiClick);
            return;
        }

        AudioManager.Instance?.PlayUiEvent(AudioEventId.UiClick);

        int world = LevelLoader.Instance != null ? LevelLoader.Instance.worldIndex : 1;
        int level = LevelLoader.Instance != null ? LevelLoader.Instance.levelIndex : 1;
        bool showAd = AdsManager.ShouldShowLevelClearInterstitial(world, level);

        CloseActiveMenuAnimated(() =>
        {
            AudioManager.Instance?.PauseMusic(false);
            if (showAd)
                ShowInterstitialThen(LoadNextLevel);
            else
                LoadNextLevel();
        });
    }

    public void ReturnToMainMenu()
    {
        if (isAnimatingMenu)
        {
            return;
        }

        AudioManager.Instance?.PlayUiEvent(AudioEventId.UiClick);
        CloseActiveMenuAnimated(() =>
        {
            AudioManager.Instance?.PauseMusic(false);
            DoReturnToMainMenu();
        });
    }

    private void DoRestartLevel()
    {
        if (LevelLoader.Instance != null)
        {
            LevelLoader.Instance.LoadLevel(LevelLoader.Instance.worldIndex, LevelLoader.Instance.levelIndex);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void DoReturnToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private static void ShowInterstitialThen(System.Action continueAction)
    {
        if (AdsManager.Instance == null)
        {
            continueAction?.Invoke();
            return;
        }

        AdsManager.Instance.ShowInterstitial(continueAction);
    }

    private void LoadNextLevel()
    {
        if (LevelLoader.Instance == null)
        {
            return;
        }

        int world = LevelLoader.Instance.worldIndex;
        int level = LevelLoader.Instance.levelIndex;
        int nextLevel = level + 1;
        if (nextLevel <= LevelProgress.LevelsPerWorld)
        {
            if (!LevelProgress.IsLevelUnlocked(world, nextLevel))
            {
                Debug.LogWarning($"[GameUI] Next blocked — w{world}l{nextLevel} locked");
                return;
            }

            LevelLoader.Instance.LoadLevel(world, nextLevel);
            return;
        }

        int nextWorld = world + 1;
        if (nextWorld <= LevelProgress.MaxWorld)
        {
            if (!LevelProgress.IsLevelUnlocked(nextWorld, 1))
            {
                Debug.LogWarning($"[GameUI] Next blocked — w{nextWorld}l1 locked");
                return;
            }

            LevelLoader.Instance.LoadLevel(nextWorld, 1);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void CloseActiveMenuAnimated(System.Action onClosed)
    {
        GameObject root;
        Animator animator;

        if (IsLevelCompleteOpen)
        {
            root = levelCompleteRoot;
            animator = levelCompleteAnimator;
        }
        else if (IsPaused)
        {
            root = pauseRoot;
            animator = pauseAnimator;
        }
        else
        {
            onClosed?.Invoke();
            return;
        }

        if (menuRoutine != null)
        {
            StopCoroutine(menuRoutine);
        }

        menuRoutine = StartCoroutine(ClosePanelRoutine(root, animator, onClosed));
    }

    private void OpenPanelAnimated(GameObject root, Animator animator)
    {
        if (root == null)
        {
            return;
        }

        root.SetActive(true);
        if (animator == null)
        {
            return;
        }

        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.Play(ClosedState, 0, 0f);
        animator.Update(0f);
        animator.SetBool(openParamId, true);
    }

    private IEnumerator ClosePanelRoutine(GameObject root, Animator animator, System.Action onClosed)
    {
        isAnimatingMenu = true;

        if (root != null)
        {
            root.SetActive(true);
        }

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.SetBool(openParamId, false);

            float elapsed = 0f;
            while (elapsed < CloseTimeout && root != null && animator != null)
            {
                if (!animator.IsInTransition(0) &&
                    animator.GetCurrentAnimatorStateInfo(0).IsName(ClosedState))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        HidePanelImmediate(root, animator);
        IsPaused = false;
        IsLevelCompleteOpen = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        isAnimatingMenu = false;
        menuRoutine = null;

        onClosed?.Invoke();
    }

    private void ResolveReferences()
    {
        if (pauseRoot == null)
        {
            Transform found = transform.Find("PauseMenu");
            if (found != null)
            {
                pauseRoot = found.gameObject;
            }
        }

        if (levelCompleteRoot == null)
        {
            Transform found = transform.Find("LevelCompleteMenu");
            if (found != null)
            {
                levelCompleteRoot = found.gameObject;
            }
        }

        if (pauseAnimator == null && pauseRoot != null)
        {
            pauseAnimator = pauseRoot.GetComponent<Animator>();
        }

        if (levelCompleteAnimator == null && levelCompleteRoot != null)
        {
            levelCompleteAnimator = levelCompleteRoot.GetComponent<Animator>();
        }

        if (levelCompleteRoot != null)
        {
            if (levelCompleteWorldLabel == null)
            {
                Transform worldLabel = levelCompleteRoot.transform.Find("Window/Label World");
                if (worldLabel != null)
                {
                    levelCompleteWorldLabel = worldLabel.GetComponent<Text>();
                }
            }

            if (levelCompleteLevelLabel == null)
            {
                Transform levelLabel = levelCompleteRoot.transform.Find("Window/Label Level");
                if (levelLabel != null)
                {
                    levelCompleteLevelLabel = levelLabel.GetComponent<Text>();
                }
            }

            if (levelCompleteStarsRoot == null)
            {
                Transform stars = levelCompleteRoot.transform.Find("Window/Stars");
                if (stars != null)
                {
                    levelCompleteStarsRoot = stars;
                }
            }
        }

        if (pauseHudButton == null)
        {
            Transform hud = transform.Find("PauseButton");
            if (hud == null)
            {
                hud = transform.Find("MainMenuButton");
            }

            if (hud != null)
            {
                pauseHudButton = hud.GetComponent<Button>();
            }
        }

        WireHudButton();
        WirePanelButtons(pauseRoot, isPause: true);
        WirePanelButtons(levelCompleteRoot, isPause: false);
    }

    private void WireHudButton()
    {
        if (pauseHudButton == null)
        {
            return;
        }

        pauseHudButton.onClick.RemoveAllListeners();
        pauseHudButton.onClick.AddListener(OpenPause);
    }

    private void WirePanelButtons(GameObject root, bool isPause)
    {
        if (root == null)
        {
            return;
        }

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            string name = button.gameObject.name.ToLowerInvariant();
            button.onClick.RemoveAllListeners();

            if (name.Contains("resume"))
            {
                button.onClick.AddListener(Resume);
            }
            else if (name.Contains("restart"))
            {
                button.onClick.AddListener(RestartLevel);
            }
            else if (name.Contains("next"))
            {
                if (!isPause)
                {
                    levelCompleteNextButton = button;
                }

                button.onClick.AddListener(NextLevel);
            }
            else if (name.Contains("main") || name.Contains("quit") || name.Contains("exit") || name.Contains("menu"))
            {
                button.onClick.AddListener(ReturnToMainMenu);
            }
            else if (isPause && name.Contains("close"))
            {
                button.onClick.AddListener(Resume);
            }
        }
    }

    private void HidePanelImmediate(GameObject root, Animator animator)
    {
        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.SetBool(openParamId, false);
            animator.Play(ClosedState, 0, 0f);
            animator.Update(0f);
        }

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void CloseAllMenusImmediate()
    {
        if (menuRoutine != null)
        {
            StopCoroutine(menuRoutine);
            menuRoutine = null;
        }

        isAnimatingMenu = false;
        IsPaused = false;
        IsLevelCompleteOpen = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        HidePanelImmediate(pauseRoot, pauseAnimator);
        HidePanelImmediate(levelCompleteRoot, levelCompleteAnimator);
    }
}
