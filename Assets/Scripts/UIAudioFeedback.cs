using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UIAudioFeedback :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler,
    ISelectHandler,
    ISubmitHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.IsActive() && button.IsInteractable())
        {
            AudioManager.Instance?.PlayUiEvent(AudioEventId.UiHover);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClick();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (button != null && button.IsActive() && button.IsInteractable())
        {
            AudioManager.Instance?.PlayUiEvent(AudioEventId.UiHover);
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClick();
    }

    private void PlayClick()
    {
        if (button == null || !button.IsActive() || !button.IsInteractable())
        {
            return;
        }

        string buttonName = gameObject.name.ToLowerInvariant();
        AudioEventId eventId = buttonName.Contains("restart")
            ? AudioEventId.Restart
            : buttonName.Contains("hint")
                ? AudioEventId.Hint
                : buttonName.Contains("undo") || buttonName.Contains("rewind")
                    ? AudioEventId.Undo
                    : AudioEventId.UiClick;

        AudioManager.Instance?.PlayUiEvent(eventId);
    }
}

public static class UIAudioFeedbackInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameObject installer = new GameObject("UI Audio Feedback Installer");
        Object.DontDestroyOnLoad(installer);
        installer.AddComponent<UIAudioFeedbackInstallRoutine>().Install(scene);
    }
}

public sealed class UIAudioFeedbackInstallRoutine : MonoBehaviour
{
    private Scene targetScene;

    public void Install(Scene scene)
    {
        targetScene = scene;
        StartCoroutine(InstallAfterUiStart());
    }

    private IEnumerator InstallAfterUiStart()
    {
        // Some gameplay buttons are created from Start(), so wait one frame.
        yield return null;

        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button.gameObject.scene == targetScene &&
                button.GetComponent<UIAudioFeedback>() == null)
            {
                button.gameObject.AddComponent<UIAudioFeedback>();
            }
        }

        Destroy(gameObject);
    }
}
